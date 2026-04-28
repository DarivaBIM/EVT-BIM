using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.UI
{
    public partial class ParameterEditorWindow : Window
    {
        private static ParameterEditorWindow? _instance;

        private readonly ParameterEditorSelectionExternalEvent _selectionEvent = new();
        private readonly ParameterEditorApplyExternalEvent _applyEvent = new();

        // Mantemos os ElementIds em memória; a janela usa-os apenas como
        // referência. O Document e os Elements são revalidados a cada execução
        // do external event para evitar referências obsoletas.
        private readonly List<ElementId> _selectedIds = new();

        public ParameterEditorViewModel ViewModel { get; }

        public ParameterEditorWindow()
        {
            InitializeComponent();
            ViewModel = new ParameterEditorViewModel();
            DataContext = ViewModel;
        }

        public static ParameterEditorWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new ParameterEditorWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();
            return _instance;
        }

        public IReadOnlyList<ElementId> SelectedIds => _selectedIds;

        public void SetSelection(IReadOnlyList<ElementId> ids, IReadOnlyList<CommonParameterOption> commonParams)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _selectedIds.Clear();
                _selectedIds.AddRange(ids);
                ViewModel.SelectedCount = ids.Count;

                CommonParameterOption? previous = ViewModel.SelectedParameter;
                ViewModel.Parameters.Clear();

                foreach (CommonParameterOption opt in commonParams)
                    ViewModel.Parameters.Add(opt);

                if (previous != null)
                {
                    foreach (CommonParameterOption opt in ViewModel.Parameters)
                    {
                        if (opt.Name == previous.Name && opt.IsInstance == previous.IsInstance)
                        {
                            ViewModel.SelectedParameter = opt;
                            break;
                        }
                    }
                }

                if (ids.Count == 0)
                {
                    ViewModel.StatusMessage = "Nenhum elemento selecionado.";
                }
                else if (commonParams.Count == 0)
                {
                    ViewModel.StatusMessage = "Os elementos não compartilham nenhum parâmetro editável em comum.";
                }
                else
                {
                    ViewModel.StatusMessage = $"{ids.Count} elemento(s) prontos para edição.";
                }

                ViewModel.IsSelectionActive = false;
            }));
        }

        public void SetStatus(string text)
        {
            Dispatcher.BeginInvoke(new Action(() => ViewModel.StatusMessage = text));
        }

        public void SetSelectionActive(bool active)
        {
            Dispatcher.BeginInvoke(new Action(() => ViewModel.IsSelectionActive = active));
        }

        private void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSelectionActive = true;
            ViewModel.StatusMessage = "Selecione elementos no Revit. ENTER ou ESC para finalizar.";
            _selectionEvent.Raise(this);
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedIds.Count == 0)
            {
                ViewModel.StatusMessage = "Selecione pelo menos um elemento antes de aplicar.";
                return;
            }

            CommonParameterOption? param = ViewModel.SelectedParameter;
            if (param == null)
            {
                ViewModel.StatusMessage = "Escolha um parâmetro.";
                return;
            }

            if (!string.IsNullOrEmpty(ViewModel.ValidationMessage))
            {
                ViewModel.StatusMessage = ViewModel.ValidationMessage;
                return;
            }

            ViewModel.StatusMessage = "Aplicando…";
            _applyEvent.Raise(this, _selectedIds, param, ViewModel.Value ?? string.Empty);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _selectedIds.Clear();
        }
    }
}
