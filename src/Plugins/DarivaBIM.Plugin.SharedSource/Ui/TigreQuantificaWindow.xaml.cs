using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Application.Services.Quantifica;
using DarivaBIM.Plugin.Features.TigreQuantifica;
using DarivaBIM.Presentation.Wpf.TigreQuantifica;
using Microsoft.Win32;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Janela "Tigre Quantifica" — dispara um <c>ExternalEvent</c> de scan
    /// no Loaded e exporta CSV via <c>File.WriteAllText</c> síncrono no
    /// code-behind (export NÃO é transação Revit, então não passa por
    /// ExternalEvent). Singleton: ShowSingleton garante uma instância só
    /// por sessão do Revit.
    ///
    /// Slice 4.3.B F4 — ganha <see cref="UpdateProjectInfoExternalEvent"/>
    /// pra escrever Cliente/Autor em <c>ProjectInformation</c>. Após save
    /// bem-sucedido dispara um re-scan pra refresh do snapshot + audit.
    /// </summary>
    public partial class TigreQuantificaWindow : Window
    {
        private static TigreQuantificaWindow? _instance;

        private readonly QuantityScanExternalEvent _scanEvent;
        private readonly SelectElementsExternalEvent _selectEvent;
        private readonly UpdateProjectInfoExternalEvent _updateProjectInfoEvent;
        private QuantitySnapshot? _lastSnapshot;

        public TigreQuantificaViewModel ViewModel { get; }

        public TigreQuantificaWindow()
        {
            InitializeComponent();
            ViewModel = new TigreQuantificaViewModel();
            DataContext = ViewModel;

            _scanEvent = new QuantityScanExternalEvent();
            _selectEvent = new SelectElementsExternalEvent();

            // Slice 4.3.B F4 — handler do save de Cliente/Autor. O callback
            // de sucesso re-roda o scan pra refresh do snapshot (e do
            // finding de auditoria); o de erro escreve no StatusMessage do
            // VM (não TaskDialog) pra não bloquear a janela.
            _updateProjectInfoEvent = new UpdateProjectInfoExternalEvent(
                onError: msg => RunOnUi(() => ViewModel.StatusMessage = msg),
                onSuccess: () => RunOnUi(() => RaiseScan("Atualizando após edição…")));

            // Slice 4.3.A F2 — callback de seleção setado ANTES do
            // primeiro ApplyScan; cada AuditFindingViewModel construído
            // no ApplyScan já recebe o callback via TigreQuantificaViewModel.
            ViewModel.SelectInRevitCallback = ids => _selectEvent.Raise(ids);

            // Slice 4.3.A F1 ampliado — callback de "Corrigir agora"
            // abre PipeCodesWindow pré-filtrado nos IDs do finding.
            // PipeCodesWindow.ShowSingleton aceita prefilterIds opcional.
            ViewModel.CorrigirAgoraCallback = ids => PipeCodesWindow.ShowSingleton(ids);

            // Slice 4.3.B F4 — callback de save dispara o ExternalEvent
            // que escreve em ProjectInformation.ClientName/Author.
            ViewModel.SaveProjectInfoCallback = dto => _updateProjectInfoEvent.Raise(dto);

            Loaded += (_, _) => RaiseScan("Lendo elementos do projeto…");
        }

        public static TigreQuantificaWindow ShowSingleton()
        {
            if (_instance == null)
            {
                _instance = new TigreQuantificaWindow();
                _instance.Closed += (_, _) => _instance = null;
            }

            if (!_instance.IsVisible)
                _instance.Show();

            _instance.Activate();
            return _instance;
        }

        // ---------------- Callback do ExternalEvent ----------------

        public void NotifyScanCompleted(QuantitySnapshot snapshot)
        {
            RunOnUi(() =>
            {
                _lastSnapshot = snapshot;
                ViewModel.IsBusy = false;
                ViewModel.ApplyScan(snapshot);
                ViewModel.StatusMessage = BuildPostScanStatus(snapshot);
            });
        }

        // ---------------- Handlers de UI ----------------

        private void OnRescanClicked(object sender, RoutedEventArgs e)
        {
            RaiseScan("Re-lendo elementos do projeto…");
        }

        private void OnExportClicked(object sender, RoutedEventArgs e)
        {
            if (_lastSnapshot == null || !string.IsNullOrWhiteSpace(_lastSnapshot.ErrorMessage))
            {
                ViewModel.StatusMessage = "Faça uma varredura antes de exportar.";
                return;
            }

            SaveFileDialog dlg = new()
            {
                Title = "Exportar relatório Tigre Quantifica",
                Filter = "Planilha CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
                FileName = BuildSuggestedFileName(_lastSnapshot.ProjectInfo),
                AddExtension = true,
                DefaultExt = ".csv",
                OverwritePrompt = true,
            };

            bool? ok = dlg.ShowDialog(this);
            if (ok != true)
                return;

            try
            {
                string csv = QuantityCsvWriter.Write(_lastSnapshot);
                // BOM já está embutido na string pelo writer; usar
                // UTF8Encoding(false) evita o BOM adicional do encoder.
                File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(false));
                ViewModel.StatusMessage = $"Relatório salvo em: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Falha ao salvar: {ex.Message}";
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Nada a persistir hoje — snapshot some com a janela.
        }

        // ---------------- Handlers do FindingRowTemplate (hotfix 4.3.A.1) ----------------

        /// <summary>
        /// Clique na linha do finding (qualquer área que NÃO seja o botão
        /// "Corrigir agora" filho) dispara seleção dos elementos no Revit.
        /// Substitui o command binding antigo que sofria com bubble do
        /// botão interno → crash fatal.
        /// </summary>
        private void OnFindingRowMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not AuditFindingViewModel vm) return;
            if (!vm.CanSelectInRevit) return;
            if (!vm.SelectInRevitCommand.CanExecute(null)) return;

            vm.SelectInRevitCommand.Execute(null);
            e.Handled = true;
        }

        /// <summary>
        /// Click do mini-botão "Corrigir agora". Disparamos via code-behind
        /// (não via Command no XAML) pra garantir que e.Handled=true CORTE
        /// o bubble do MouseLeftButtonUp pro Border pai — caso contrário,
        /// o click ativaria tanto SelectInRevit quanto CorrigirAgora no
        /// mesmo instante (race de ExternalEvents → crash fatal Revit).
        /// </summary>
        private void OnCorrigirAgoraClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not AuditFindingViewModel vm) return;
            if (!vm.CanCorrigirAgora) return;
            if (!vm.CorrigirAgoraCommand.CanExecute(null)) return;

            vm.CorrigirAgoraCommand.Execute(null);
        }

        // ---------------- Slice 4.3.B F3 — busca ----------------

        /// <summary>
        /// Botão (X) limpa o campo de busca. Mantém foco no TextBox pra
        /// usuário continuar digitando uma nova busca sem clicar de volta.
        /// </summary>
        private void OnClearSearchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SearchText = string.Empty;
            // Best-effort: localizar o TextBox de busca pelo nome e focar.
            if (FindName("SearchBox") is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        // ---------------- Slice 4.3.B F4 — edição inline Cliente/Autor ----------------

        /// <summary>
        /// LostFocus do TextBox editável dispara o save (se dirty). Esse
        /// é o comportamento padrão de TextBox.UpdateSourceTrigger no
        /// binding TwoWay; aqui acionamos o command que faz o Raise no
        /// ExternalEvent.
        /// </summary>
        private void OnProjectFieldLostFocus(object sender, RoutedEventArgs e)
        {
            TrySaveProjectInfo();
        }

        /// <summary>
        /// Enter no TextBox força commit do binding + save imediato. Esc
        /// reverte pro valor original do snapshot (descarta dirty).
        /// </summary>
        private void OnProjectFieldKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox tb)
                {
                    // Força commit do binding antes do save (UpdateSourceTrigger
                    // default em TextBox é LostFocus — Enter sem este
                    // GetBindingExpression().UpdateSource() não chega no VM).
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                TrySaveProjectInfo();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Restaura snapshot atual no TextBox sem disparar save.
                // O setter do VM cuida do binding TwoWay; precisamos
                // re-bater os valores via ProjectInfo (clearra IsDirty).
                if (sender is TextBox tb)
                {
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                }
                e.Handled = true;
            }
        }

        private void TrySaveProjectInfo()
        {
            if (ViewModel.SaveProjectInfoCommand.CanExecute(null))
                ViewModel.SaveProjectInfoCommand.Execute(null);
        }

        // ---------------- Helpers ----------------

        private void RaiseScan(string busyMessage)
        {
            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = busyMessage;
            _scanEvent.Raise(this);
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess())
                action();
            else
                Dispatcher.Invoke(action);
        }

        private static string BuildPostScanStatus(QuantitySnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
                return snapshot.ErrorMessage!;

            int totalGroups = snapshot.Groups?.Count ?? 0;
            int totalFindings = snapshot.AuditFindings?.Count ?? 0;
            return totalGroups == 0
                ? "Varredura concluída — nenhum elemento encontrado nas categorias mapeadas."
                : $"Varredura concluída — {totalGroups} grupo(s), {totalFindings} achado(s) de auditoria.";
        }

        private static string BuildSuggestedFileName(ProjectInfoDto? info)
        {
            string baseName = "TigreQuantifica";
            if (info != null && !string.IsNullOrWhiteSpace(info.Name) && info.Name != "(não preenchido)")
            {
                string safe = string.Join("_", info.Name.Trim().Split(Path.GetInvalidFileNameChars()));
                if (safe.Length > 0)
                    baseName = baseName + "_" + safe;
            }
            return baseName + ".csv";
        }
    }
}
