using DarivaBIM.Plugin.V2026.Ribbon.Panels;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon
{
    /// <summary>
    /// Declarative ribbon for the Revit 2026 plugin. Acts only as the aggregator
    /// of the main TigreBIM tab — each panel and each button lives in its own
    /// file under <c>Ribbon/Panels</c> and <c>Ribbon/Buttons</c>.
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
