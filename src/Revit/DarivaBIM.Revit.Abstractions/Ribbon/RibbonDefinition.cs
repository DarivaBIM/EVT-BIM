using System.Collections.Generic;

namespace DarivaBIM.Revit.Abstractions.Ribbon
{
    public sealed class RibbonDefinition
    {
        public RibbonDefinition(string tabName, IReadOnlyList<RibbonPanelDefinition> panels)
        {
            TabName = tabName;
            Panels = panels;
        }

        public string TabName { get; }

        public IReadOnlyList<RibbonPanelDefinition> Panels { get; }
    }
}
