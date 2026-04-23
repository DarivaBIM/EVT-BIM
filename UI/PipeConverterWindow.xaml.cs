using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.UI
{
    public partial class PipeConverterWindow : Window
    {
        private static PipeConverterWindow? _instance;

        private readonly PipeConverterDataLoadExternalEvent _dataLoadEvent = new();
        private readonly PipeInsertionExternalEvent _pipeInsertionEvent = new();

        public PipeConverterViewModel ViewModel { get; }

        public PipeConverterWindow()
        {
            InitializeComponent();
            ViewModel = new PipeConverterViewModel();
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
            _dataLoadEvent.Raise(ViewModel);
        }

        private void OnToggleClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsActive)
            {
                ViewModel.IsActive = false;
                ViewModel.StatusMessage = "Ferramenta desativada.";
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
            if (ViewModel.IsActive)
            {
                ViewModel.IsActive = false;
                SendEscapeToRevit();
            }
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

        private static void SendEscapeToRevit()
        {
            try
            {
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
