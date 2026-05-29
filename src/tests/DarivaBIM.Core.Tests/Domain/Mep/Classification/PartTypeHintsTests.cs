using DarivaBIM.Domain.Mep.Classification.Connections;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests do mapeamento Revit PartType (string raw) -> BaseKind hint (secao 7 do
/// rulebook). Hint FRACO (D7): so alimenta diagnostico; a geometria prevalece.
/// </summary>
public class PartTypeHintsTests
{
    [Theory]
    [InlineData("Elbow", BaseKind.Elbow)]
    [InlineData("Offset", BaseKind.Elbow)]
    [InlineData("Tee", BaseKind.Tee)]
    [InlineData("LateralTee", BaseKind.Tee)]
    [InlineData("TapPerpendicular", BaseKind.Tee)]
    [InlineData("SpudAdjustable", BaseKind.Tee)]
    [InlineData("Wye", BaseKind.Wye)]
    [InlineData("Cross", BaseKind.Cross)]
    [InlineData("LateralCross", BaseKind.Cross)]
    [InlineData("Union", BaseKind.Union)]
    [InlineData("PipeFlange", BaseKind.Union)]
    [InlineData("Transition", BaseKind.Reducer)]
    [InlineData("MultiPort", BaseKind.MultiPort)]
    [InlineData("Cap", BaseKind.Cap)]
    [InlineData("ValveNormal", BaseKind.Valve)]
    [InlineData("ValveBreaksInto", BaseKind.Valve)]
    public void ToBaseKindHint_maps_known_partTypes(string raw, BaseKind expected)
    {
        Assert.Equal(expected, PartTypeHints.ToBaseKindHint(raw));
    }

    [Theory]
    [InlineData("Undefined")]
    [InlineData("Other")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bogus")]
    [InlineData(null)]
    public void ToBaseKindHint_returns_null_for_unmapped_or_blank(string? raw)
    {
        Assert.Null(PartTypeHints.ToBaseKindHint(raw));
    }

    [Fact]
    public void ToBaseKindHint_trims_surrounding_whitespace()
    {
        // PartType vindo do Revit pode chegar com espacos; Trim garante o match.
        Assert.Equal(BaseKind.Elbow, PartTypeHints.ToBaseKindHint("  Elbow  "));
    }
}
