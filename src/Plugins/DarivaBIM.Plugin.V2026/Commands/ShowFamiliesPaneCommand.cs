using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DarivaBIM.Plugin.V2026.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowFamiliesPaneCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = App.Executor.Execute(commandData, ref outerMessage, _ =>
            {
                DockablePaneId paneId = new DockablePaneId(PaneIds.FamiliesPaneId);
                DockablePane pane = commandData.Application.GetDockablePane(paneId);
                pane.Show();
                return Result.Succeeded;
            });
            message = outerMessage;
            return result;
        }
    }
}
