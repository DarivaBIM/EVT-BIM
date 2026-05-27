using System;
using System.IO;
using System.Text;
using System.Windows;
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
    /// </summary>
    public partial class TigreQuantificaWindow : Window
    {
        private static TigreQuantificaWindow? _instance;

        private readonly QuantityScanExternalEvent _scanEvent;
        private readonly SelectElementsExternalEvent _selectEvent;
        private QuantitySnapshot? _lastSnapshot;

        public TigreQuantificaViewModel ViewModel { get; }

        public TigreQuantificaWindow()
        {
            InitializeComponent();
            ViewModel = new TigreQuantificaViewModel();
            DataContext = ViewModel;

            _scanEvent = new QuantityScanExternalEvent();
            _selectEvent = new SelectElementsExternalEvent();

            // Slice 4.3.A F2 — callback de seleção setado ANTES do
            // primeiro ApplyScan; cada AuditFindingViewModel construído
            // no ApplyScan já recebe o callback via TigreQuantificaViewModel.
            ViewModel.SelectInRevitCallback = ids => _selectEvent.Raise(ids);

            // Slice 4.3.A F1 ampliado — callback de "Corrigir agora"
            // abre PipeCodesWindow pré-filtrado nos IDs do finding.
            // PipeCodesWindow.ShowSingleton aceita prefilterIds opcional.
            ViewModel.CorrigirAgoraCallback = ids => PipeCodesWindow.ShowSingleton(ids);

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
