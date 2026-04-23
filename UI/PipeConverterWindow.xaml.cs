using System.ComponentModel;
using System.Windows;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.UI
{
    public partial class PipeConverterWindow : Window
    {
        private static PipeConverterWindow? _instance;

        private readonly PipeConverterDataLoadExternalEvent _dataLoadEvent = new();

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
            // Passo 3: disparará o ExternalEvent do loop de PickObject.
            ViewModel.IsActive = !ViewModel.IsActive;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Passo 5: garantir que o loop de PickObject saia se a janela for fechada com a ferramenta ativa.
            ViewModel.IsActive = false;
        }
    }
}
