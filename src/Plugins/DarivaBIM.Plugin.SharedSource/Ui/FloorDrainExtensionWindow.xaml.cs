using System;
using System.Globalization;
using System.Windows;
using DarivaBIM.Plugin.Features.FloorDrainExtension;

namespace DarivaBIM.Plugin.Ui
{
    public partial class FloorDrainExtensionWindow : Window
    {
        private static FloorDrainExtensionWindow? _instance;

        private readonly FloorDrainExtensionExternalEvent _externalEvent = new();

        public FloorDrainExtensionWindow()
        {
            InitializeComponent();
        }

        public static FloorDrainExtensionWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new FloorDrainExtensionWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();
            return _instance;
        }

        public void SetStatus(string text)
        {
            Dispatcher.BeginInvoke(new Action(() => StatusTextBlock.Text = text));
        }

        private void OnPickClicked(object sender, RoutedEventArgs e)
        {
            if (!TryReadLength(out double meters, out string error))
            {
                StatusTextBlock.Text = error;
                return;
            }

            StatusTextBlock.Text = "Selecione as caixas no Revit. Pressione ESC para finalizar.";
            _externalEvent.Raise(this, meters);
        }

        private bool TryReadLength(out double meters, out string error)
        {
            meters = 0;
            error = string.Empty;

            string raw = (LengthTextBox.Text ?? string.Empty).Trim().Replace(",", ".");
            if (string.IsNullOrEmpty(raw))
            {
                error = "Informe um comprimento em metros (ex.: 0.5).";
                return false;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                error = "Comprimento inválido. Use número decimal (ex.: 0.5).";
                return false;
            }

            if (parsed <= 0)
            {
                error = "O comprimento deve ser maior que zero.";
                return false;
            }

            meters = parsed;
            return true;
        }
    }
}
