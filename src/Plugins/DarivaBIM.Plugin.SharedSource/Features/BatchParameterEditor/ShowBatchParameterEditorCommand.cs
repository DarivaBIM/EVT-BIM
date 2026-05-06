using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.BatchParameterEditor
{
    [Transaction(TransactionMode.Manual)]
    public class ShowBatchParameterEditorCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = RevitCommandExecutor.Current!.Execute(commandData, ref outerMessage, _ =>
            {
                BatchParameterEditorWindow.ShowSingleton();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
