using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Plugin.V2026.Features.PipeCadMapper;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.V2026.Ui
{
    public partial class PipeConverterWindow : Window
    {
        private static PipeConverterWindow? _instance;

        private readonly PipeConverterDataLoadExternalEvent _dataLoadEvent = new();
        private readonly PipeInsertionExternalEvent _pipeInsertionEvent = new();

        private PipeCadMapperSettings _initialSettings = new();
        private bool _initialLoadDone;

        public PipeConverterViewModel ViewModel { get; }

        public PipeConverterWindow()
        {
            InitializeComponent();
            ViewModel = new PipeConverterViewModel();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = ViewModel;
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
            // Carrega as preferências persistidas e pede para o handler
            // aplicá-las na primeira passagem de carga de dados.
            _initialSettings = PipeCadMapperSettings.Load();
            ViewModel.OffsetMm = _initialSettings.OffsetMm;
            _initialLoadDone = true;

            _dataLoadEvent.RaiseWithSettings(ViewModel, _initialSettings);
        }

        private void OnToggleClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsActive)
            {
                ViewModel.IsActive = false;
                ViewModel.StatusMessage = "Ferramenta desativada.";
                _pipeInsertionEvent.MarkNextCancelAsInternal();
                SendEscapeToRevit();
                return;
            }

            if (!HasMinimumConfiguration())
            {
                ViewModel.StatusMessage =
                    "Configuração incompleta — selecione sistema, tipo, diâmetro e nível antes de ativar.";
                return;
            }

            ViewModel.IsActive = true;
            ViewModel.StatusMessage = "Ferramenta ativa — clique em uma linha do vínculo CAD.";
            _pipeInsertionEvent.Raise(ViewModel);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            // Persiste o último estado da configuração para a próxima sessão.
            SaveCurrentSettings();

            if (ViewModel.IsActive)
            {
                ViewModel.IsActive = false;
                _pipeInsertionEvent.MarkNextCancelAsInternal();
                SendEscapeToRevit();
            }
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
                };
                settings.Save();
            }
            catch
            {
                // Persistência best-effort — falhas não devem impedir o fechamento.
            }
        }

        // Quando o usuário troca um parâmetro de inserção (sistema, tipo,
        // diâmetro, nível, offset) com a ferramenta ATIVA, cancela o PickObject
        // corrente e reagenda um novo pick. Sem isso, o ciclo de seleção podia
        // ficar travado depois de uma alteração no WPF.
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!ViewModel.IsActive)
                return;

            if (!IsInsertionParameter(e.PropertyName))
                return;

            // Marca esse cancel como interno antes de enviar o ESC, caso
            // contrário o handler interpretaria como ESC do usuário e
            // desativaria a ferramenta.
            _pipeInsertionEvent.MarkNextCancelAsInternal();
            SendEscapeToRevit();
            _pipeInsertionEvent.RaiseIfActive(ViewModel);
        }

        // Apenas parâmetros vindos de ComboBox: a alteração ocorre dentro do
        // próprio WPF (sem clique no canvas) então é seguro disparar ESC.
        // OffsetMm fica de fora para não criar corrida com o clique-pick
        // (TextBox.LostFocus dispara junto com a mudança de foco).
        private static bool IsInsertionParameter(string? propertyName)
        {
            return propertyName is
                nameof(PipeConverterViewModel.SelectedSystem) or
                nameof(PipeConverterViewModel.SelectedPipeType) or
                nameof(PipeConverterViewModel.SelectedDiameterMm) or
                nameof(PipeConverterViewModel.SelectedLevel);
        }

        // Chamado pelo App quando o documento ativo do Revit muda.
        // Recarrega sistemas/tipos/níveis para refletir o novo projeto, mas não
        // interrompe uma sessão de inserção em andamento.
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

                    // Em recargas (troca de projeto), não reaplicamos o
                    // snapshot inicial: usamos o estado corrente do VM como
                    // alvo das seleções por nome.
                    PipeCadMapperSettings reloadHints = _instance._initialLoadDone
                        ? new PipeCadMapperSettings
                        {
                            SystemName = _instance.ViewModel.SelectedSystem?.Name,
                            PipeTypeName = _instance.ViewModel.SelectedPipeType?.Name,
                            DiameterMm = _instance.ViewModel.SelectedDiameterMm,
                            LevelName = _instance.ViewModel.SelectedLevel?.Name,
                            OffsetMm = _instance.ViewModel.OffsetMm,
                        }
                        : _instance._initialSettings;

                    _instance._dataLoadEvent.RaiseWithSettings(_instance.ViewModel, reloadHints);
                }
            }));
        }

        private bool HasMinimumConfiguration()
        {
            return ViewModel.SelectedSystem != null
                && ViewModel.SelectedPipeType != null
                && ViewModel.SelectedDiameterMm.HasValue
                && ViewModel.SelectedLevel != null;
        }

        private const byte VK_ESCAPE = 0x1B;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void SendEscapeToRevit()
        {
            try
            {
                // Sem dar foco a Revit, o ESC pode ser consumido pela própria
                // janela WPF (Topmost) e o PickObject continua travado.
                IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(revitHandle);
                }

                keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch
            {
                // Se o sistema bloquear, o loop sai no próximo pick/ESC manual.
            }
        }
    }
}
