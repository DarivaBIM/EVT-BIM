using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowFamiliesPaneCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                DockablePaneId paneId = new DockablePaneId(PaneIds.FamiliesPaneId);
                DockablePane pane = commandData.Application.GetDockablePane(paneId);
                pane.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FamiliesImporterHub",
                    $"Erro ao abrir o painel de famílias:\n{ex.Message}");

                return Result.Failed;
            }
        }
    }
}