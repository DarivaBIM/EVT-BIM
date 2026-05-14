using Autodesk.Revit.UI;
using DarivaBIM.Application.Common;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Infrastructure.Api.Clients;
using DarivaBIM.Infrastructure.Persistence.Cache;
using DarivaBIM.Infrastructure.Persistence.Preferences;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Ui.Models;
using DarivaBIM.Plugin.Ui.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        private bool _hasLoaded;
        private bool _lastLoadFailed;
        // Reentrance guard pro card-click — dividido em duas fases. Sem
        // os dois flags, dois downloads concorrentes brigariam pelo mesmo
        // arquivo de cache, ou dois ExternalEvents poderiam ser enfileirados
        // enquanto o Revit ainda processa o anterior (o handler é pesado:
        // OpenDocumentFile + LoadFamily + Activate + Prompt).
        //
        //   _isDownloading: setado durante o Task.Run do download HTTP/cache.
        //   _isImporting:   setado entre Raise() e o callback Completed
        //                   do handler — cobre a janela em que o Revit
        //                   ainda está com o UI thread preso carregando.
        private bool _isDownloading;
        private bool _isImporting;
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

            // Quando o handler do Revit termina (sucesso ou falha), limpa
            // _isImporting pra liberar o próximo clique. Sem essa pinga,
            // o flag ficaria true pra sempre e a galeria viraria read-only.
            _importFamilyExternalEvent.Completed += OnImportFamilyCompleted;
        }

        private void OnImportFamilyCompleted()
        {
            // ContextIdle (não Render) é proposital: queremos rodar DEPOIS
            // que o WPF esgotou a fila normal — incluindo qualquer render
            // pass pendente do nosso lado. Só então forçamos a repaint
            // no nível Win32, garantindo que estamos invalidando em cima
            // do frame mais novo do WPF, não no meio de um pass.
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _isImporting = false;
                    ForcePaneRepaint();
                }),
                DispatcherPriority.ContextIdle);
        }

        // --- Workaround pro freeze pós-PromptForFamilyInstancePlacement ---
        //
        // Causa raiz: DockablePane do Revit envelopa nosso FrameworkElement
        // numa HWND própria gerenciada pelo AdWindows.dll do Revit. Durante
        // o nested message pump do PromptForFamilyInstancePlacement, esse
        // wrapper acaba com estado de composição stale — só `WM_SIZE` em
        // cascata (que o usuário dispara maximizando o Revit) o reseta.
        //
        // Sem API pública pra forçar isso, replicamos a operação no Win32:
        //   1) Walk completo na cadeia de HWND-pais a partir do HwndSource
        //      do WPF — invalida cada nível com flags equivalentes ao que
        //      o sistema dispara em redimensionamento.
        //   2) `SetWindowPos` com `SWP_FRAMECHANGED` no nível do dock pane
        //      — força o `WM_NCCALCSIZE` + `WM_NCPAINT` que o `WM_SIZE`
        //      naturalmente dispararia, sem realmente mudar dimensão.
        //   3) `Window.Activate()` no nível WPF — restaura o estado de
        //      ativação que o nested pump pode ter deixado inconsistente.
        //
        // ComponentDispatcher.PushModal/PopModal no ImportFamilyHandler
        // (lado Revit) é mantido como padrão MS documentado — não custa
        // e cobre o cenário em que o problema é WPF-modal-state.

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(
            IntPtr hWnd,
            IntPtr lprcUpdate,
            IntPtr hrgnUpdate,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        // RedrawWindow flags equivalentes ao que o sistema envia em
        // WM_SIZE/restore: invalida toda a área, marca o frame não-cliente
        // como sujo, força o repaint imediato em vez de só agendar, e
        // propaga pra todos os filhos.
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_FRAME = 0x0400;

        // SetWindowPos flags: não mexer em posição, tamanho, z-order ou
        // ativação — só forçar o ciclo WM_NCCALCSIZE/WM_NCPAINT via
        // SWP_FRAMECHANGED. É o mesmo gatilho que o Revit usa internamente
        // quando o usuário maximiza, sem o efeito visual.
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private void ForcePaneRepaint()
        {
            try
            {
                if (PresentationSource.FromVisual(this) is not HwndSource source)
                    return;

                IntPtr hwnd = source.Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                const uint redrawFlags =
                    RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN | RDW_FRAME;
                const uint frameChangedFlags =
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED;

                // Walk completo da cadeia de pais, invalidando cada nível.
                // O wrapper de docking do Revit pode estar a N HWNDs acima
                // do HwndSource do WPF — não basta hardcode 2 níveis.
                // O loop limita em 6 saltos pra cortar infinite-loop em caso
                // de janela órfã; a árvore real do Revit é tipicamente 3-4.
                IntPtr current = hwnd;
                for (int hop = 0; hop < 6 && current != IntPtr.Zero; hop++)
                {
                    RedrawWindow(current, IntPtr.Zero, IntPtr.Zero, redrawFlags);

                    // SetWindowPos com SWP_FRAMECHANGED só no dock pane
                    // wrapper (1 nível acima do HwndSource) — replica o
                    // WM_NCCALCSIZE/WM_NCPAINT que o WM_SIZE de maximize
                    // produz, sem alterar dimensões ou ativação.
                    if (hop == 1)
                    {
                        SetWindowPos(
                            current,
                            IntPtr.Zero,
                            0, 0, 0, 0,
                            frameChangedFlags);
                    }

                    current = GetParent(current);
                }

                // No nível WPF, reativa a Window — o nested message pump
                // do PromptForFamilyInstancePlacement pode ter deixado o
                // foco/ativação em estado inconsistente, e Activate() é
                // a forma WPF-pública de pedir restauração disso.
                try
                {
                    Window.GetWindow(this)?.Activate();
                }
                catch
                {
                    // Activate pode falhar em alguns estados; não é fatal.
                }
            }
            catch
            {
                // Falha em forçar repaint não trava nada — apenas o
                // sintoma original (frame stale) permanece, e o próximo
                // gesto do usuário acaba acordando a DWM via input.
            }
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

            // Guard contra reentrada cobrindo as duas fases: enquanto
            // estiver baixando o .rfa OU enquanto o handler do Revit
            // ainda estiver processando o anterior, o segundo clique é
            // ignorado em silêncio. Sem o segundo flag, o usuário podia
            // disparar imports concorrentes empilhando ExternalEvents
            // enquanto o Revit segura o UI thread no OpenDocumentFile.
            if (_isDownloading || _isImporting)
            {
                return;
            }

            FamilyItem family = cardVm.Family;

            if (family.DownloadLinks == null || family.DownloadLinks.Count == 0)
            {
                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
                    $"A família \"{family.Name}\" não possui link de download disponível.");

                return;
            }

            ImportFamilyRequest request;

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

            // Download runs entirely off the WPF dispatcher — Task.Run garante
            // que toda a I/O síncrona inicial (Directory.CreateDirectory,
            // File.Exists, FileInfo, File.Delete do .download anterior) cai
            // no thread pool. Sem isso, antivírus ou disco lento conseguem
            // congelar o painel por centenas de ms antes mesmo do primeiro
            // await na HTTP request.
            _isDownloading = true;
            string localFilePath;

            try
            {
                localFilePath = await Task.Run(() =>
                    _familyDownloadService.DownloadToCacheAsync(
                        request,
                        _familyCacheService));
            }
            catch (Exception ex)
            {
                _isDownloading = false;

                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
                    "Não foi possível baixar o arquivo da família.\n\n" +
                    $"Família: {request.FamilyName}\n" +
                    $"URL: {request.DownloadUrl}\n\n" +
                    $"Erro: {ex.Message}");
                return;
            }

            _isDownloading = false;

            // Sinaliza re-entrância pra próxima fase ANTES de Raise: o
            // handler pode demorar (OpenDocumentFile bloqueia o UI thread)
            // e o usuário não pode disparar um segundo import enquanto o
            // primeiro ainda estiver em curso. _isImporting é zerado pelo
            // callback Completed do ExternalEvent.
            _isImporting = true;

            try
            {
                _importFamilyExternalEvent.Raise(request, localFilePath);

                // Histórico de Recentes: persistir só após Raise() bem-sucedido.
                // Se o ExternalEvent.Raise falhar, gravar o "uso" produziria
                // entrada no histórico de uma família que o usuário não chegou
                // a inserir no projeto — ruído. Roda fire-and-forget no thread
                // pool porque envolve File.WriteAllText do JSON de prefs.
                int familyId = family.Id;
                _ = Task.Run(() => _preferences.RegisterRecentImport(familyId));
            }
            catch (Exception ex)
            {
                // Raise falhou: o handler não vai rodar e não vai disparar
                // Completed. Limpa o flag manualmente aqui pra não travar
                // a galeria.
                _isImporting = false;

                TaskDialog.Show(
                    FeatureNames.FamiliesImporter,
                    $"Não foi possível agendar a importação da família.\n\n{ex.Message}");
            }
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
                    family.SearchIndex = FamilySearchHelpers.BuildSearchIndex(family);
                    family.SearchIndexCompact = FamilySearchHelpers.Compact(family.SearchIndex);

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

        private async Task ApplySearchAsync(string? rawSearch, bool scrollToTop)
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            CancellationToken cancellationToken = _searchCancellationTokenSource.Token;

            string normalizedSearch = FamilySearchHelpers.NormalizeForSearch(rawSearch ?? string.Empty);
            string compactSearch = FamilySearchHelpers.Compact(normalizedSearch);
            string[] searchTokens = FamilySearchHelpers.Tokenize(normalizedSearch).ToArray();

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
                        "recent" => FamilySorter.OrderByRecency(snapshot, recentsSnapshot),
                        _ => Enumerable.Empty<FamilyItem>(),
                    };

                    if (hasSearch || hasSistemaFilter)
                    {
                        filtered = filtered.Where(family =>
                            (!hasSearch || FamilySearchHelpers.MatchesFast(family, normalizedSearch, compactSearch, searchTokens)) &&
                            (!hasSistemaFilter || FamilySearchHelpers.MatchesSistemas(family, selectedSistemasSnapshot, familySistemasSnapshot)));
                    }

                    // "recent" tem ordem natural por timestamp; deixar o sort
                    // do toolbar sobrescrever isso confunde o usuário ("eu pedi
                    // recentes mas vejo nome A-Z"). As outras abas honram o
                    // sort do toolbar.
                    if (activeTab != "recent")
                    {
                        filtered = FamilySorter.ApplySort(filtered, activeSort);
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

        private void SetBusyState(bool isBusy)
        {
            SearchTextBox.IsEnabled = !isBusy;
        }
    }
}