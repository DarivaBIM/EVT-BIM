using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Plugin.Features.PipeCodes;
using DarivaBIM.Presentation.Wpf.PipeCodes;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Janela "Codificar Tubos" — dispara ExternalEvents para varrer o
    /// projeto, criar o shared parameter Tigre: Código e inserir/apagar o
    /// valor nos tubos selecionados pelo usuário. Toda interação com a
    /// RevitAPI passa pelos handlers; o code-behind apenas atualiza o
    /// view model e mostra os TaskDialogs com o resumo.
    /// </summary>
    public partial class PipeCodesWindow : Window
    {
        private static PipeCodesWindow? _instance;

        private readonly PipeCodesScanExternalEvent _scanEvent;
        private readonly PipeCodesApplyExternalEvent _applyEvent;
        private readonly PipeCodesClearExternalEvent _clearEvent;
        private readonly PipeCodesEnsureParameterExternalEvent _ensureEvent;

        // Slice 4.3.A F1 ampliado — prefilter ativo na sessão atual da
        // janela. Quando setado (via ShowSingleton(ids)), todo Raise do
        // _scanEvent leva os IDs. Null = varredura completa.
        private IReadOnlyCollection<long>? _prefilterIds;

        public PipeCodesViewModel ViewModel { get; }

        public PipeCodesWindow()
        {
            InitializeComponent();
            ViewModel = new PipeCodesViewModel();
            DataContext = ViewModel;

            _scanEvent = new PipeCodesScanExternalEvent();
            _applyEvent = new PipeCodesApplyExternalEvent();
            _clearEvent = new PipeCodesClearExternalEvent();
            _ensureEvent = new PipeCodesEnsureParameterExternalEvent();

            SourceInitialized += (_, _) => WindowChromeHelper.DisableMinimize(this);
            StateChanged += OnWindowStateChanged;
            Loaded += (_, _) => RaiseScan("Lendo tubos do projeto…");
        }

        // Backstop pra ALT+Space → Minimize e Win+Down: o P/Invoke já cobre o
        // botão do chrome, mas atalhos de teclado ainda passam.
        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        }

        public static PipeCodesWindow ShowSingleton()
        {
            return ShowSingleton(prefilterIds: null);
        }

        /// <summary>
        /// Singleton com prefilter opcional de IDs — vindo do "Corrigir
        /// agora" do Tigre Quantifica (Slice 4.3.A F1 ampliado).
        /// <c>null</c> ou lista vazia = varredura completa (comportamento
        /// histórico). Quando preenchido, dispara re-scan filtrado mesmo
        /// se a janela já estava aberta.
        /// </summary>
        public static PipeCodesWindow ShowSingleton(IReadOnlyCollection<long>? prefilterIds)
        {
            bool isNew = _instance == null;
            if (_instance == null)
            {
                _instance = new PipeCodesWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            // Snapshot defensivo + propaga pra próxima varredura.
            _instance._prefilterIds = prefilterIds != null && prefilterIds.Count > 0
                ? prefilterIds.ToArray()
                : null;

            int filteredCount = _instance._prefilterIds?.Count ?? 0;
            _instance.ViewModel.SetFilterState(filteredCount);
            if (filteredCount == 0)
                _instance.ViewModel.ClearFilterState();

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();

            // Se a janela já estava aberta, force re-scan com o prefilter
            // novo (Loaded só dispara em janelas novas — singleton já
            // existente não re-loadea).
            if (!isNew)
                _instance.RaiseScan(filteredCount > 0
                    ? $"Filtrando {filteredCount} elemento(s) do finding..."
                    : "Re-lendo elementos do projeto...");

            return _instance;
        }

        // ---------------- Atualizações vindas dos ExternalEvents ----------------

        public void NotifyScanCompleted(TigreScanResult result)
        {
            RunOnUi(() =>
            {
                ViewModel.ApplyScan(result);
                ViewModel.IsBusy = false;

                // ApplyScan já chama RefreshContextualStatus quando não há
                // erro; aqui só sobrescrevemos com a mensagem fatal de erro.
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    ViewModel.StatusMessage = result.ErrorMessage!;
            });
        }

        public void NotifyApplyCompleted(TigreSelectiveApplyResult result)
        {
            RunOnUi(() =>
            {
                ShowApplyDialog(result);
                ViewModel.StatusMessage = BuildApplyShortStatus(result);
                // Re-scan para refletir as mudanças nas caixinhas.
                RaiseScan(busyMessage: "Atualizando lista após aplicação…");
            });
        }

        public void NotifyClearCompleted(TigreClearResult result)
        {
            RunOnUi(() =>
            {
                ShowClearDialog(result);
                ViewModel.StatusMessage = BuildClearShortStatus(result);
                RaiseScan(busyMessage: "Atualizando lista após limpeza…");
            });
        }

        public void NotifyEnsureParameterCompleted(TigreEnsureParameterResult result)
        {
            RunOnUi(() =>
            {
                ShowEnsureDialog(result);
                ViewModel.StatusMessage = string.IsNullOrEmpty(result.ErrorMessage)
                    ? "Parâmetro 'Tigre: Código' criado/atualizado."
                    : $"Erro ao criar o parâmetro: {result.ErrorMessage}";
                RaiseScan(busyMessage: "Atualizando lista após criação do parâmetro…");
            });
        }

        // ---------------- Handlers de UI ----------------

        private void OnRescanClicked(object sender, RoutedEventArgs e)
        {
            RaiseScan("Re-lendo tubos do projeto…");
        }

        // Slice 4.3.A F1 ampliado — limpa o prefilter ativo e re-escaneia
        // o projeto inteiro.
        private void OnClearFilterClicked(object sender, RoutedEventArgs e)
        {
            _prefilterIds = null;
            ViewModel.ClearFilterState();
            RaiseScan("Removendo filtro e re-lendo o projeto…");
        }

        private void OnEnsureParameterClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = "Criando/atualizando o parâmetro 'Tigre: Código' nos tubos…";
            _ensureEvent.Raise(this);
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<long> ids = ViewModel.CollectSelectedIds();
            if (ids.Count == 0)
            {
                ViewModel.StatusMessage = "Marque pelo menos um tubo antes de aplicar.";
                return;
            }

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = $"Aplicando códigos em {ids.Count} tubo(s)…";
            _applyEvent.Raise(this, ids);
        }

        private void OnClearClicked(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<long> ids = ViewModel.CollectSelectedIds();
            if (ids.Count == 0)
            {
                ViewModel.StatusMessage = "Marque pelo menos um tubo antes de apagar.";
                return;
            }

            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = $"Apagando códigos em {ids.Count} tubo(s)…";
            _clearEvent.Raise(this, ids);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Nada a persistir hoje — listas e seleção morrem com a janela.
        }

        // ---------------- Helpers ----------------

        private void RaiseScan(string busyMessage)
        {
            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = busyMessage;
            _scanEvent.Raise(this, _prefilterIds);
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess())
                action();
            else
                Dispatcher.Invoke(action);
        }

        private void ShowApplyDialog(TigreSelectiveApplyResult r)
        {
            TaskDialog dlg = new("EVT-BIM — Codificar Tubos");

            if (!string.IsNullOrEmpty(r.ErrorMessage))
            {
                dlg.MainIcon = TaskDialogIcon.TaskDialogIconError;
                dlg.MainInstruction = "Não foi possível aplicar os códigos.";
                dlg.MainContent = r.ErrorMessage;
                dlg.CommonButtons = TaskDialogCommonButtons.Close;
                dlg.Show();
                return;
            }

            int touched = r.Inserted + r.Overwritten;
            dlg.MainIcon = r.NoMatch + r.ParameterIssue > 0
                ? TaskDialogIcon.TaskDialogIconWarning
                : TaskDialogIcon.TaskDialogIconInformation;

            dlg.MainInstruction = touched > 0
                ? $"Códigos aplicados em {touched} tubo(s)."
                : "Nenhum tubo precisou ser alterado.";

            StringBuilder sb = new();
            sb.AppendLine($"Catálogo Tigre: {r.CatalogCount} item(ns)");
            sb.AppendLine($"Tubos no projeto: {r.PipesTotalInProject}");
            sb.AppendLine();
            sb.AppendLine($"Marcados no WPF: {r.Selected}");
            sb.AppendLine($"  · Códigos inseridos (estavam vazios): {r.Inserted}");
            sb.AppendLine($"  · Códigos sobrescritos (estavam divergentes): {r.Overwritten}");
            sb.AppendLine($"  · Já estavam corretos: {r.AlreadyOk}");
            sb.AppendLine($"  · Sem correspondência no catálogo: {r.NoMatch}");
            sb.AppendLine($"  · Sem parâmetro acessível: {r.ParameterIssue}");
            dlg.MainContent = sb.ToString().TrimEnd();
            dlg.CommonButtons = TaskDialogCommonButtons.Close;
            dlg.Show();
        }

        private void ShowClearDialog(TigreClearResult r)
        {
            TaskDialog dlg = new("EVT-BIM — Codificar Tubos");

            if (!string.IsNullOrEmpty(r.ErrorMessage))
            {
                dlg.MainIcon = TaskDialogIcon.TaskDialogIconError;
                dlg.MainInstruction = "Não foi possível apagar os códigos.";
                dlg.MainContent = r.ErrorMessage;
                dlg.CommonButtons = TaskDialogCommonButtons.Close;
                dlg.Show();
                return;
            }

            dlg.MainIcon = r.ParameterIssue > 0
                ? TaskDialogIcon.TaskDialogIconWarning
                : TaskDialogIcon.TaskDialogIconInformation;

            dlg.MainInstruction = r.Cleared > 0
                ? $"Códigos apagados em {r.Cleared} tubo(s)."
                : "Nenhum código foi apagado.";

            StringBuilder sb = new();
            sb.AppendLine($"Marcados no WPF: {r.Selected}");
            sb.AppendLine($"  · Apagados: {r.Cleared}");
            sb.AppendLine($"  · Já estavam vazios: {r.AlreadyEmpty}");
            sb.AppendLine($"  · Sem parâmetro acessível: {r.ParameterIssue}");
            dlg.MainContent = sb.ToString().TrimEnd();
            dlg.CommonButtons = TaskDialogCommonButtons.Close;
            dlg.Show();
        }

        private void ShowEnsureDialog(TigreEnsureParameterResult r)
        {
            TaskDialog dlg = new("EVT-BIM — Codificar Tubos");

            if (!string.IsNullOrEmpty(r.ErrorMessage))
            {
                dlg.MainIcon = TaskDialogIcon.TaskDialogIconError;
                dlg.MainInstruction = "Não foi possível criar o parâmetro 'Tigre: Código'.";
                dlg.MainContent = r.ErrorMessage;
                dlg.CommonButtons = TaskDialogCommonButtons.Close;
                dlg.Show();
                return;
            }

            dlg.MainIcon = r.Warnings.Count > 0
                ? TaskDialogIcon.TaskDialogIconWarning
                : TaskDialogIcon.TaskDialogIconInformation;
            dlg.MainInstruction = "Parâmetro 'Tigre: Código' está pronto nos tubos.";

            StringBuilder sb = new();
            sb.AppendLine(r.Action);
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Avisos:");
                foreach (string w in r.Warnings)
                    sb.AppendLine("  · " + w);
            }
            dlg.MainContent = sb.ToString().TrimEnd();
            dlg.CommonButtons = TaskDialogCommonButtons.Close;
            dlg.Show();
        }

        private static string BuildApplyShortStatus(TigreSelectiveApplyResult r)
        {
            int touched = r.Inserted + r.Overwritten;
            return touched > 0
                ? $"Aplicação concluída: {touched} tubo(s) atualizados."
                : "Aplicação concluída — nenhum tubo precisou ser alterado.";
        }

        private static string BuildClearShortStatus(TigreClearResult r)
        {
            return r.Cleared > 0
                ? $"Limpeza concluída: {r.Cleared} tubo(s) zerados."
                : "Limpeza concluída — nenhum tubo precisou ser zerado.";
        }
    }
}
