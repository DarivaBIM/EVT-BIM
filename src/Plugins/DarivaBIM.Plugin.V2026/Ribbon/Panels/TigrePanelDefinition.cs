using DarivaBIM.Plugin.V2026.Ribbon.Buttons;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon.Panels
{
    public static class TigrePanelDefinition
    {
        public const string Name = "Tigre";

        public static RibbonPanelDefinition Build()
        {
            return new RibbonPanelDefinition(Name, new[]
            {
                FamiliesImporterHubButton.Definition,
                PipeCadMapperButton.Definition,
                TigreCodesButton.Definition,
                ProlongadorButton.Definition,
                ParameterEditorButton.Definition
            });
        }
    }
}
