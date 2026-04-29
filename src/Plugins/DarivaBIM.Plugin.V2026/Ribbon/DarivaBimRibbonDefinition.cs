using DarivaBIM.Plugin.V2026.Ribbon.Panels;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon
{
    /// <summary>
    /// Declarative ribbon for the Revit 2026 plugin. Acts only as the aggregator
    /// of the main TigreBIM tab — each panel lives under <c>Ribbon/Panels</c>
    /// and each button lives next to its feature in
    /// <c>Features/&lt;Tool&gt;/&lt;Tool&gt;Button.cs</c>.
    /// </summary>
    public static class DarivaBimRibbonDefinition
    {
        public const string TabName = "TigreBIM";

        public static RibbonDefinition Build()
        {
            return new RibbonDefinition(TabName, new[]
            {
                TigrePanelDefinition.Build()
            });
        }
    }
}
