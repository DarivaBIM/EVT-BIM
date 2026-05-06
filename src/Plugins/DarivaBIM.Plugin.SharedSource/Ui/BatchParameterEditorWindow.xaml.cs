using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Features.BatchParameterEditor;
using DarivaBIM.Presentation.Wpf.BatchParameterEditor;
using DarivaBIM.Revit.Adapters.Features.BatchParameterEditor;

namespace DarivaBIM.Plugin.Ui
{
    public partial class BatchParameterEditorWindow : Window
    {
        private static BatchParameterEditorWindow? _instance;

        private readonly BatchParameterEditorSelectionExternalEvent _selectionEvent = new();
        private readonly BatchParameterEditorApplyExternalEvent _applyEvent = new();

        // Mantemos os ElementIds em memória; a janela usa-os apenas como
        // referência. O Document e os Elements são revalidados a cada execução
        // do external event para evitar referências obsoletas.
        private readonly List<ElementId> _selectedIds = new();

        // O ViewModel só conhece tipos neutros (Presentation.Wpf), então
        // guardamos por aqui o CommonParameterOption original de cada opção
        // exibida — assim o handler do botão "Aplicar" sabe qual StorageType
        // entregar ao ExternalEvent que toca a RevitAPI.
        private readonly Dictionary<(string Name, bool IsInstance), CommonParameterOption> _adapterByVm = new();

        public BatchParameterEditorViewModel ViewModel { get; }

        public BatchParameterEditorWindow()
        {
            InitializeComponent();
            ViewModel = new BatchParameterEditorViewModel();
            DataContext = ViewModel;
        }

        public static BatchParameterEditorWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new BatchParameterEditorWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();
            return _instance;
        }

        public IReadOnlyList<ElementId> SelectedIds => _selectedIds;

        public void SetSelection(
            IReadOnlyList<ElementId> ids,
            IReadOnlyList<CommonParameterOption> commonParams,
            string categoriesSummary)
        {
            // Sempre marshalar para o thread de UI; usar Invoke (sincrono) ao
            // invés de BeginInvoke evita corridas onde o usuário consegue
            // disparar "Aplicar" antes do dispatcher ter atualizado os IDs.
            Action update = () =>
            {
                _selectedIds.Clear();
                _selectedIds.AddRange(ids);
                ViewModel.SelectedCount = ids.Count;
                ViewModel.SelectionCategoriesSummary = categoriesSummary ?? string.Empty;

                ParameterOptionViewModel? previous = ViewModel.SelectedParameter;
                ViewModel.Parameters.Clear();
                _adapterByVm.Clear();

                foreach (CommonParameterOption opt in commonParams)
                {
                    ParameterOptionViewModel vmOpt = new(
                        opt.Name,
                        BatchParameterEditorTypeMapping.ToValueKind(opt.StorageType),
                        opt.IsInstance);

                    ViewModel.Parameters.Add(vmOpt);
                    _adapterByVm[(opt.Name, opt.IsInstance)] = opt;
                }

                if (previous != null)
                {
                    foreach (ParameterOptionViewModel opt in ViewModel.Parameters)
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
                    ViewModel.StatusMessage = $"{ids.Count} elemento(s) selecionado(s), mas sem parâmetros em comum.";
                }
                else
                {
                    ViewModel.NoCommonParametersMessage = string.Empty;
                    ViewModel.StatusMessage =
                        $"{ids.Count} elemento(s) prontos. Escolha o parâmetro e o valor.";
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

        public void NotifyApplyCompleted(ParameterApplyResult result)
        {
            Action update = () =>
            {
                ViewModel.StatusMessage = BuildShortStatus(result);
                ShowResultDialog(result);
            };

            if (Dispatcher.CheckAccess())
                update();
            else
                Dispatcher.Invoke(update);
        }

        private void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSelectionActive = true;
            ViewModel.StatusMessage =
                "Selecione no Revit. Ctrl+clique adiciona, Shift+clique remove. Clique em Concluir na ribbon para finalizar.";

            // Snapshot das disciplinas marcadas no momento do clique e dos IDs
            // já selecionados; o handler vai usar esses IDs como pré-seleção
            // do PickObjects, permitindo seleção incremental entre cliques.
            IReadOnlyList<Discipline> disciplines = ViewModel.SelectedDisciplines
                .Select(BatchParameterEditorTypeMapping.ToAdapter)
                .ToList();
            List<ElementId> previous = _selectedIds.ToList();
            _selectionEvent.Raise(this, disciplines, previous);
        }

        private void OnClearSelectionClicked(object sender, RoutedEventArgs e)
        {
            _selectedIds.Clear();
            ViewModel.SelectedCount = 0;
            ViewModel.SelectionCategoriesSummary = string.Empty;
            ViewModel.NoCommonParametersMessage = string.Empty;
            ViewModel.Parameters.Clear();
            _adapterByVm.Clear();
            ViewModel.SelectedParameter = null;
            ViewModel.StatusMessage = "Seleção limpa.";
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

            ParameterOptionViewModel? param = ViewModel.SelectedParameter;
            if (param == null)
            {
                ViewModel.StatusMessage = "Escolha um parâmetro.";
                return;
            }

            if (!_adapterByVm.TryGetValue((param.Name, param.IsInstance), out CommonParameterOption? adapterParam))
            {
                ViewModel.StatusMessage = "Não foi possível resolver o parâmetro selecionado.";
                return;
            }

            if (!string.IsNullOrEmpty(ViewModel.ValidationMessage))
            {
                ViewModel.StatusMessage = ViewModel.ValidationMessage;
                return;
            }

            ViewModel.StatusMessage = "Aplicando…";
            _applyEvent.Raise(this, snapshot, adapterParam, ViewModel.Value ?? string.Empty);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _selectedIds.Clear();
            _adapterByVm.Clear();
        }

        private static string BuildShortStatus(ParameterApplyResult result)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
                return $"Erro: {result.ErrorMessage}";

            int total = result.Updated + result.UpdatedNested;
            if (total == 0 && result.Failed == 0)
                return $"Nenhum elemento foi alterado para '{result.ParameterName}'.";

            return result.Failed > 0
                ? $"'{result.ParameterName}' atualizado em {total} elemento(s); {result.Failed} falha(s)."
                : $"'{result.ParameterName}' atualizado em {total} elemento(s).";
        }

        private static void ShowResultDialog(ParameterApplyResult result)
        {
            TaskDialog dlg = new("EVT-BIM — Editor de parâmetros");

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                dlg.MainIcon = TaskDialogIcon.TaskDialogIconError;
                dlg.MainInstruction = "Não foi possível aplicar o parâmetro.";
                dlg.MainContent = result.ErrorMessage;
                dlg.CommonButtons = TaskDialogCommonButtons.Close;
                dlg.Show();
                return;
            }

            int total = result.Updated + result.UpdatedNested;

            if (total == 0 && result.Failed == 0)
            {
                dlg.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                dlg.MainInstruction = "Nenhum elemento foi alterado.";
                dlg.MainContent =
                    $"O parâmetro \"{result.ParameterName}\" não foi atualizado em nenhum dos " +
                    $"{result.TopLevelCount} elemento(s) selecionado(s) nem nas famílias aninhadas.";
                dlg.ExpandedContent = BuildExpandedContent(result);
                dlg.CommonButtons = TaskDialogCommonButtons.Close;
                dlg.Show();
                return;
            }

            dlg.MainIcon = result.Failed > 0
                ? TaskDialogIcon.TaskDialogIconWarning
                : TaskDialogIcon.TaskDialogIconInformation;

            dlg.MainInstruction = result.Failed > 0
                ? $"Parâmetro aplicado com {result.Failed} falha(s)."
                : "Parâmetro aplicado com sucesso.";

            StringBuilder content = new();
            content.AppendLine($"Parâmetro: \"{result.ParameterName}\"");
            content.AppendLine($"Valor: \"{result.Value}\"");
            content.AppendLine();
            content.AppendLine(
                $"Elementos selecionados atualizados: {result.Updated} de {result.TopLevelCount}");
            if (result.UpdatedNested > 0)
                content.AppendLine($"Famílias aninhadas atualizadas: {result.UpdatedNested}");
            if (result.Failed > 0)
                content.AppendLine($"Falhas ao aplicar o valor: {result.Failed}");
            dlg.MainContent = content.ToString().TrimEnd();
            dlg.ExpandedContent = BuildExpandedContent(result);
            dlg.CommonButtons = TaskDialogCommonButtons.Close;
            dlg.Show();
        }

        private static string BuildExpandedContent(ParameterApplyResult result)
        {
            StringBuilder sb = new();
            sb.AppendLine("Detalhes:");
            sb.AppendLine($"• {result.TopLevelCount} elemento(s) selecionado(s) (raiz).");
            sb.AppendLine($"• {result.Updated} alterado(s) na seleção principal.");
            sb.AppendLine($"• {result.UpdatedNested} alterado(s) em famílias aninhadas.");

            if (result.SkippedMissing > 0)
                sb.AppendLine(
                    $"• {result.SkippedMissing} família(s) aninhada(s) ignorada(s) " +
                    "(não possuem o parâmetro — comportamento esperado).");

            if (result.SkippedReadOnly > 0)
                sb.AppendLine(
                    $"• {result.SkippedReadOnly} parâmetro(s) ignorado(s) por estarem " +
                    "marcados como somente-leitura.");

            if (result.Failed > 0)
                sb.AppendLine(
                    $"• {result.Failed} falha(s) — o Revit recusou o valor (verifique a unidade ou o tipo).");

            return sb.ToString().TrimEnd();
        }
    }
}
