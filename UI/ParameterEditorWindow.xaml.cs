using System;
using System.Collections.Generic;
using System.Linq;
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
            // Sempre marshalar para o thread de UI; usar Invoke (sincrono) ao
            // invés de BeginInvoke evita corridas onde o usuário consegue
            // disparar "Aplicar" antes do dispatcher ter atualizado os IDs.
            Action update = () =>
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
                    ViewModel.NoCommonParametersMessage = string.Empty;
                    ViewModel.StatusMessage = "Nenhum elemento selecionado.";
                }
                else if (commonParams.Count == 0)
                {
                    ViewModel.NoCommonParametersMessage =
                        "Os elementos selecionados não compartilham nenhum parâmetro editável em comum.";
                    ViewModel.StatusMessage = $"{ids.Count} elemento(s) selecionado(s).";
                }
                else
                {
                    ViewModel.NoCommonParametersMessage = string.Empty;
                    ViewModel.StatusMessage = $"{ids.Count} elemento(s) prontos para edição.";
                }

                ViewModel.IsSelectionActive = false;
            };

            if (Dispatcher.CheckAccess())
                update();
            else
                Dispatcher.Invoke(update);
        }

        public void SetStatus(string text)
        {
            if (Dispatcher.CheckAccess())
                ViewModel.StatusMessage = text;
            else
                Dispatcher.Invoke(() => ViewModel.StatusMessage = text);
        }

        public void SetSelectionActive(bool active)
        {
            if (Dispatcher.CheckAccess())
                ViewModel.IsSelectionActive = active;
            else
                Dispatcher.Invoke(() => ViewModel.IsSelectionActive = active);
        }

        private void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSelectionActive = true;
            ViewModel.StatusMessage = "Selecione elementos no Revit. ENTER ou ESC para finalizar.";

            // Snapshot das disciplinas marcadas no momento do clique.
            IReadOnlyList<Discipline> disciplines = ViewModel.SelectedDisciplines;
            _selectionEvent.Raise(this, disciplines);
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            // Snapshot dos IDs selecionados no exato momento do clique, no
            // thread de UI. Isso garante que cada clique aplica nos elementos
            // que estavam selecionados naquele instante, mesmo que a janela
            // permaneça aberta entre múltiplas seleções.
            List<ElementId> snapshot = _selectedIds.ToList();

            if (snapshot.Count == 0)
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
            _applyEvent.Raise(this, snapshot, param, ViewModel.Value ?? string.Empty);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _selectedIds.Clear();
        }
    }
}
