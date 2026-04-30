using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Revit.Adapters.V2026.Features.TigreCodes;

namespace DarivaBIM.Plugin.V2026.Features.TigreCodes
{
    /// <summary>
    /// Composition glue for the "Códigos Tigre" tool: builds the Revit-side
    /// service (which needs the active <c>Document</c>) and the
    /// Application-side use case, runs the use case and reports the outcome to
    /// the user. Lives in the Plugin (not in Application) because it binds a
    /// <c>Document</c> and shows a Revit <c>TaskDialog</c>.
    /// </summary>
    public sealed class ApplyTigreCodesTool
    {
        private readonly ITigreCatalogProvider _catalogProvider;

        public ApplyTigreCodesTool(ITigreCatalogProvider catalogProvider)
        {
            _catalogProvider = catalogProvider;
        }

        public Result Execute(Document document)
        {
            ITigreCodeApplyService service = new TigreCodeApplier(document, _catalogProvider);
            ApplyTigreCodesUseCase useCase = new ApplyTigreCodesUseCase(service);

            TigreCodeApplyResult report = useCase.Execute();

            TaskDialog.Show(
                "EVT-BIM — Códigos Tigre",
                ApplyTigreCodesUseCase.FormatReport(report));

            return Result.Succeeded;
        }
    }
}
