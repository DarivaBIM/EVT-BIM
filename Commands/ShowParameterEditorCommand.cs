using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowParameterEditorCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                ParameterEditorWindow.ShowSingleton();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "TigreBIM",
                    $"Erro ao abrir o editor de parâmetros:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
