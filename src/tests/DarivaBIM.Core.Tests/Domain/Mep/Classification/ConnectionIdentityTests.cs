using System;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless do POCO ConnectionIdentity — required fields, defaults,
/// granulacoes opcionais (Valve/Instrument/Filter) e value-equality de record.
/// Sem Revit.
/// </summary>
public class ConnectionIdentityTests
{
    // Helpers locais pra reduzir boilerplate dos testes.
    private static MepPort Port(PortRole role, int dn) => new()
    {
        Role = role,
        DnMm = dn,
        Direction = Vector3.UnitX,
        Origin = Vector3.Zero,
    };

    private static ClassificationConfidence HighConfidence() => new()
    {
        Score = 0.9,
        Bucket = ConfidenceBucket.High,
        Reasons = Array.Empty<string>(),
    };

    [Fact]
    public void Constructs_with_only_required_fields()
    {
        // Required: Discipline, Category, BaseKind, Ports, Confidence.
        // Demais devem cair em defaults documentados.
        var identity = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
            Confidence = HighConfidence(),
        };

        Assert.Equal(Discipline.Plumbing, identity.Discipline);
        Assert.Equal(ProductCategory.PipeFitting, identity.Category);
        Assert.Equal(BaseKind.Elbow, identity.BaseKind);
        Assert.Equal(2, identity.Ports.Count);

        // Defaults.
        Assert.Equal(GeometryKind.Unspecified, identity.GeometryKind);
        Assert.Equal(Feature.None, identity.Features);
        Assert.Equal(ProductLine.Unknown, identity.Line);
        Assert.Null(identity.NominalAngleDeg);

        // Granulacoes opcionais nao setadas devem ser null.
        Assert.Null(identity.ValveKind);
        Assert.Null(identity.InstrumentKind);
        Assert.Null(identity.FilterKind);
    }

    [Fact]
    public void Carries_optional_valve_kind_when_set()
    {
        var identity = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeAccessory,
            BaseKind = BaseKind.Valve,
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
            Confidence = HighConfidence(),
            ValveKind = ValveKind.Shutoff,
        };

        Assert.Equal(ValveKind.Shutoff, identity.ValveKind);
        // Outras granulacoes seguem null.
        Assert.Null(identity.InstrumentKind);
        Assert.Null(identity.FilterKind);
    }

    [Fact]
    public void Carries_nominal_angle_when_set()
    {
        var identity = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            NominalAngleDeg = 45.0,
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
            Confidence = HighConfidence(),
        };

        Assert.Equal(45.0, identity.NominalAngleDeg);
    }

    [Fact]
    public void Features_flag_combinations_are_preserved()
    {
        // Feature e [Flags] — confere que combinacoes bitwise sobrevivem.
        var identity = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
            Confidence = HighConfidence(),
            Features = Feature.ThreadedEnd | Feature.BrassBushing,
        };

        Assert.True(identity.Features.HasFlag(Feature.ThreadedEnd));
        Assert.True(identity.Features.HasFlag(Feature.BrassBushing));
        Assert.False(identity.Features.HasFlag(Feature.Inspection));
    }

    [Fact]
    public void Confidence_is_required_and_attached()
    {
        var confidence = new ClassificationConfidence
        {
            Score = 0.62,
            Bucket = ConfidenceBucket.Medium,
            Reasons = new[] { "TopologyMatched:Elbow90" },
        };

        var identity = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
            Confidence = confidence,
        };

        Assert.Same(confidence, identity.Confidence);
        Assert.Equal(0.62, identity.Confidence.Score);
        Assert.Equal(ConfidenceBucket.Medium, identity.Confidence.Bucket);
        Assert.Single(identity.Confidence.Reasons);
    }

    [Fact]
    public void Equality_holds_when_all_fields_match()
    {
        // record sealed: deve ter value equality. Mesmos enums, mesma ref de
        // ports (pq IReadOnlyList compara por referencia em records), mesma
        // ref de confidence.
        var ports = new[]
        {
            Port(PortRole.Inlet, 50),
            Port(PortRole.Outlet, 50),
        };
        var confidence = HighConfidence();

        var a = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = ports,
            Confidence = confidence,
        };
        var b = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = ports,
            Confidence = confidence,
        };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Inequality_when_baseKind_differs()
    {
        var ports = new[]
        {
            Port(PortRole.Inlet, 50),
            Port(PortRole.Outlet, 50),
        };
        var confidence = HighConfidence();

        var elbow = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Elbow,
            Ports = ports,
            Confidence = confidence,
        };
        var tee = new ConnectionIdentity
        {
            Discipline = Discipline.Plumbing,
            Category = ProductCategory.PipeFitting,
            BaseKind = BaseKind.Tee,
            Ports = ports,
            Confidence = confidence,
        };

        Assert.NotEqual(elbow, tee);
        Assert.False(elbow == tee);
    }

    [Fact]
    public void ClassificationConfidence_carries_reasons_list()
    {
        // Sanity check do tipo aninhado: Reasons deve preservar items.
        var confidence = new ClassificationConfidence
        {
            Score = 0.30,
            Bucket = ConfidenceBucket.Low,
            Reasons = new[] { "FallbackUnknown", "NoLexicalHint" },
        };

        Assert.Equal(0.30, confidence.Score);
        Assert.Equal(ConfidenceBucket.Low, confidence.Bucket);
        Assert.Equal(2, confidence.Reasons.Count);
        Assert.Contains("FallbackUnknown", confidence.Reasons);
        Assert.Contains("NoLexicalHint", confidence.Reasons);
    }
}
