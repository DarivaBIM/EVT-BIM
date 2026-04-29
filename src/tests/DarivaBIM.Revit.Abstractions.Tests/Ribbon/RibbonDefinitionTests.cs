using DarivaBIM.Revit.Abstractions.Ribbon;
using Xunit;

namespace DarivaBIM.Revit.Abstractions.Tests.Ribbon
{
    public class RibbonDefinitionTests
    {
        [Fact]
        public void RibbonDefinition_exposes_panels_and_buttons()
        {
            var button = new RibbonButtonDefinition(
                internalName: "Test",
                text: "Test",
                commandId: RibbonCommandId.WriteTigreCodes);
            var panel = new RibbonPanelDefinition("Panel", new[] { button });
            var ribbon = new RibbonDefinition("Tab", new[] { panel });

            Assert.Equal("Tab", ribbon.TabName);
            Assert.Single(ribbon.Panels);
            Assert.Single(ribbon.Panels[0].Buttons);
            Assert.Equal(RibbonCommandId.WriteTigreCodes, ribbon.Panels[0].Buttons[0].CommandId);
        }
    }
}
