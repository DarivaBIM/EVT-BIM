using System.Collections.Generic;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless do POCO ConnectionTopology — defaults + helpers computed
/// (AllDns, RunDn, BranchDn, HasReduction) + init-only ReductionKind.
/// Sem Revit.
/// </summary>
public class ConnectionTopologyTests
{
    // Helper local pra construir MepPort rapido com Direction/Origin canonicos.
    private static MepPort Port(PortRole role, int dn) => new()
    {
        Role = role,
        DnMm = dn,
        Direction = Vector3.UnitX,
        Origin = Vector3.Zero,
    };

    [Fact]
    public void Defaults_are_safe_with_empty_ports()
    {
        // Topology com Ports vazio nao deve crashar helpers; PartType default "";
        // ReductionKind default None; AngleMatrix/DistanceMatrix arrays vazios;
        // Inferreds Unknown; IsInlinePairDetected false.
        var topology = new ConnectionTopology
        {
            Ports = System.Array.Empty<MepPort>(),
        };

        Assert.Equal("", topology.PartType);
        Assert.Equal(ReductionKind.None, topology.ReductionKind);
        Assert.Equal(BaseKind.Unknown, topology.InferredBaseKind);
        Assert.Equal(Discipline.Unknown, topology.InferredDiscipline);
        Assert.Equal(ProductCategory.Unknown, topology.InferredCategory);
        Assert.False(topology.IsInlinePairDetected);
        Assert.Empty(topology.AngleMatrix);
        Assert.Empty(topology.DistanceMatrix);

        // Helpers nao crasham com Ports vazio.
        Assert.Empty(topology.AllDns);
        Assert.Null(topology.RunDn);
        Assert.Null(topology.BranchDn);
        Assert.False(topology.HasReduction);
    }

    [Fact]
    public void AllDns_returns_all_port_dns_in_order()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.RunA, 50),
                Port(PortRole.RunB, 50),
                Port(PortRole.Branch, 25),
            },
        };

        Assert.Equal(new[] { 50, 50, 25 }, topology.AllDns);
    }

    [Fact]
    public void RunDn_returns_first_port_with_RunA_role()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.Inlet, 32),
                Port(PortRole.RunA, 50),
                Port(PortRole.RunA, 75), // segundo RunA — helper pega o primeiro
            },
        };

        Assert.Equal(50, topology.RunDn);
    }

    [Fact]
    public void RunDn_is_null_when_no_RunA_port_present()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.Inlet, 50),
                Port(PortRole.Outlet, 50),
            },
        };

        Assert.Null(topology.RunDn);
    }

    [Fact]
    public void BranchDn_returns_first_port_with_Branch_role()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.RunA, 100),
                Port(PortRole.RunB, 100),
                Port(PortRole.Branch, 50),
            },
        };

        Assert.Equal(50, topology.BranchDn);
    }

    [Fact]
    public void BranchDn_is_null_when_no_Branch_port_present()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.RunA, 50),
                Port(PortRole.RunB, 50),
            },
        };

        Assert.Null(topology.BranchDn);
    }

    [Fact]
    public void HasReduction_false_when_all_dns_equal()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.RunA, 50),
                Port(PortRole.RunB, 50),
                Port(PortRole.Branch, 50),
            },
        };

        Assert.False(topology.HasReduction);
    }

    [Fact]
    public void HasReduction_true_when_dns_differ()
    {
        var topology = new ConnectionTopology
        {
            Ports = new[]
            {
                Port(PortRole.RunA, 75),
                Port(PortRole.RunB, 50),
            },
        };

        Assert.True(topology.HasReduction);
    }

    [Fact]
    public void ReductionKind_is_init_only_default_None()
    {
        // Default e None quando nao especificado.
        var defaultTopology = new ConnectionTopology
        {
            Ports = System.Array.Empty<MepPort>(),
        };
        Assert.Equal(ReductionKind.None, defaultTopology.ReductionKind);

        // E quando seto via init, o record carrega o valor.
        var withReduction = new ConnectionTopology
        {
            Ports = System.Array.Empty<MepPort>(),
            ReductionKind = ReductionKind.Eccentric,
        };
        Assert.Equal(ReductionKind.Eccentric, withReduction.ReductionKind);
    }

    [Fact]
    public void PartType_can_be_assigned_to_raw_string()
    {
        // PartType e string raw vinda do Revit ("Elbow", "Tee", ...).
        var topology = new ConnectionTopology
        {
            PartType = "Elbow",
            Ports = System.Array.Empty<MepPort>(),
        };

        Assert.Equal("Elbow", topology.PartType);
    }

    [Fact]
    public void Inferred_fields_carry_assigned_enum_values()
    {
        var topology = new ConnectionTopology
        {
            Ports = System.Array.Empty<MepPort>(),
            InferredBaseKind = BaseKind.Tee,
            InferredDiscipline = Discipline.Plumbing,
            InferredCategory = ProductCategory.PipeFitting,
            IsInlinePairDetected = true,
        };

        Assert.Equal(BaseKind.Tee, topology.InferredBaseKind);
        Assert.Equal(Discipline.Plumbing, topology.InferredDiscipline);
        Assert.Equal(ProductCategory.PipeFitting, topology.InferredCategory);
        Assert.True(topology.IsInlinePairDetected);
    }
}
