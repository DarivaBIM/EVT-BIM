using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;

namespace DarivaBIM.Plugin.Features.ParameterEditor
{
    [Transaction(TransactionMode.Manual)]
    public class ShowParameterEditorCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = App.Executor.Execute(commandData, ref outerMessage, _ =>
            {
                ParameterEditorWindow.ShowSingleton();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
