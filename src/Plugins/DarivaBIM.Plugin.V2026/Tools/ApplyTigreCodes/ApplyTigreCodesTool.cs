using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Revit.Adapters.V2026.Writers;

namespace DarivaBIM.Plugin.V2026.Tools.ApplyTigreCodes
{
    /// <summary>
    /// Composition glue for the "Códigos Tigre" tool: builds the Revit-side
    /// service (which needs the active <c>Document</c>) and the
    /// Application-side use case, then runs the use case. Lives in the Plugin
    /// (not in Application) because it has to bind a <c>Document</c> at the
    /// IExternalCommand boundary.
    /// </summary>
    internal sealed class ApplyTigreCodesTool
    {
        private readonly ITigreCatalogProvider _catalogProvider;

        public ApplyTigreCodesTool(ITigreCatalogProvider catalogProvider)
        {
            _catalogProvider = catalogProvider;
        }

        public TigreCodeApplyResult Execute(Document document)
        {
            ITigreCodeApplyService service = new TigreCodeApplier(document, _catalogProvider);
            ApplyTigreCodesUseCase useCase = new ApplyTigreCodesUseCase(service);
            return useCase.Execute();
        }
    }
}
