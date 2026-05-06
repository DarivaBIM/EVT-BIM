using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Infrastructure.Api.Clients;
using DarivaBIM.Infrastructure.Persistence.Cache;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DarivaBIM.Plugin.Ui
{
    public partial class FamiliesPage : Page, IDockablePaneProvider
    {
        private const int SearchDebounceMilliseconds = 120;
        private const int ResizeDebounceMilliseconds = 80;
        private const int SkeletonCardCount = 6;

        // Card width 176 + horizontal margin 6+6 from CardContainerStyle.
        private const double CardCellWidth = 188d;

        private readonly ApiClient _apiClient = new();
        private readonly ImportFamilyExternalEvent _importFamilyExternalEvent = new();
        private readonly FamilyCacheService _familyCacheService = new();
        private readonly FamilyDownloadService _familyDownloadService = new();
        private readonly List<FamilyItem> _allFamilies = new();
        private readonly ObservableCollection<FamilyRow> _rows = new();
        private readonly ObservableCollection<TagFilterOption> _tagFilters = new();
        private readonly HashSet<string> _selectedTagKeys = new(StringComparer.Ordinal);
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _resizeDebounceTimer;

        private List<FamilyItem> _filteredFamilies = new();
        private CancellationTokenSource? _searchCancellationTokenSource;
        private CancellationTokenSource? _currentDownloadCts;
        private bool _hasLoaded;
        private bool _lastLoadFailed;
        private bool _suppressDownloadProgress;
        private int _itemsPerRow = 1;

        public FamiliesPage()
        {
            InitializeComponent();

            GalleryList.ItemsSource = _rows;
            TagFiltersHost.ItemsSource = _tagFilters;
            SkeletonHost.ItemsSource = Enumerable.Range(0, SkeletonCardCount).ToArray();

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDebounceMilliseconds)
            };

            _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;

            _resizeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ResizeDebounceMilliseconds)
            };

            _resizeDebounceTimer.Tick += OnResizeDebounceTimerTick;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;
            await LoadFamiliesAsync();
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async void OnSearchDebounceTimerTick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            await ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private async void OnFamilyCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not FamilyItem family)
            {
                return;
            }

            if (family.DownloadLinks == null || family.DownloadLinks.Count == 0)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"A família \"{family.Name}\" não possui link de download disponível.");

                return;
            }

            // Download runs on the WPF thread pool BEFORE the ExternalEvent
            // fires, so the .rfa is already cached locally when the Revit-side
            // handler executes. This keeps Revit responsive during slow
            // network conditions.
            ImportFamilyRequest request;
            string localFilePath;

            try
            {
                request = ImportFamilyRequest.FromFamily(family);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Não foi possível preparar a importação da família.\n\n{ex.Message}");
                return;
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            _currentDownloadCts = cts;

            ShowDownloadOverlay(family.Name);

            Progress<DownloadProgress> progress = new Progress<DownloadProgress>(OnDownloadProgress);

            try
            {
                SetBusyState(true);
                localFilePath = await _familyDownloadService.DownloadToCacheAsync(
                    request,
                    _familyCacheService,
                    progress,
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
                SetBusyState(false);
                HideDownloadOverlay();
                ReleaseDownloadCts(cts);
                return;
            }
            catch (Exception ex)
            {
                // Hide the overlay BEFORE surfacing the error dialog —
                // otherwise the dialog stacks on top of a "stuck" progress
                // bar and the panel looks frozen until the user dismisses
                // it. Also release busy state so the search box re-enables.
                SetBusyState(false);
                HideDownloadOverlay();
                ReleaseDownloadCts(cts);

                TaskDialog.Show(
                    "FamiliesImporterHub",
                    "Não foi possível baixar o arquivo da família.\n\n" +
                    $"Família: {request.FamilyName}\n" +
                    $"URL: {request.DownloadUrl}\n\n" +
                    $"Erro: {ex.Message}");
                return;
            }

            // Snap the bar to 100% and show a brief "concluído" state so the
            // user sees a clean completion before the overlay closes. Without
            // this, the bar can stop a hair short of the end (last chunk
            // smaller than buffer, or the last Progress<T> report still in
            // flight on the dispatcher queue) and the overlay vanishes
            // mid-frame, which reads as "it froze and crashed".
            ShowDownloadComplete();
            await Task.Delay(220);

            SetBusyState(false);
            HideDownloadOverlay();
            ReleaseDownloadCts(cts);

            try
            {
                _importFamilyExternalEvent.Raise(request, localFilePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Não foi possível agendar a importação da família.\n\n{ex.Message}");
            }
        }

        private void ReleaseDownloadCts(CancellationTokenSource cts)
        {
            if (ReferenceEquals(_currentDownloadCts, cts))
            {
                _currentDownloadCts = null;
            }

            cts.Dispose();
        }

        private void OnGallerySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
            {
                return;
            }

            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();
        }

        private void OnResizeDebounceTimerTick(object? sender, EventArgs e)
        {
            _resizeDebounceTimer.Stop();

            // The number of dot-rows depends on the dock width, so a resize
            // can cross the overflow threshold even when the gallery row
            // count doesn't change.
            ScheduleExpandToggleVisibilityRefresh();

            int newItemsPerRow = ComputeItemsPerRow();

            if (newItemsPerRow == _itemsPerRow)
            {
                return;
            }

            _itemsPerRow = newItemsPerRow;
            RebuildRows();
        }

        private int ComputeItemsPerRow()
        {
            double available = GalleryList.ActualWidth;

            if (available <= 0d)
            {
                return Math.Max(_itemsPerRow, 1);
            }

            int count = (int)Math.Floor(available / CardCellWidth);
            return Math.Max(1, count);
        }

        private async Task LoadFamiliesAsync()
        {
            try
            {
                SetBusyState(true);
                _lastLoadFailed = false;

                List<FamilyItem> families = await _apiClient.GetFamiliesAsync();

                _allFamilies.Clear();

                List<FamilyItem> tigreFamilies = families
                    .Where(f =>
                        !string.IsNullOrWhiteSpace(f.ManufacturerName) &&
                        f.ManufacturerName.Trim().Equals("Tigre", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name)
                    .ToList();

                foreach (FamilyItem family in tigreFamilies)
                {
                    family.SearchIndex = BuildSearchIndex(family);
                    family.SearchIndexCompact = Compact(family.SearchIndex);

                    _allFamilies.Add(family);
                }

                RebuildTagFilters();

                await ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
            }
            catch (Exception ex)
            {
                _lastLoadFailed = true;
                _allFamilies.Clear();
                _filteredFamilies = new List<FamilyItem>();
                _rows.Clear();
                RebuildTagFilters();
                UpdateVisualState();

                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Não foi possível carregar as famílias da API.\n\n{ex.Message}");
            }
            finally
            {
                SetBusyState(false);
                SetInitialLoadingVisuals(isLoading: false);
            }
        }

        private void SetInitialLoadingVisuals(bool isLoading)
        {
            SkeletonHost.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            GalleryList.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowDownloadOverlay(string familyName)
        {
            _suppressDownloadProgress = false;
            DownloadingFamilyName.Text = familyName ?? string.Empty;
            DownloadProgressBar.IsIndeterminate = true;
            DownloadProgressBar.Value = 0d;
            DownloadProgressLabel.Text = "Conectando...";
            CancelDownloadButton.IsEnabled = true;
            DownloadOverlay.Visibility = Visibility.Visible;
        }

        private void HideDownloadOverlay()
        {
            DownloadOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowDownloadComplete()
        {
            // Latch out late Progress<T> reports — Progress<T> dispatches via
            // SyncContext.Post, so the last few byte-count updates may still
            // be queued behind us when the download finishes. Without this
            // flag they would race in and overwrite the "Concluído!" text.
            _suppressDownloadProgress = true;
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = DownloadProgressBar.Maximum;
            DownloadProgressLabel.Text = "Concluído!";
            CancelDownloadButton.IsEnabled = false;
        }

        private void OnDownloadProgress(DownloadProgress progress)
        {
            if (_suppressDownloadProgress)
            {
                return;
            }

            if (progress.Fraction is double fraction)
            {
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = fraction;
                DownloadProgressLabel.Text =
                    $"{(int)(fraction * 100)}% • {FormatBytes(progress.BytesDownloaded)} / {FormatBytes(progress.TotalBytes!.Value)}";
            }
            else
            {
                DownloadProgressBar.IsIndeterminate = true;
                DownloadProgressLabel.Text = FormatBytes(progress.BytesDownloaded);
            }
        }

        private void OnCancelDownloadClicked(object sender, RoutedEventArgs e)
        {
            CancelDownloadButton.IsEnabled = false;
            DownloadProgressLabel.Text = "Cancelando...";
            _currentDownloadCts?.Cancel();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024L * 1024L)
            {
                return $"{bytes / 1024d:F0} KB";
            }

            return $"{bytes / 1024d / 1024d:F1} MB";
        }

        private async Task ApplySearchAsync(string? rawSearch, bool scrollToTop)
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            CancellationToken cancellationToken = _searchCancellationTokenSource.Token;

            string normalizedSearch = NormalizeForSearch(rawSearch ?? string.Empty);
            string compactSearch = Compact(normalizedSearch);
            string[] searchTokens = Tokenize(normalizedSearch).ToArray();

            List<FamilyItem> snapshot = _allFamilies.ToList();
            HashSet<string> selectedTagsSnapshot = new HashSet<string>(_selectedTagKeys, StringComparer.Ordinal);

            bool hasSearch = !string.IsNullOrWhiteSpace(normalizedSearch);
            bool hasTagFilter = selectedTagsSnapshot.Count > 0;

            try
            {
                List<FamilyItem> result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!hasSearch && !hasTagFilter)
                    {
                        return snapshot;
                    }

                    return snapshot
                        .Where(family =>
                            (!hasSearch || MatchesFast(family, normalizedSearch, compactSearch, searchTokens)) &&
                            (!hasTagFilter || MatchesTags(family, selectedTagsSnapshot)))
                        .ToList();
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _filteredFamilies = result;
                RebuildRows();

                if (scrollToTop && _rows.Count > 0)
                {
                    GalleryList.ScrollIntoView(_rows[0]);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void RebuildRows()
        {
            int perRow = _itemsPerRow > 0 ? _itemsPerRow : ComputeItemsPerRow();
            _itemsPerRow = perRow;

            _rows.Clear();

            int total = _filteredFamilies.Count;

            for (int i = 0; i < total; i += perRow)
            {
                int take = Math.Min(perRow, total - i);
                FamilyItem[] items = new FamilyItem[take];

                for (int j = 0; j < take; j++)
                {
                    items[j] = _filteredFamilies[i + j];
                }

                _rows.Add(new FamilyRow(items));
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_filteredFamilies.Count > 0)
            {
                SetEmptyState(EmptyStateKind.Hidden);
                return;
            }

            if (_lastLoadFailed)
            {
                SetEmptyState(EmptyStateKind.ApiError);
            }
            else if (_allFamilies.Count == 0)
            {
                SetEmptyState(EmptyStateKind.NoFamiliesAvailable);
            }
            else
            {
                SetEmptyState(EmptyStateKind.FilteredOut);
            }
        }

        private enum EmptyStateKind
        {
            Hidden,
            ApiError,
            NoFamiliesAvailable,
            FilteredOut,
        }

        private void SetEmptyState(EmptyStateKind kind)
        {
            if (kind == EmptyStateKind.Hidden)
            {
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                return;
            }

            string icon;
            Color iconColor;
            string title;
            string hint;

            switch (kind)
            {
                case EmptyStateKind.ApiError:
                    icon = ""; // Segoe MDL2: Warning triangle
                    iconColor = Color.FromRgb(0xD9, 0x77, 0x06);
                    title = "Não foi possível carregar a biblioteca";
                    hint = "Verifique sua conexão e feche/reabra o painel para tentar novamente.";
                    break;

                case EmptyStateKind.NoFamiliesAvailable:
                    icon = ""; // Segoe MDL2: Info
                    iconColor = Color.FromRgb(0x94, 0xA3, 0xB8);
                    title = "Nenhuma família disponível";
                    hint = "A biblioteca Tigre não retornou famílias no momento.";
                    break;

                case EmptyStateKind.FilteredOut:
                default:
                    icon = ""; // Segoe MDL2: Search
                    iconColor = Color.FromRgb(0x94, 0xA3, 0xB8);
                    title = "Nenhum resultado";
                    hint = BuildFilteredOutHint();
                    break;
            }

            EmptyStateIcon.Text = icon;
            EmptyStateIcon.Foreground = new SolidColorBrush(iconColor);
            EmptyStateTitle.Text = title;
            EmptyStateHint.Text = hint;
            EmptyStatePanel.Visibility = Visibility.Visible;
        }

        private string BuildFilteredOutHint()
        {
            bool hasSearch = !string.IsNullOrWhiteSpace(SearchTextBox.Text);
            bool hasTags = _selectedTagKeys.Count > 0;

            if (hasSearch && hasTags)
            {
                return "Ajuste a busca ou limpe os filtros aplicados.";
            }

            if (hasSearch)
            {
                return "Nenhuma família corresponde à sua busca.";
            }

            if (hasTags)
            {
                return "Nenhuma família corresponde aos filtros selecionados.";
            }

            return "Nenhuma família corresponde aos critérios atuais.";
        }

        private void RebuildTagFilters()
        {
            foreach (TagFilterOption existing in _tagFilters)
            {
                existing.PropertyChanged -= OnTagFilterChanged;
            }

            _tagFilters.Clear();
            _selectedTagKeys.Clear();

            // Distinct by normalized description, taking the first original
            // casing as the chip label so "PVC" stays "PVC" instead of "pvc".
            Dictionary<string, string> distinct = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (FamilyItem family in _allFamilies)
            {
                if (family.Tags == null)
                {
                    continue;
                }

                for (int i = 0; i < family.Tags.Count; i++)
                {
                    FamilyTag tag = family.Tags[i];

                    if (tag == null || string.IsNullOrWhiteSpace(tag.Description))
                    {
                        continue;
                    }

                    string description = tag.Description.Trim();
                    string key = NormalizeForSearch(description);

                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (!distinct.ContainsKey(key))
                    {
                        distinct[key] = description;
                    }
                }
            }

            IEnumerable<TagFilterOption> ordered = distinct
                .Select(kvp => new TagFilterOption(kvp.Value, kvp.Key))
                .OrderBy(opt => opt.Description, StringComparer.OrdinalIgnoreCase);

            foreach (TagFilterOption opt in ordered)
            {
                opt.PropertyChanged += OnTagFilterChanged;
                _tagFilters.Add(opt);
            }

            TagFiltersCard.Visibility = _tagFilters.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Reset expansion when the catalog reloads — otherwise a fresh
            // tag set could open expanded with a stale "Ver menos" caption.
            ExpandFiltersToggle.IsChecked = false;
            ApplyExpandedFiltersState();
            ScheduleExpandToggleVisibilityRefresh();

            UpdateClearTagsButton();
        }

        private void OnExpandFiltersToggled(object sender, RoutedEventArgs e)
        {
            ApplyExpandedFiltersState();
        }

        private void ApplyExpandedFiltersState()
        {
            bool isExpanded = ExpandFiltersToggle.IsChecked == true;

            // 40px holds one row of 32px dots with 8px bottom margin; lifting
            // the cap to PositiveInfinity lets the WrapPanel grow naturally
            // when expanded, so the layout system clips/extends in one move.
            TagFiltersClip.MaxHeight = isExpanded
                ? double.PositiveInfinity
                : 40d;

            ExpandFiltersLabel.Text = isExpanded ? "Ver menos" : "Ver mais";

            // ChevronDown (E70D) → ChevronUp (E70E).
            ExpandFiltersChevron.Text = isExpanded ? "" : "";
        }

        private void ScheduleExpandToggleVisibilityRefresh()
        {
            // The WrapPanel hasn't measured its real desired size yet at the
            // moment RebuildTagFilters runs — we have to wait for the next
            // layout pass before we can decide whether to expose "Ver mais".
            Dispatcher.BeginInvoke(
                new Action(RefreshExpandToggleVisibility),
                DispatcherPriority.Loaded);
        }

        private void RefreshExpandToggleVisibility()
        {
            if (_tagFilters.Count == 0)
            {
                ExpandFiltersToggle.Visibility = Visibility.Collapsed;
                return;
            }

            // The clip border caps the host at 40px while collapsed, so
            // ActualHeight is useless for detecting overflow. Re-measure
            // the host against the available width with no height cap and
            // compare its DESIRED height to the cap. This correctly fires
            // even when the panel is currently collapsed.
            double availableWidth = TagFiltersClip.ActualWidth > 0
                ? TagFiltersClip.ActualWidth
                : double.PositiveInfinity;

            TagFiltersHost.Measure(new Size(availableWidth, double.PositiveInfinity));
            double naturalHeight = TagFiltersHost.DesiredSize.Height;

            ExpandFiltersToggle.Visibility = naturalHeight > 40d
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Re-trigger a layout pass so the host returns to whatever the
            // visual tree actually wants — measuring with infinity above
            // would otherwise leave a stale arrangement on the next render.
            TagFiltersHost.InvalidateMeasure();
        }

        private void OnTagFilterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TagFilterOption.IsSelected) ||
                sender is not TagFilterOption opt)
            {
                return;
            }

            if (opt.IsSelected)
            {
                _selectedTagKeys.Add(opt.Key);
            }
            else
            {
                _selectedTagKeys.Remove(opt.Key);
            }

            UpdateClearTagsButton();
            _ = ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private void OnClearTagsClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedTagKeys.Count == 0)
            {
                return;
            }

            // IsSelected = false fires PropertyChanged → OnTagFilterChanged →
            // ApplySearchAsync. Setting many at once produces N filter passes;
            // for the typical handful of chips this is harmless, and keeping
            // the path uniform avoids a parallel "skip-event" code branch.
            foreach (TagFilterOption opt in _tagFilters)
            {
                if (opt.IsSelected)
                {
                    opt.IsSelected = false;
                }
            }
        }

        private void UpdateClearTagsButton()
        {
            ClearTagsButton.Visibility = _selectedTagKeys.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static bool MatchesTags(FamilyItem family, IReadOnlyCollection<string> selectedKeys)
        {
            if (selectedKeys.Count == 0)
            {
                return true;
            }

            if (family.Tags == null || family.Tags.Count == 0)
            {
                return false;
            }

            foreach (string requiredKey in selectedKeys)
            {
                bool matched = false;

                for (int i = 0; i < family.Tags.Count; i++)
                {
                    FamilyTag tag = family.Tags[i];

                    if (tag == null || string.IsNullOrWhiteSpace(tag.Description))
                    {
                        continue;
                    }

                    if (NormalizeForSearch(tag.Description) == requiredKey)
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesFast(
            FamilyItem family,
            string normalizedSearch,
            string compactSearch,
            IReadOnlyList<string> searchTokens)
        {
            if (string.IsNullOrWhiteSpace(family.SearchIndex))
            {
                return false;
            }

            if (family.SearchIndex.Contains(normalizedSearch, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(compactSearch) &&
                family.SearchIndexCompact.Contains(compactSearch, StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < searchTokens.Count; i++)
            {
                string token = searchTokens[i];

                bool tokenMatched =
                    family.SearchIndex.Contains(token, StringComparison.Ordinal) ||
                    family.SearchIndexCompact.Contains(token, StringComparison.Ordinal);

                if (!tokenMatched)
                {
                    return false;
                }
            }

            return searchTokens.Count > 0;
        }

        private static string BuildSearchIndex(FamilyItem family)
        {
            var parts = new List<string>
            {
                family.Name,
                family.FileName,
                family.ManufacturerName
            };

            if (family.Keywords != null)
            {
                parts.AddRange(family.Keywords);
            }

            if (family.Tags != null)
            {
                parts.AddRange(
                    family.Tags
                        .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Description))
                        .Select(tag => tag.Description));
            }

            return NormalizeForSearch(
                string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))));
        }

        private static IEnumerable<string> Tokenize(string value)
        {
            return value
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal);
        }

        private static string Compact(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty);
        }

        private static string NormalizeForSearch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();

            foreach (char c in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append(' ');
                }
            }

            string withoutDiacritics = builder
                .ToString()
                .Normalize(NormalizationForm.FormC);

            return Regex.Replace(withoutDiacritics, @"\s+", " ").Trim();
        }

        private void SetBusyState(bool isBusy)
        {
            SearchTextBox.IsEnabled = !isBusy;
        }
    }

    public sealed class FamilyRow
    {
        public FamilyRow(IReadOnlyList<FamilyItem> items)
        {
            Items = items;
        }

        public IReadOnlyList<FamilyItem> Items { get; }
    }
}