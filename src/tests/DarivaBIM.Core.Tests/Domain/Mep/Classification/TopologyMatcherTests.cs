using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Filtro topologico (2.B-3a) contra o pipe_connection_rules.json REAL. Fixtures de
/// ConnectionTopology montados a mao (sem Revit), espelhando os roles/angulos que o
/// motor 1.B-1 produz. Os cenarios provam end-to-end as convencoes de angulo do gate
/// 2.B-2: primaryAngleRule raw direto e lateral = min(raw, 180-raw).
/// </summary>
public class TopologyMatcherTests
{
    private const string LogicalName = "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

    private static readonly ConnectionRulebookDocument Rulebook =
        ConnectionRulebookLoader.LoadEmbedded(typeof(ConnectionRulebookLoader).Assembly, LogicalName);

    // Direction/Origin sao irrelevantes para o matcher (ele so le Role, DnMm e a
    // AngleMatrix ja calculada); ficam dummy.
    private static MepPort Port(PortRole role, int dnMm)
        => new() { Role = role, DnMm = dnMm, Direction = Vector3.UnitX, Origin = Vector3.Zero };

    private static ConnectionTopology Topo(string partType, BaseKind inferred, MepPort[] ports, double[][] angle)
        => new()
        {
            PartType = partType,
            Ports = ports,
            AngleMatrix = Array.ConvertAll(angle, r => (IReadOnlyList<double>)r),
            InferredBaseKind = inferred,
        };

    private static IReadOnlyList<string> Ids(ConnectionTopology topology)
        => TopologyMatcher.FilterCandidates(Rulebook, topology).Select(r => r.Id).ToList();

    [Fact]
    public void Elbow45_raw135_includes_elbow45_and_excludes_elbow90_and_union()
    {
        // ⭐ PROVA a convencao RAW do gate 2.B-2 end-to-end: joelho 45 fisico -> raw 135;
        // so casa elbow-45 {130,140}, nunca elbow-90 {85,95}. PartType "Elbow" e aceito
        // por ambos -> a UNICA coisa que os distingue aqui e o angulo raw.
        ConnectionTopology t = Topo("Elbow", BaseKind.Elbow,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
            new[] { new[] { 0.0, 135.0 }, new[] { 135.0, 0.0 } });

        IReadOnlyList<string> ids = Ids(t);

        Assert.Contains("elbow-45", ids);
        Assert.DoesNotContain("elbow-90", ids);
        Assert.DoesNotContain("union-simple", ids);
    }

    [Fact]
    public void Elbow90_raw90_includes_elbow90_and_excludes_elbow45()
    {
        ConnectionTopology t = Topo("Elbow", BaseKind.Elbow,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
            new[] { new[] { 0.0, 90.0 }, new[] { 90.0, 0.0 } });

        IReadOnlyList<string> ids = Ids(t);

        Assert.Contains("elbow-90", ids);
        Assert.DoesNotContain("elbow-45", ids);
    }

    [Fact]
    public void Union_raw180_equalDn_includes_unionSimple_and_excludes_reducer()
    {
        ConnectionTopology t = Topo("Undefined", BaseKind.Union,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
            new[] { new[] { 0.0, 180.0 }, new[] { 180.0, 0.0 } });

        IReadOnlyList<string> ids = Ids(t);

        Assert.Contains("union-simple", ids);
        // reducer exige different[RunLarge,RunSmall]; esses roles ausentes -> nao casa.
        Assert.DoesNotContain("reducer-concentric", ids);
    }

    [Fact]
    public void Reducer_raw180_differentDn_includes_reducer_and_excludes_unionSimple()
    {
        // ⭐ PROVA a decisao "roles explicitos ausentes = constraint falha": o motor da
        // RunLarge/RunSmall a um reducer; union-simple (equal[RunA,RunB]) NAO pode casar
        // vacuosamente so porque RunA/RunB nao existem.
        ConnectionTopology t = Topo("Undefined", BaseKind.Reducer,
            new[] { Port(PortRole.RunLarge, 50), Port(PortRole.RunSmall, 32) },
            new[] { new[] { 0.0, 180.0 }, new[] { 180.0, 0.0 } });

        IReadOnlyList<string> ids = Ids(t);

        Assert.Contains("reducer-concentric", ids);
        Assert.DoesNotContain("union-simple", ids);
    }

    [Fact]
    public void Tee_runs180_lateral90_includes_tee()
    {
        ConnectionTopology t = Topo("Tee", BaseKind.Tee,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50), Port(PortRole.Branch, 50) },
            new[]
            {
                new[] { 0.0, 180.0, 90.0 },
                new[] { 180.0, 0.0, 90.0 },
                new[] { 90.0, 90.0, 0.0 },
            });

        Assert.Contains("tee", Ids(t));
    }

    [Fact]
    public void Wye_branchRaw135_lateral45_includes_wyeSimple_and_excludes_tee()
    {
        // ⭐ PROVA a extracao lateral via min(raw, 180-raw): branch raw 135 -> lateral 45,
        // casa wye-simple {40,50} e NAO tee {85,95}. PartType "LateralTee" e aceito por
        // ambos -> a UNICA distincao e o angulo lateral.
        ConnectionTopology t = Topo("LateralTee", BaseKind.Wye,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50), Port(PortRole.Branch, 50) },
            new[]
            {
                new[] { 0.0, 180.0, 135.0 },
                new[] { 180.0, 0.0, 45.0 },
                new[] { 135.0, 45.0, 0.0 },
            });

        IReadOnlyList<string> ids = Ids(t);

        Assert.Contains("wye-simple", ids);
        Assert.DoesNotContain("tee", ids);
    }

    [Fact]
    public void Cross_count4_runs180_lateral90_includes_cross()
    {
        ConnectionTopology t = Topo("Cross", BaseKind.Cross,
            new[]
            {
                Port(PortRole.RunA, 50), Port(PortRole.RunB, 50),
                Port(PortRole.BranchLeft, 50), Port(PortRole.BranchRight, 50),
            },
            new[]
            {
                new[] { 0.0, 180.0, 90.0, 90.0 },
                new[] { 180.0, 0.0, 90.0, 90.0 },
                new[] { 90.0, 90.0, 0.0, 180.0 },
                new[] { 90.0, 90.0, 180.0, 0.0 },
            });

        Assert.Contains("cross", Ids(t));
    }

    [Fact]
    public void Cap_singleConnector_includes_cap()
    {
        ConnectionTopology t = Topo("Cap", BaseKind.Cap,
            new[] { Port(PortRole.Outlet, 50) },
            new[] { new[] { 0.0 } });

        Assert.Contains("cap", Ids(t));
    }

    [Fact]
    public void Manifold_guard_rejects_two_connector_undefined()
    {
        // manifold so tem PartTypeAccepts (inclui "Undefined"); sem a guarda anti-catch-all
        // casaria esta peca de 2 bocas. InferredBaseKind=Union != MultiPort -> rejeita.
        ConnectionTopology t = Topo("Undefined", BaseKind.Union,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
            new[] { new[] { 0.0, 180.0 }, new[] { 180.0, 0.0 } });

        Assert.DoesNotContain("manifold", Ids(t));
    }

    [Fact]
    public void TeeReducer_branchSmaller_satisfies_lessThan_diameter()
    {
        // tee-reducer: lessThan[Branch] target RunA. Branch 25 < RunA 50 - tol 2 -> casa.
        ConnectionTopology t = Topo("Tee", BaseKind.Tee,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50), Port(PortRole.Branch, 25) },
            new[]
            {
                new[] { 0.0, 180.0, 90.0 },
                new[] { 180.0, 0.0, 90.0 },
                new[] { 90.0, 90.0, 0.0 },
            });

        Assert.Contains("tee-reducer", Ids(t));
    }

    [Fact]
    public void Tee_with_different_run_dns_is_excluded_by_equal_diameter()
    {
        // RunA 50 / RunB 40 (diff 10 > tol 2): a regra "tee" exige equal[RunA,RunB] -> nao casa.
        ConnectionTopology t = Topo("Tee", BaseKind.Tee,
            new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 40), Port(PortRole.Branch, 50) },
            new[]
            {
                new[] { 0.0, 180.0, 90.0 },
                new[] { 180.0, 0.0, 90.0 },
                new[] { 90.0, 90.0, 0.0 },
            });

        Assert.DoesNotContain("tee", Ids(t));
    }

    [Fact]
    public void FilterCandidates_null_inputs_return_empty()
    {
        ConnectionTopology t = Topo("Cap", BaseKind.Cap,
            new[] { Port(PortRole.Outlet, 50) },
            new[] { new[] { 0.0 } });

        Assert.Empty(TopologyMatcher.FilterCandidates(null!, t));
        Assert.Empty(TopologyMatcher.FilterCandidates(Rulebook, null!));
    }
}
