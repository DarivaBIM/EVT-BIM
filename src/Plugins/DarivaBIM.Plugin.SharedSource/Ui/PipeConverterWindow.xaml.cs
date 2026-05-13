using System;
using System.ComponentModel;
using System.Windows;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Abstractions.Hosting;

namespace DarivaBIM.Plugin.Ui
{
    public partial class PipeConverterWindow : Window
    {
        private static PipeConverterWindow? _instance;

        private readonly PipeConverterDataLoadExternalEvent _dataLoadEvent = new();
        private readonly CadLinkPickExternalEvent _cadLinkPickEvent = new();
        private readonly MarkerLinePickExternalEvent _linePickEvent = new();
        private readonly BifilarMarkerLinePickExternalEvent _bifilarLinePickEvent = new();
        private readonly MarkerBatchExternalEvent _batchEvent = new();
        private readonly MarkerConversionExternalEvent _convertEvent = new();
        private readonly MarkerCountRefreshExternalEvent _countRefreshEvent = new();
        private readonly IRevitPickCancellationService _pickCancellationService =
            new Win32RevitPickCancellationService();

        private PipeCadMapperSettings _initialSettings = new();
        private bool _initialLoadDone;

        public PipeConverterViewModel ViewModel { get; }

        public PipeConverterWindow()
        {
            // Revit não cria uma System.Windows.Application própria; sem
            // isso, pack URIs relativos no XAML (Source="/Themes/..." em
            // ResourceDictionary.MergedDictionaries) resolvem contra
            // Assembly.GetEntryAssembly(), que volta null e dispara
            // XamlParseException ao InitializeComponent. Apontar
            // ResourceAssembly para o assembly do plugin antes do
            // InitializeComponent corrige a resolução — typeof
            // (PipeConverterWindow) discrimina V2025 vs V2026 porque cada
            // plugin compila sua própria cópia do código compartilhado.
            if (System.Windows.Application.ResourceAssembly == null)
            {
                System.Windows.Application.ResourceAssembly = typeof(PipeConverterWindow).Assembly;
            }

            InitializeComponent();
            ViewModel = new PipeConverterViewModel();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = ViewModel;

            SourceInitialized += (_, _) => WindowChromeHelper.DisableMinimize(this);
            StateChanged += OnWindowStateChanged;
        }

        // Backstop pra ALT+Space → Minimize e Win+Down: o P/Invoke já cobre o
        // botão do chrome, mas atalhos de teclado ainda passam.
        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        }

        public static PipeConverterWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new PipeConverterWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
            {
                _instance.Show();
            }

            _instance.Activate();
            return _instance;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _initialSettings = PipeCadMapperSettings.Load();
            ViewModel.OffsetMm = _initialSettings.OffsetMm;
            ViewModel.UseCadElevation = _initialSettings.UseCadElevation;
            // O handler de data-load aplica o nível de tolerância junto com
            // sistema/tipo/nível persistidos. Garante que a UI já mostra o
            // ComboBox preenchido na primeira renderização.
            _initialLoadDone = true;

            _dataLoadEvent.RaiseWithSettings(ViewModel, _initialSettings);
            _countRefreshEvent.Raise(ViewModel);
        }

        // Encerra qualquer pick em andamento (unifilar ou bifilar). Os dois
        // events recebem o sinal de "próximo cancel é interno" porque, do
        // ponto de vista da janela, não sabemos qual loop está ativo a essa
        // altura — o handler que estiver de fato esperando consome o flag e
        // o outro permanece sem efeito até o próximo pick dele.
        private void EndActivePickLoop()
        {
            if (!ViewModel.IsActive) return;
            ViewModel.IsActive = false;
            _linePickEvent.MarkNextCancelAsInternal();
            _bifilarLinePickEvent.MarkNextCancelAsInternal();
            _pickCancellationService.CancelPendingPick();
        }

        private void OnPickCadClicked(object sender, RoutedEventArgs e)
        {
            // Ao iniciar uma nova escolha de CAD, encerra qualquer pick em
            // andamento e zera o estado de "ativa" para não confundir o
            // usuário no próximo Pick do CAD.
            EndActivePickLoop();
            _cadLinkPickEvent.Raise(ViewModel);
        }

        private void OnToggleLinePickClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsActive)
            {
                EndActivePickLoop();
                return;
            }

            if (!HasMinimumConfiguration())
            {
                ViewModel.StatusMessage = "Selecione vínculo CAD, layer, sistema, tipo, diâmetro e nível antes de ativar.";
                return;
            }

            ViewModel.IsActive = true;
            if (ViewModel.Mode == PipeCadMappingMode.Bifilar)
            {
                ViewModel.StatusMessage = $"Clique em uma das paredes do tubo no layer '{ViewModel.SelectedLayer}'.";
                _bifilarLinePickEvent.Raise(ViewModel);
            }
            else
            {
                ViewModel.StatusMessage = $"Clique em uma linha do layer '{ViewModel.SelectedLayer}' para criar marcadores.";
                _linePickEvent.Raise(ViewModel);
            }
        }

        private void OnBatchClicked(object sender, RoutedEventArgs e)
        {
            // Se um pick estiver em andamento, encerra antes de rodar o batch
            // (não queremos o usuário com duas modalidades concorrentes).
            EndActivePickLoop();

            if (!HasMinimumConfiguration())
            {
                ViewModel.StatusMessage = "Selecione vínculo CAD, layer, sistema, tipo, diâmetro e nível antes de criar marcadores em lote.";
                return;
            }

            if (ViewModel.IsBusy)
                return;

            ViewModel.StatusMessage = ViewModel.Mode == PipeCadMappingMode.Bifilar
                ? "Detectando tubos bifilar... isso pode levar alguns segundos."
                : "Criando marcadores para o layer...";

            _batchEvent.Raise(ViewModel);
        }

        private void OnConvertMarkersClicked(object sender, RoutedEventArgs e)
        {
            EndActivePickLoop();

            if (ViewModel.IsBusy)
                return;

            ViewModel.StatusMessage = "Convertendo marcadores em tubos...";
            _convertEvent.Raise(ViewModel);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            SaveCurrentSettings();

            EndActivePickLoop();
        }

        private void SaveCurrentSettings()
        {
            try
            {
                PipeCadMapperSettings settings = new()
                {
                    SystemName = ViewModel.SelectedSystem?.Name,
                    PipeTypeName = ViewModel.SelectedPipeType?.Name,
                    DiameterMm = ViewModel.SelectedDiameterMm,
                    LevelName = ViewModel.SelectedLevel?.Name,
                    OffsetMm = ViewModel.OffsetMm,
                    LayerName = ViewModel.SelectedLayer,
                    Mode = ViewModel.Mode.ToString(),
                    UseCadElevation = ViewModel.UseCadElevation,
                    ToleranceLevel = ViewModel.ToleranceLevel.ToString(),
                };
                settings.Save();
            }
            catch
            {
                // Persistência best-effort — falhas não devem impedir o fechamento.
            }
        }

        // Quando o usuário troca um parâmetro de inserção (sistema, tipo,
        // diâmetro, nível, layer, modo) com a ferramenta ATIVA (pick linha-a-
        // linha), cancela o PickObject corrente e reagenda. Sem isso, o ciclo
        // de seleção podia ficar travado depois de uma alteração no WPF.
        // O dispatch entre unifilar/bifilar é decidido pelo Mode corrente do
        // ViewModel (mudanças em IsUnifilar/IsBifilar/Mode também caem aqui).
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!ViewModel.IsActive)
                return;

            if (!IsLineLoopParameter(e.PropertyName))
                return;

            if (ViewModel.Mode == PipeCadMappingMode.Bifilar)
            {
                _bifilarLinePickEvent.MarkNextCancelAsInternal();
                _pickCancellationService.CancelPendingPick();
                _bifilarLinePickEvent.RaiseIfActive(ViewModel);
            }
            else
            {
                _linePickEvent.MarkNextCancelAsInternal();
                _pickCancellationService.CancelPendingPick();
                _linePickEvent.RaiseIfActive(ViewModel);
            }
        }

        private static bool IsLineLoopParameter(string? propertyName)
        {
            return propertyName is
                nameof(PipeConverterViewModel.SelectedSystem) or
                nameof(PipeConverterViewModel.SelectedPipeType) or
                nameof(PipeConverterViewModel.SelectedDiameterMm) or
                nameof(PipeConverterViewModel.SelectedLevel) or
                nameof(PipeConverterViewModel.SelectedLayer) or
                nameof(PipeConverterViewModel.UseCadElevation) or
                nameof(PipeConverterViewModel.Mode);
        }

        internal static void RequestDataReload()
        {
            PipeConverterWindow? w = _instance;
            if (w == null || w.ViewModel.IsActive)
                return;

            w.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_instance != null && !_instance.ViewModel.IsActive)
                {
                    _instance.ViewModel.StatusMessage = "Recarregando dados do projeto…";

                    // Em recargas (troca de projeto), o CAD selecionado pode
                    // não existir mais; zera para forçar nova seleção.
                    _instance.ViewModel.SelectedCadLinkId = null;
                    _instance.ViewModel.SelectedCadLinkName = null;
                    _instance.ViewModel.CadLayers.Clear();
                    _instance.ViewModel.ActiveViewMarkerCount = 0;

                    PipeCadMapperSettings reloadHints = _instance._initialLoadDone
                        ? new PipeCadMapperSettings
                        {
                            SystemName = _instance.ViewModel.SelectedSystem?.Name,
                            PipeTypeName = _instance.ViewModel.SelectedPipeType?.Name,
                            DiameterMm = _instance.ViewModel.SelectedDiameterMm,
                            LevelName = _instance.ViewModel.SelectedLevel?.Name,
                            OffsetMm = _instance.ViewModel.OffsetMm,
                            LayerName = _instance.ViewModel.SelectedLayer,
                            Mode = _instance.ViewModel.Mode.ToString(),
                            UseCadElevation = _instance.ViewModel.UseCadElevation,
                            ToleranceLevel = _instance.ViewModel.ToleranceLevel.ToString(),
                        }
                        : _instance._initialSettings;

                    _instance._dataLoadEvent.RaiseWithSettings(_instance.ViewModel, reloadHints);
                    _instance._countRefreshEvent.Raise(_instance.ViewModel);
                }
            }));
        }

        private bool HasMinimumConfiguration()
        {
            return ViewModel.HasCadLink
                && !string.IsNullOrEmpty(ViewModel.SelectedLayer)
                && ViewModel.SelectedSystem != null
                && ViewModel.SelectedPipeType != null
                && ViewModel.SelectedDiameterMm.HasValue
                && ViewModel.SelectedLevel != null;
        }
    }
}
