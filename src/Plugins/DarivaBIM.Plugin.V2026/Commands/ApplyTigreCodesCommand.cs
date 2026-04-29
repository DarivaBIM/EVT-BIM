using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.V2026.Commands
{
    /// <summary>
    /// Thin <c>IExternalCommand</c> shell. Resolves the use case from the DI
    /// scope built by <see cref="RevitCommandExecutor"/> and translates the
    /// outcome to a Revit <see cref="Result"/>.
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

                ITigreCatalogProvider catalogProvider = (ITigreCatalogProvider)ctx.Services.GetService(typeof(ITigreCatalogProvider))!;
                ITigreCodeApplyService service = new TigreCodeApplier(doc, catalogProvider);
                ApplyTigreCodesUseCase useCase = new ApplyTigreCodesUseCase(service);

                try
                {
                    TigreCodeApplyResult report = useCase.Execute();
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
