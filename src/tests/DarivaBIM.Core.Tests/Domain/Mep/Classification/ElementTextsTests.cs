using DarivaBIM.Domain.Mep.Classification.Connections;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless do POCO ElementTexts — defaults "" (nunca null) e value
/// equality de record. Sem Revit.
/// </summary>
public class ElementTextsTests
{
    [Fact]
    public void Defaults_are_empty_strings()
    {
        var texts = new ElementTexts();

        Assert.Equal("", texts.FamilyName);
        Assert.Equal("", texts.TypeName);
        Assert.Equal("", texts.Description);
    }

    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new ElementTexts
        {
            FamilyName = "ESG_Redux_Joelho 45_90",
            TypeName = "DN50",
            Description = "Joelho 90 REDUX DN50",
        };
        var b = new ElementTexts
        {
            FamilyName = "ESG_Redux_Joelho 45_90",
            TypeName = "DN50",
            Description = "Joelho 90 REDUX DN50",
        };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void With_expression_updates_single_field()
    {
        var original = new ElementTexts { FamilyName = "F", TypeName = "T", Description = "D" };

        var copy = original with { Description = "D2" };

        Assert.Equal("D", original.Description);
        Assert.Equal("D2", copy.Description);
        Assert.Equal("F", copy.FamilyName);
    }
}
