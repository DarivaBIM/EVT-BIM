using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Plugin.V2026.Tools.ApplyTigreCodes;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.V2026.Commands
{
    /// <summary>
    /// Thin <c>IExternalCommand</c> shell. Validates the active document,
    /// resolves <see cref="ITigreCatalogProvider"/> from the DI scope and
    /// delegates the run to <see cref="ApplyTigreCodesTool"/>.
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
                    TaskDialog.Show("TigreBIM", "Abra um projeto Revit para aplicar os códigos Tigre.");
                    return Result.Cancelled;
                }

                ITigreCatalogProvider catalogProvider =
                    (ITigreCatalogProvider)ctx.Services.GetService(typeof(ITigreCatalogProvider))!;

                try
                {
                    ApplyTigreCodesTool tool = new ApplyTigreCodesTool(catalogProvider);
                    TigreCodeApplyResult report = tool.Execute(doc);
                    TaskDialog.Show("TigreBIM — Códigos Tigre", ApplyTigreCodesUseCase.FormatReport(report));
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("TigreBIM", $"Erro ao aplicar códigos Tigre:\n{ex.Message}");
                    return Result.Failed;
                }
            });
            message = outerMessage;
            return result;
        }
    }
}
