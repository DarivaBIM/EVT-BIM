using Autodesk.Revit.UI;
using DarivaBIM.Application.Common;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Infrastructure.Api.Clients;
using DarivaBIM.Infrastructure.Persistence.Cache;
using DarivaBIM.Infrastructure.Persistence.Preferences;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Ui.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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

        // Card width 168 + horizontal margin 5+5 do FamilyCardStyle.
        // Em painel de 440px (rail 56 + 28px de margem do gallery), 2 cards
        // por linha cabem com folga; em painéis estreitos cai pra 1.
        private const double CardCellWidth = 178d;

        private readonly ApiClient _apiClient = new();
        private readonly ImportFamilyExternalEvent _importFamilyExternalEvent = new();
        private readonly FamilyCacheService _familyCacheService = new();
        private readonly FamilyDownloadService _familyDownloadService = new();
        private readonly FamilyPreferencesService _preferences = new();
        private readonly List<FamilyItem> _allFamilies = new();
        private readonly ObservableCollection<FamilyRow> _rows = new();
        private readonly ObservableCollection<TagFilterOption> _tagFilters = new();
        // Cache do mapeamento família→sistemas, populado uma vez por load.
        // Chave é FamilyItem.Id; valor é a lista (ordem do catálogo) de
        // sistemas a que a família pertence. Computar isso por família por
        // filtro seria N×14×K-sinônimos por keystroke — caro num ListBox
        // virtualizado de centenas de itens.
        private readonly Dictionary<int, IReadOnlyList<string>> _familySistemas = new();
        // IDs de sistemas atualmente selecionados (subset dos 14 do catálogo).
        // OR-semantics: qualquer sistema selecionado mostra a família.
        private readonly HashSet<string> _selectedSistemaIds = new(StringComparer.Ordinal);
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _resizeDebounceTimer;

        private List<FamilyItem> _filteredFamilies = new();
        private CancellationTokenSource? _searchCancellationTokenSource;
        private CancellationTokenSource? _currentDownloadCts;
        private bool _hasLoaded;
        private bool _lastLoadFailed;
        private bool _suppressDownloadProgress;
        private int _itemsPerRow = 1;

        // Estado da navegação por aba (rail). "all" sempre tem dados; as
        // outras abas dependem de persistência local (favoritas/recentes)
        // ou de telemetria do servidor (populares) — em commit C ainda
        // não vinculadas, então caem em empty state "em breve".
        private string _activeTab = "all";

        // Critério de ordenação selecionado no toolbar. Aplicado em
        // ApplySearchAsync depois do filtro. "updated" é o default histórico
        // (a API já devolve por nome → reordeno por updatedAt no client).
        private string _currentSort = "updated";

        // Modo de exibição (grid/lista). Cada modo usa seu próprio
        // DataTemplate (GalleryRowGridTemplate inline na ListBox vs
        // GalleryRowListTemplate em Page.Resources) e diferentes
        // _itemsPerRow (responsivo vs sempre 1).
        private string _currentView = "grid";

        // Template inline da ListBox capturado no construtor para podermos
        // restaurar quando o usuário voltar do modo lista para grade.
        // Sem isso, GalleryList.ItemTemplate = null não restauraria —
        // ItemTemplate, uma vez setado, sobrescreve o inline.
        private DataTemplate? _gridRowTemplate;

        public FamiliesPage()
        {
            // Revit não cria uma System.Windows.Application própria; sem
            // isso, pack URIs relativos no XAML (Source="/Themes/..." em
            // ResourceDictionary.MergedDictionaries) resolvem contra
            // Assembly.GetEntryAssembly(), que volta null e dispara
            // XamlParseException. Apontar ResourceAssembly para o assembly
            // do plugin antes do InitializeComponent corrige a resolução
            // — typeof(FamiliesPage) discrimina V2025 vs V2026 porque cada
            // plugin compila sua própria cópia do código compartilhado.
            if (System.Windows.Application.ResourceAssembly == null)
            {
                System.Windows.Application.ResourceAssembly = typeof(FamiliesPage).Assembly;
            }

            InitializeComponent();

            InitializeSistemaFilters();
            InitializeFooter();

            // Captura o template inline da ListBox antes de qualquer swap
            // para modo lista; OnViewModeChanged usa essa referência para
            // voltar ao modo grade.
            _gridRowTemplate = GalleryList.ItemTemplate;

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

        // Footer mostra a versão da DLL do plugin (V2025 ou V2026), lida
        // de AssemblyName.Version. Como release.ps1 carimba a versão de
        // build no .csproj, ela reflete a release atual sem precisar de
        // string hardcoded.
        private void InitializeFooter()
        {
            try
            {
                Version version = typeof(FamiliesPage).Assembly.GetName().Version ?? new Version(0, 0, 0);
                FooterVersionText.Text = $"v {version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                FooterVersionText.Text = string.Empty;
            }
        }

        private enum FooterStatusKind
        {
            Loading,
            Synced,
            Offline,
        }

        private void SetFooterStatus(FooterStatusKind kind)
        {
            switch (kind)
            {
                case FooterStatusKind.Loading:
                    FooterStatusText.Text = "Atualizando…";
                    FooterStatusDot.Fill = (System.Windows.Media.Brush)FindResource("InkFaint");
                    break;
                case FooterStatusKind.Offline:
                    FooterStatusText.Text = "Sem conexão";
                    FooterStatusDot.Fill = (System.Windows.Media.Brush)FindResource("Warn");
                    break;
                case FooterStatusKind.Synced:
                default:
                    FooterStatusText.Text = "Sincronizado";
                    FooterStatusDot.Fill = (System.Windows.Media.Brush)FindResource("Success");
                    break;
            }
        }

        // Os 14 chips de sistema são fixos: vivem do catálogo, não dos dados.
        // Inicializados uma vez aqui para que o usuário veja a paleta inteira
        // de sistemas suportados antes mesmo de a API responder, e para que
        // recarregar a lista de famílias não derrube a seleção atual de
        // filtros (era o comportamento antigo, baseado em rebuild dinâmico).
        private void InitializeSistemaFilters()
        {
            foreach (Sistema sistema in SistemaCatalog.All)
            {
                TagFilterOption opt = new TagFilterOption(sistema);
                opt.PropertyChanged += OnTagFilterChanged;
                _tagFilters.Add(opt);
            }

            // O card "Filtrar por sistema" passa a estar sempre disponível
            // (antes ficava Collapsed enquanto a API não respondia). Mostra
            // os 14 chips de saída — o conteúdo abaixo do chevron continua
            // recolhido por padrão para reservar espaço vertical à galeria.
            TagFiltersCard.Visibility = Visibility.Visible;
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

        // Aba ativa do rail (Todas/Favoritas/Recentes/Populares/Coleções).
        // Em commit C, só "all" tem dados — as demais retornam empty state
        // "em breve" via UpdateVisualState. Commit E vai vincular favoritas
        // e recentes a um JSON local.
        private void OnRailTabChecked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.Tag is not string tag)
            {
                return;
            }

            if (_activeTab == tag)
            {
                return;
            }

            _activeTab = tag;
            UpdateHeaderTitle();

            // Não usa o debounce — mudar de aba é um gesto explícito e o
            // usuário espera resposta imediata, ao contrário do typing.
            _ = ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private void UpdateHeaderTitle()
        {
            HeaderTitleText.Text = _activeTab switch
            {
                "fav" => "Favoritas",
                "recent" => "Recentes",
                "popular" => "Populares",
                "collections" => "Coleções",
                _ => "Tigre",
            };
        }

        // Sort: o popup é toggleado pelo botão; clicar fora fecha (StaysOpen=False).
        private void OnSortButtonClicked(object sender, RoutedEventArgs e)
        {
            SortPopup.IsOpen = !SortPopup.IsOpen;
        }

        private void OnSortItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string sort)
            {
                return;
            }

            _currentSort = sort;
            SortLabel.Text = sort switch
            {
                "name" => "Nome A–Z",
                "newest" => "Mais novas",
                _ => "Atualização",
            };
            SortPopup.IsOpen = false;

            _ = ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private void OnViewModeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.Tag is not string view)
            {
                return;
            }

            if (_currentView == view)
            {
                return;
            }

            _currentView = view;

            // Troca o template e força 1 card por linha em modo lista
            // (cada FamilyRow vira uma linha do StackPanel vertical do
            // GalleryRowListTemplate; sem itemsPerRow=1 a lista
            // continuaria reagrupando 2 cards por linha como na grade).
            if (view == "list")
            {
                GalleryList.ItemTemplate = (DataTemplate)FindResource("GalleryRowListTemplate");
                _itemsPerRow = 1;
            }
            else
            {
                GalleryList.ItemTemplate = _gridRowTemplate;
                _itemsPerRow = ComputeItemsPerRow();
            }

            RebuildRows();
        }

        // Toggle do coração: serviço é a fonte da verdade, VM segue.
        // Click event aqui evita o ciclo ToggleButton→TwoWay binding→
        // PropertyChanged→ToggleFavorite→re-set→PropertyChanged que era
        // possível em casos de dessincronia entre a flag visual e o JSON
        // em disco.
        private void OnFavoriteHeartClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            // Disambiguação: o using Autodesk.Revit.UI traz outro
            // ToggleButton no escopo (botão do ribbon), por isso o tipo
            // WPF precisa do nome completo aqui.
            if (sender is not System.Windows.Controls.Primitives.ToggleButton tb ||
                tb.DataContext is not FamilyCardViewModel vm)
            {
                return;
            }

            // Service.ToggleFavorite retorna o novo estado canônico (após
            // gravar no JSON). Empurramos esse estado de volta no VM, que
            // por OneWay refletirá no IsChecked do botão.
            bool newState = _preferences.ToggleFavorite(vm.Family.Id);
            vm.IsFavorita = newState;

            // ToggleButton internamente flipou IsChecked ao tratar o Click
            // (antes do nosso handler). OneWay binding restaura a partir do
            // VM; se houve double-flip (clique rápido), `vm.IsFavorita = newState`
            // converge porque o service-toggle é a única operação atômica.
            tb.IsChecked = newState;
        }

        private async void OnSearchDebounceTimerTick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            await ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private async void OnFamilyCardClicked(object sender, MouseButtonEventArgs e)
        {
            // Tag agora carrega FamilyCardViewModel (não mais FamilyItem direto)
            // — a página passou a empacotar a entidade num VM com badges/IsNew
            // pré-computados pra render do card.
            if (sender is not Border border || border.Tag is not FamilyCardViewModel cardVm)
            {
                return;
            }

            // Cliques dentro de controles interativos do card (coração, futuros
            // botões) NÃO devem disparar import. Walk-up no visual tree do
            // OriginalSource: se algum ancestral até o Border é um ToggleButton
            // ou Button, deixa o controle interno processar o click.
            //
            // Mais robusto que marcar Handled em PreviewMouseLeftButtonDown/Up
            // no toggle (essa abordagem matava o sinal antes do ButtonBase
            // reconhecer o click — coração ficava sem responder).
            if (e.OriginalSource is DependencyObject src)
            {
                DependencyObject? walker = src;
                while (walker != null && !ReferenceEquals(walker, border))
                {
                    if (walker is System.Windows.Controls.Primitives.ButtonBase)
                    {
                        return;
                    }

                    walker = System.Windows.Media.VisualTreeHelper.GetParent(walker);
                }
            }

            // Marca o evento como tratado para que o mouse-up não suba
            // além do card (evita race com atalhos do Revit que podem
            // disparar comandos como "Criar Piso" se o foco passar
            // pra viewport durante o download).
            e.Handled = true;

            FamilyItem family = cardVm.Family;

            if (family.DownloadLinks == null || family.DownloadLinks.Count == 0)
            {
                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
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
                    FeatureNames.FamiliesImporter,
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
                    FeatureNames.FamiliesImporter,
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
            // 80ms só pra o usuário ver a barra "concluída" antes do overlay
            // sumir — antes era 220ms, percebido como atraso.
            await Task.Delay(80);

            SetBusyState(false);
            HideDownloadOverlay();
            ReleaseDownloadCts(cts);

            try
            {
                _importFamilyExternalEvent.Raise(request, localFilePath);

                // Histórico de Recentes: persistir só após Raise() bem-sucedido.
                // Se o ExternalEvent.Raise falhar (return Result.Failed do Revit),
                // gravar o "uso" produziria entrada no histórico de uma família
                // que o usuário não chegou a inserir no projeto — ruído.
                _preferences.RegisterRecentImport(family.Id);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
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
            // Em modo lista cada FamilyRow tem exatamente 1 card por design,
            // independente da largura. Resize não recalcula por nada.
            if (_currentView == "list")
            {
                return 1;
            }

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
                SetFooterStatus(FooterStatusKind.Loading);

                List<FamilyItem> families = await _apiClient.GetFamiliesAsync();

                _allFamilies.Clear();
                _familySistemas.Clear();

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
                    _familySistemas[family.Id] = SistemaCatalog.ResolveSistemaIds(family.Tags);
                }

                await ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
                SetFooterStatus(FooterStatusKind.Synced);
            }
            catch (Exception ex)
            {
                _lastLoadFailed = true;
                _allFamilies.Clear();
                _familySistemas.Clear();
                _filteredFamilies = new List<FamilyItem>();
                _rows.Clear();
                UpdateVisualState();
                SetFooterStatus(FooterStatusKind.Offline);

                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
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
            HashSet<string> selectedSistemasSnapshot = new HashSet<string>(_selectedSistemaIds, StringComparer.Ordinal);
            // Snapshot do mapeamento família→sistemas para o Task.Run rodar
            // sem tocar no Dictionary mutável da UI thread.
            Dictionary<int, IReadOnlyList<string>> familySistemasSnapshot = new Dictionary<int, IReadOnlyList<string>>(_familySistemas);
            string activeTab = _activeTab;
            string activeSort = _currentSort;

            // Snapshots de preferências para o filtro rodar fora da UI thread
            // sem corrida com gravação concorrente do JSON.
            HashSet<int> favoritesSnapshot = new HashSet<int>(_preferences.GetFavoriteIds());
            IReadOnlyList<RecentFamilyEntry> recentsSnapshot = _preferences.GetRecents();

            bool hasSearch = !string.IsNullOrWhiteSpace(normalizedSearch);
            bool hasSistemaFilter = selectedSistemasSnapshot.Count > 0;

            try
            {
                List<FamilyItem> result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Cada aba define sua própria fonte. "popular" e "collections"
                    // ainda não têm dados (comissão futura) e voltam vazias —
                    // UpdateVisualState diferencia "em breve" de "vazio".
                    IEnumerable<FamilyItem> filtered = activeTab switch
                    {
                        "all" => snapshot,
                        "fav" => snapshot.Where(f => favoritesSnapshot.Contains(f.Id)),
                        "recent" => OrderByRecency(snapshot, recentsSnapshot),
                        _ => Enumerable.Empty<FamilyItem>(),
                    };

                    if (hasSearch || hasSistemaFilter)
                    {
                        filtered = filtered.Where(family =>
                            (!hasSearch || MatchesFast(family, normalizedSearch, compactSearch, searchTokens)) &&
                            (!hasSistemaFilter || MatchesSistemas(family, selectedSistemasSnapshot, familySistemasSnapshot)));
                    }

                    // "recent" tem ordem natural por timestamp; deixar o sort
                    // do toolbar sobrescrever isso confunde o usuário ("eu pedi
                    // recentes mas vejo nome A-Z"). As outras abas honram o
                    // sort do toolbar.
                    if (activeTab != "recent")
                    {
                        filtered = ApplySort(filtered, activeSort);
                    }

                    return filtered.ToList();
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
                FamilyCardViewModel[] cards = new FamilyCardViewModel[take];

                for (int j = 0; j < take; j++)
                {
                    FamilyItem family = _filteredFamilies[i + j];
                    cards[j] = BuildCardViewModel(family);
                }

                _rows.Add(new FamilyRow(cards));
            }

            UpdateToolbarCount(total);
            UpdateVisualState();
        }

        private FamilyCardViewModel BuildCardViewModel(FamilyItem family)
        {
            // Resolve sistemas usando o cache pré-computado em LoadFamiliesAsync.
            // Sistema desconhecido (familia sem sistemas mapeados) renderiza
            // sem badges no rodapé, mas ainda aparece no card.
            IReadOnlyList<string>? sistemaIds = _familySistemas.TryGetValue(family.Id, out IReadOnlyList<string>? ids)
                ? ids
                : Array.Empty<string>();

            List<Sistema> sistemas = new List<Sistema>(sistemaIds.Count);

            for (int k = 0; k < sistemaIds.Count; k++)
            {
                Sistema? sistema = SistemaCatalog.FindById(sistemaIds[k]);
                if (sistema != null)
                {
                    sistemas.Add(sistema);
                }
            }

            return new FamilyCardViewModel(
                family,
                sistemas,
                _preferences.IsFavorite(family.Id));
        }

        private void UpdateToolbarCount(int count)
        {
            ToolbarCountNumber.Text = count.ToString(CultureInfo.InvariantCulture);
            ToolbarCountLabel.Text = count == 1 ? " família" : " famílias";
        }

        // Aplica a ordenação escolhida no toolbar. UpdatedAt/CreatedAt podem
        // ser null para famílias antigas — ordenamos null como mais antigo.
        private static IEnumerable<FamilyItem> ApplySort(IEnumerable<FamilyItem> source, string sort)
        {
            return sort switch
            {
                "name" => source.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
                "newest" => source.OrderByDescending(f => f.CreatedAt ?? DateTime.MinValue),
                _ => source.OrderByDescending(f => f.UpdatedAt ?? DateTime.MinValue),
            };
        }

        // Empareia famílias do catálogo com entradas do histórico de import,
        // preservando a ordem do histórico (mais recente primeiro). Famílias
        // que estavam no histórico mas saíram do catálogo (excluídas no
        // backend) são silenciosamente ignoradas.
        private static List<FamilyItem> OrderByRecency(
            List<FamilyItem> snapshot,
            IReadOnlyList<RecentFamilyEntry> recents)
        {
            Dictionary<int, FamilyItem> byId = new Dictionary<int, FamilyItem>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                byId[snapshot[i].Id] = snapshot[i];
            }

            List<FamilyItem> ordered = new List<FamilyItem>(recents.Count);
            for (int i = 0; i < recents.Count; i++)
            {
                if (byId.TryGetValue(recents[i].FamilyId, out FamilyItem? family))
                {
                    ordered.Add(family);
                }
            }

            return ordered;
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
            else if (_activeTab == "popular" || _activeTab == "collections")
            {
                // Estas duas abas dependem de telemetria/dados que ainda
                // não temos. Empty state explícito "em breve" evita confundir
                // o usuário com um grid vazio sem explicação.
                SetEmptyState(EmptyStateKind.ComingSoon);
            }
            else
            {
                // Inclui favoritas/recentes vazios — BuildFilteredOutHint
                // diferencia "ainda não favoritou nada" de "filtro zerou".
                SetEmptyState(EmptyStateKind.FilteredOut);
            }
        }

        private enum EmptyStateKind
        {
            Hidden,
            ApiError,
            NoFamiliesAvailable,
            FilteredOut,
            ComingSoon,
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

                case EmptyStateKind.ComingSoon:
                    icon = ""; // Segoe MDL2: ChevronRightSmall fallback (sem PNG ainda); intent: novidade
                    iconColor = Color.FromRgb(0x9A, 0x94, 0x8A); // InkFaint
                    title = BuildComingSoonTitle();
                    hint = "Estamos trabalhando para liberar essa visão em breve.";
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

        private string BuildComingSoonTitle()
        {
            return _activeTab switch
            {
                "popular" => "Populares em breve",
                "collections" => "Coleções em breve",
                _ => "Em breve",
            };
        }

        private string BuildFilteredOutHint()
        {
            bool hasSearch = !string.IsNullOrWhiteSpace(SearchTextBox.Text);
            bool hasSistemas = _selectedSistemaIds.Count > 0;
            bool hasFiltering = hasSearch || hasSistemas;

            // Sem filtros aplicados, a aba não tem dados próprios — o
            // empty hint explica como popular a aba (favoritar / importar)
            // em vez de pedir para "ajustar filtros".
            if (!hasFiltering)
            {
                return _activeTab switch
                {
                    "fav" => "Toque no coração de qualquer card para favoritar.",
                    "recent" => "Importe uma família para ela aparecer aqui.",
                    _ => "Nenhuma família corresponde aos critérios atuais.",
                };
            }

            if (hasSearch && hasSistemas)
            {
                return "Ajuste a busca ou limpe os filtros aplicados.";
            }

            if (hasSearch)
            {
                return "Nenhuma família corresponde à sua busca.";
            }

            return "Nenhuma família corresponde aos sistemas selecionados.";
        }

        private void OnFilterSectionToggled(object sender, RoutedEventArgs e)
        {
            ApplyFilterSectionState();
        }

        private void ApplyFilterSectionState()
        {
            bool isExpanded = FilterSectionToggle.IsChecked == true;

            TagFiltersHost.Visibility = isExpanded
                ? Visibility.Visible
                : Visibility.Collapsed;

            // ChevronRight (E76C) when collapsed -> ChevronDown (E70D) when
            // expanded; matches the rotation users expect from disclosure
            // triangles in tree views and accordion panels.
            FilterSectionChevron.Text = isExpanded ? "" : "";
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
                _selectedSistemaIds.Add(opt.Key);
            }
            else
            {
                _selectedSistemaIds.Remove(opt.Key);
            }

            UpdateClearTagsButton();
            _ = ApplySearchAsync(SearchTextBox.Text, scrollToTop: true);
        }

        private void OnClearTagsClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSistemaIds.Count == 0)
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
            int count = _selectedSistemaIds.Count;
            bool hasFilters = count > 0;

            ClearTagsButton.Visibility = hasFilters
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Badge contador no header de filtros. Mesma fonte de verdade
            // (_selectedSistemaIds) — atualiza junto pra evitar drift.
            FilterCountBadge.Visibility = hasFilters
                ? Visibility.Visible
                : Visibility.Collapsed;
            FilterCountText.Text = count.ToString(CultureInfo.InvariantCulture);
        }

        // OR-semantics: a família aparece se casar com QUALQUER um dos
        // sistemas marcados. Sem nenhum marcado, todas passam (sem filtro).
        // Antes era AND (todas precisavam casar), o que rapidamente zerava
        // a galeria quando o usuário marcava 2+ chips.
        private static bool MatchesSistemas(
            FamilyItem family,
            IReadOnlyCollection<string> selectedSistemaIds,
            IReadOnlyDictionary<int, IReadOnlyList<string>> familySistemas)
        {
            if (selectedSistemaIds.Count == 0)
            {
                return true;
            }

            if (!familySistemas.TryGetValue(family.Id, out IReadOnlyList<string>? sistemaIds) ||
                sistemaIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < sistemaIds.Count; i++)
            {
                if (selectedSistemaIds.Contains(sistemaIds[i]))
                {
                    return true;
                }
            }

            return false;
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

        private static string NormalizeForSearch(string value) => TigreTextUtils.NormalizeForSearch(value);

        private void SetBusyState(bool isBusy)
        {
            SearchTextBox.IsEnabled = !isBusy;
        }
    }

    public sealed class FamilyRow
    {
        public FamilyRow(IReadOnlyList<FamilyCardViewModel> cards)
        {
            Cards = cards;
        }

        public IReadOnlyList<FamilyCardViewModel> Cards { get; }
    }
}