using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless do POCO MepPort (record imutavel). Cobrem build com
/// required fields, default de Shape, semantica value-equality de record
/// e immutability via with-expression. Sem Revit.
/// </summary>
public class MepPortTests
{
    [Fact]
    public void Constructs_with_required_fields()
    {
        // Required: Role, DnMm, Direction, Origin. Shape deve defaultar Round.
        var port = new MepPort
        {
            Role = PortRole.Inlet,
            DnMm = 50,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };

        Assert.Equal(PortRole.Inlet, port.Role);
        Assert.Equal(50, port.DnMm);
        Assert.Equal(Vector3.UnitX, port.Direction);
        Assert.Equal(Vector3.Zero, port.Origin);
        Assert.Equal(ConnectorShape.Round, port.Shape);
    }

    [Fact]
    public void Shape_default_is_Round()
    {
        var port = new MepPort
        {
            Role = PortRole.Outlet,
            DnMm = 25,
            Direction = Vector3.UnitY,
            Origin = Vector3.Zero,
        };

        Assert.Equal(ConnectorShape.Round, port.Shape);
    }

    [Fact]
    public void Shape_can_be_overridden_at_construction()
    {
        var port = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 100,
            Direction = Vector3.UnitZ,
            Origin = Vector3.Zero,
            Shape = ConnectorShape.Rectangular,
        };

        Assert.Equal(ConnectorShape.Rectangular, port.Shape);
    }

    [Fact]
    public void Equality_holds_for_identical_records()
    {
        // record value semantics: dois MepPort com mesmos fields devem
        // ser .Equals e ter mesmo hashcode.
        var a = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 75,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };
        var b = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 75,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Not_equal_when_dn_differs()
    {
        var a = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 50,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };
        var b = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 75,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void Not_equal_when_role_differs()
    {
        var a = new MepPort
        {
            Role = PortRole.RunA,
            DnMm = 50,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };
        var b = new MepPort
        {
            Role = PortRole.Branch,
            DnMm = 50,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void With_expression_preserves_immutability_and_updates_field()
    {
        // with-expression cria novo record com field alterado;
        // o original deve permanecer intacto.
        var original = new MepPort
        {
            Role = PortRole.Inlet,
            DnMm = 50,
            Direction = Vector3.UnitX,
            Origin = Vector3.Zero,
        };

        var copy = original with { DnMm = 75 };

        Assert.Equal(50, original.DnMm);
        Assert.Equal(75, copy.DnMm);
        Assert.NotSame(original, copy);
        Assert.NotEqual(original, copy);
        // Demais fields preservados na copia.
        Assert.Equal(original.Role, copy.Role);
        Assert.Equal(original.Direction, copy.Direction);
        Assert.Equal(original.Origin, copy.Origin);
        Assert.Equal(original.Shape, copy.Shape);
    }
}
