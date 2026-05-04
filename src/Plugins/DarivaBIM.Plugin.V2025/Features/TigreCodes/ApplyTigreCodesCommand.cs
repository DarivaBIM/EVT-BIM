using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.V2025.Features.TigreCodes
{
    /// <summary>
    /// Thin <c>IExternalCommand</c> shell. Validates the active document,
    /// resolves <see cref="ApplyTigreCodesTool"/> from the DI scope and
    /// delegates the run to it.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ApplyTigreCodesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            string outerMessage = message;
            Result result = App.Executor.Execute(commandData, ref outerMessage, ctx =>
            {
                Document? doc = (ctx.Document as RevitDocumentContext)?.RevitDocument;
                if (doc == null || doc.IsFamilyDocument)
                {
                    TaskDialog.Show("EVT-BIM", "Abra um projeto Revit para aplicar os códigos Tigre.");
                    return Result.Cancelled;
                }

                ApplyTigreCodesTool tool =
                    (ApplyTigreCodesTool)ctx.Services.GetService(typeof(ApplyTigreCodesTool))!;

                try
                {
                    return tool.Execute(doc);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("EVT-BIM", $"Erro ao aplicar códigos Tigre:\n{ex.Message}");
                    return Result.Failed;
                }
            });
            message = outerMessage;
            return result;
        }
    }
}
