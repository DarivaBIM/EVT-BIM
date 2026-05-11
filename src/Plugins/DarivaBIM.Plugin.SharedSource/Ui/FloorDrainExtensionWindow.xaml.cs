using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using DarivaBIM.Infrastructure.Persistence.Settings;
using DarivaBIM.Plugin.Features.FloorDrainExtension;
using DarivaBIM.Presentation.Wpf.FloorDrainExtension;

namespace DarivaBIM.Plugin.Ui
{
    public partial class FloorDrainExtensionWindow : Window
    {
        private static FloorDrainExtensionWindow? _instance;

        private readonly FloorDrainExtensionScanExternalEvent _scanEvent = new();
        private readonly FloorDrainExtensionExternalEvent _runEvent = new();

        private FloorDrainExtensionSettings _settings = new();
        private bool _initialLoadDone;

        public FloorDrainExtensionViewModel ViewModel { get; }

        public FloorDrainExtensionWindow()
        {
            InitializeComponent();
            ViewModel = new FloorDrainExtensionViewModel();
            DataContext = ViewModel;
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
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.StatusMessage = text;
                ViewModel.IsBusy = false;
            }));
        }

        /// <summary>
        /// Chamado pelo external event de scan ao terminar de varrer o
        /// projeto: substitui a lista atual de grupos e atualiza o status
        /// na thread da UI.
        /// </summary>
        public void ApplyScanResult(
            IReadOnlyList<FloorDrainBoxGroupViewModel> groups,
            string statusMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.BoxGroups.Clear();
                foreach (FloorDrainBoxGroupViewModel g in groups)
                    ViewModel.BoxGroups.Add(g);

                ViewModel.StatusMessage = statusMessage;
                ViewModel.IsBusy = false;
                ViewModel.OnGroupsChanged();
            }));
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _settings = FloorDrainExtensionSettings.Load();
            double length = _settings.LengthMeters > 0 ? _settings.LengthMeters : 0.5;
            ViewModel.LengthMeters = length;
            LengthTextBox.Text = length.ToString("0.###", CultureInfo.InvariantCulture);
            _initialLoadDone = true;

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = "Lendo tipos de caixas do projeto…";
            _scanEvent.Raise(this, ViewModel, _settings);
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (_initialLoadDone)
                SaveCurrentSettings();
        }

        private void OnPickClicked(object sender, RoutedEventArgs e)
        {
            RunCreation(FloorDrainExtensionRunMode.PickInProject,
                "Selecione as caixas no Revit. Pressione ESC para finalizar.");
        }

        private void OnAllInProjectClicked(object sender, RoutedEventArgs e)
        {
            RunCreation(FloorDrainExtensionRunMode.AllInProject,
                "Inserindo prolongadores em todas as caixas do projeto…");
        }

        private void OnVisibleInViewClicked(object sender, RoutedEventArgs e)
        {
            RunCreation(FloorDrainExtensionRunMode.VisibleInActiveView,
                "Inserindo prolongadores nas caixas visíveis na vista ativa…");
        }

        private void RunCreation(FloorDrainExtensionRunMode mode, string busyMessage)
        {
            if (!TryReadLength(out double meters, out string error))
            {
                ViewModel.StatusMessage = error;
                return;
            }

            ViewModel.LengthMeters = meters;
            SaveCurrentSettings();

            Dictionary<long, long> pipeTypeBySymbolId = new();
            // Lê o mapeamento corrente do VM: para cada tipo de caixa que o
            // usuário tem um PipeType selecionado, passa o id ao adapter.
            // Caixas sem dropdown (sem tipo compatível) caem no fallback do
            // FloorDrainExtensionPipeTypeResolver dentro do Creator.
            for (int i = 0; i < ViewModel.BoxGroups.Count; i++)
            {
                FloorDrainBoxGroupViewModel g = ViewModel.BoxGroups[i];
                if (g.SelectedPipeType == null)
                    continue;
                long symbolId = g.SymbolIdHint;
                if (symbolId != 0)
                    pipeTypeBySymbolId[symbolId] = g.SelectedPipeType.Id;
            }

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = busyMessage;
            _runEvent.Raise(this, meters, mode, pipeTypeBySymbolId);
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _settings.LengthMeters = ViewModel.LengthMeters;

                foreach (FloorDrainBoxGroupViewModel g in ViewModel.BoxGroups)
                {
                    _settings.SetPipeTypeName(
                        g.FamilyName,
                        g.SymbolName,
                        g.SelectedPipeType?.Name);
                }

                _settings.Save();
            }
            catch
            {
                // Persistência best-effort.
            }
        }

        private bool TryReadLength(out double meters, out string error)
        {
            meters = 0;
            error = string.Empty;

            // Aceita "0,5" (locale BR) e "0.5" (invariant) — o usuário
            // pode digitar de qualquer um dos dois jeitos.
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
