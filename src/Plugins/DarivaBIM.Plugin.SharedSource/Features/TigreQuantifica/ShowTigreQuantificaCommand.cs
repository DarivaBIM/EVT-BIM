using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// Entry-point do botão "Tigre Quantifica" na ribbon. Abre a janela WPF
    /// como singleton — toda interação com o documento (varredura) acontece
    /// via <c>ExternalEvent</c> disparado pela própria janela. Não roda
    /// nada no modelo até o usuário decidir; o export usa <c>File.WriteAllText</c>
    /// fora de transação.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ShowTigreQuantificaCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                TigreQuantificaWindow.ShowSingleton();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
