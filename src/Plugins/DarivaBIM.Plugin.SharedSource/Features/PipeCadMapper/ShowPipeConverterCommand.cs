using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    [Transaction(TransactionMode.Manual)]
    public class ShowPipeConverterCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                PipeConverterWindow.ShowSingleton();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
