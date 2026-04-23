using Autodesk.Revit.UI;
using FamiliesImporterHub.Infrastructure;
using FamiliesImporterHub.Models;
using FamiliesImporterHub.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Threading;

namespace FamiliesImporterHub.UI
{
    public partial class FamiliesPage : Page, IDockablePaneProvider
    {
        private const int PageSize = 30;
        private const int SearchDebounceMilliseconds = 120;

        private readonly ApiClient _apiClient = new();
        private readonly ImportFamilyExternalEvent _importFamilyExternalEvent = new();
        private readonly List<FamilyItem> _allFamilies = new();
        private readonly ObservableCollection<FamilyItem> _visibleFamilies = new();
        private readonly DispatcherTimer _searchDebounceTimer;

        private List<FamilyItem> _filteredFamilies = new();
        private CancellationTokenSource? _searchCancellationTokenSource;
        private bool _hasLoaded;

        public FamiliesPage()
        {
            InitializeComponent();

            FamiliesItemsControl.ItemsSource = _visibleFamilies;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDebounceMilliseconds)
            };

            _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;
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

        private void OnFamilyCardClicked(object sender, MouseButtonEventArgs e)
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

            try
            {
                ImportFamilyRequest request = ImportFamilyRequest.FromFamily(family);
                _importFamilyExternalEvent.Raise(request);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Não foi possível preparar a importação da família.\n\n{ex.Message}");
            }
        }

        private void OnLoadMoreClicked(object sender, RoutedEventArgs e)
        {
            AppendNextPage();
        }

        private async Task LoadFamiliesAsync()
        {
            try
            {
                SetBusyState(true);

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

                await ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
            }
            catch (Exception ex)
            {
                _allFamilies.Clear();
                _filteredFamilies = new List<FamilyItem>();
                _visibleFamilies.Clear();
                UpdateVisualState();

                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Não foi possível carregar as famílias da API.\n\n{ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
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

            try
            {
                List<FamilyItem> result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(normalizedSearch))
                    {
                        return snapshot;
                    }

                    return snapshot
                        .Where(family => MatchesFast(family, normalizedSearch, compactSearch, searchTokens))
                        .ToList();
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _filteredFamilies = result;
                ResetVisibleFamilies();

                if (scrollToTop)
                {
                    GalleryScrollViewer.ScrollToHome();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ResetVisibleFamilies()
        {
            _visibleFamilies.Clear();

            int takeCount = Math.Min(PageSize, _filteredFamilies.Count);

            for (int i = 0; i < takeCount; i++)
            {
                _visibleFamilies.Add(_filteredFamilies[i]);
            }

            UpdateVisualState();
        }

        private void AppendNextPage()
        {
            int currentCount = _visibleFamilies.Count;
            int remainingCount = _filteredFamilies.Count - currentCount;
            int nextCount = Math.Min(PageSize, remainingCount);

            for (int i = 0; i < nextCount; i++)
            {
                _visibleFamilies.Add(_filteredFamilies[currentCount + i]);
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            EmptyStateTextBlock.Visibility = _filteredFamilies.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            LoadMoreButton.Visibility = _visibleFamilies.Count < _filteredFamilies.Count
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            LoadMoreButton.IsEnabled = !isBusy;
        }
    }
}