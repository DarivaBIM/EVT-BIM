using System.Collections.Generic;

namespace DarivaBIM.Revit.Abstractions.Ribbon
{
    public sealed class RibbonPanelDefinition
    {
        public RibbonPanelDefinition(string name, IReadOnlyList<RibbonButtonDefinition> buttons)
        {
            Name = name;
            Buttons = buttons;
        }

        public string Name { get; }

        public IReadOnlyList<RibbonButtonDefinition> Buttons { get; }
    }
}
