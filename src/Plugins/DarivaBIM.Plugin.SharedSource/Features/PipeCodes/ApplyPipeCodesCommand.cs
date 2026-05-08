using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Entry-point do botão "Codificar Tubos". Abre a janela WPF como
    /// singleton — toda a interação com o documento (varredura, criação do
    /// shared parameter, inserção e limpeza dos códigos) acontece via
    /// ExternalEvents disparados pela própria janela. Não roda nada no
    /// modelo até o usuário decidir.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ApplyPipeCodesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                PipeCodesWindow.ShowSingleton();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
