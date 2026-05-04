using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Tools;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Revit.Hosting.Commands;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.TigreCodes
{
    /// <summary>
    /// Composition glue for the "Códigos Tigre" tool: builds the Revit-side
    /// service (which needs the active <c>Document</c>) and the
    /// Application-side use case, runs the use case and reports the outcome
    /// through a <see cref="ToolResult"/>. The Revit <c>TaskDialog</c> is
    /// shown by <see cref="RevitCommandBase{TTool}"/> based on the message.
    /// </summary>
    public sealed class ApplyTigreCodesTool : IRevitDocumentTool
    {
        private readonly ITigreCatalogProvider _catalogProvider;

        public ApplyTigreCodesTool(ITigreCatalogProvider catalogProvider)
        {
            _catalogProvider = catalogProvider;
        }

        public ToolResult Execute(Document document)
        {
            ITigreCodeApplyService service = new TigreCodeApplier(document, _catalogProvider);
            ApplyTigreCodesUseCase useCase = new ApplyTigreCodesUseCase(service);

            TigreCodeApplyResult report = useCase.Execute();

            return ToolResult.Ok(ApplyTigreCodesUseCase.FormatReport(report));
        }
    }
}
