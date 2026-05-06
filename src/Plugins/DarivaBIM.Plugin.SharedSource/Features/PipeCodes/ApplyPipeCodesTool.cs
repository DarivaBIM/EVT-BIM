using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Tools;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Revit.Hosting.Commands;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Composition glue for the "Codificar Tubos" tool: builds the Revit-side
    /// Tigre service (which needs the active <c>Document</c>) and the
    /// Application-side use case, runs the use case and reports the outcome
    /// through a <see cref="ToolResult"/>. The Revit <c>TaskDialog</c> is
    /// shown by <see cref="RevitCommandBase{TTool}"/> based on the message.
    /// Tigre is currently the only catalogue supported; a future expansion
    /// would dispatch to additional catalogues from this entry-point.
    /// </summary>
    public sealed class ApplyPipeCodesTool : IRevitDocumentTool
    {
        private readonly ITigreCatalogProvider _catalogProvider;

        public ApplyPipeCodesTool(ITigreCatalogProvider catalogProvider)
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
