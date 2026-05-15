using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Infrastructure.Persistence.Preferences;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.FamiliesImporter
{
    /// <summary>
    /// Abre (ou foca, se já estiver aberta) a janela modeless de Importar
    /// Famílias. Migrado de <c>DockablePane</c> para <see cref="FamiliesWindow"/>
    /// — DockablePane do Revit 2025+ tem regressão que congela a UI após
    /// placement de família.
    ///
    /// Single-instance: a referência da janela viva é mantida em campo
    /// estático. Click no botão da ribbon enquanto a janela já está aberta
    /// apenas ativa/restaura — não cria uma segunda instância. Quando a
    /// janela fecha (X, ALT+F4, Revit fechando), o evento Closed limpa a
    /// referência para que o próximo click crie uma nova.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowFamiliesPaneCommand : IExternalCommand
    {
        private static FamiliesWindow? _instance;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                // Já existe instância viva — restaura se estava minimizada
                // e traz pro topo. Não cria nova.
                if (_instance != null)
                {
                    if (_instance.WindowState == WindowState.Minimized)
                    {
                        _instance.WindowState = WindowState.Normal;
                    }

                    _instance.Activate();
                    return Result.Succeeded;
                }

                // Cria nova instância. Owner é a janela principal do Revit
                // — comportamento de filha (minimiza/fecha junto, fica
                // acima do Revit, não aparece na taskbar).
                FamiliesWindow window = new FamiliesWindow(
                    commandData.Application.MainWindowHandle,
                    new FamilyPreferencesService());

                _instance = window;
                window.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_instance, window))
                    {
                        _instance = null;
                    }
                };

                window.Show();
                return Result.Succeeded;
            });

            message = outerMessage;
            return result;
        }
    }
}
