using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamiliesImporterHub.Infrastructure;

namespace FamiliesImporterHub.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ApplyTigreCodesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    TaskDialog.Show("TigreBIM", "Abra um projeto Revit para aplicar os códigos Tigre.");
                    return Result.Cancelled;
                }

                TigreCodeApplyResult report = TigreCodeApplier.Run(uiDoc.Document);
                TaskDialog.Show("TigreBIM — Códigos Tigre", TigreCodeApplier.FormatReport(report));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "TigreBIM",
                    $"Erro ao aplicar códigos Tigre:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
