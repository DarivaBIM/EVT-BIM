using DarivaBIM.Plugin.V2026.Features.FamiliesImporter;
using DarivaBIM.Plugin.V2026.Features.ParameterEditor;
using DarivaBIM.Plugin.V2026.Features.PipeCadMapper;
using DarivaBIM.Plugin.V2026.Features.Prolongador;
using DarivaBIM.Plugin.V2026.Features.TigreCodes;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon.Panels
{
    public static class TigrePanelDefinition
    {
        public const string Name = "EVT-BIM";

        public static RibbonPanelDefinition Build()
        {
            return new RibbonPanelDefinition(Name, new[]
            {
                FamiliesImporterFeature.Button,
                PipeCadMapperFeature.Button,
                TigreCodesFeature.Button,
                ProlongadorFeature.Button,
                ParameterEditorFeature.Button
            });
        }
    }
}
