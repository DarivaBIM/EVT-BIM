using DarivaBIM.Plugin.Features.BatchParameterEditor;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Features.FloorDrainExtension;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Plugin.Features.PipeCodes;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Ribbon.Panels
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
                PipeCodesFeature.Button,
                FloorDrainExtensionFeature.Button,
                BatchParameterEditorFeature.Button
            });
        }
    }
}
