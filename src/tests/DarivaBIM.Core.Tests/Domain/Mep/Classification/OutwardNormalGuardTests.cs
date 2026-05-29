using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless da blindagem de sinal do OutwardNormal (Codex #2). Provam que
/// um BasisZ reportado inward e corrigido para outward e que, sem a blindagem, o
/// motor classificaria uma luva reta como joelho (o bug que isto previne).
/// </summary>
public class OutwardNormalGuardTests
{
    private static ConnectorReading Conn(int nativeIndex, Vector3 normal, Vector3 origin, int dn = 50) => new()
    {
        NativeIndex = nativeIndex,
        OutwardNormal = normal,
        Origin = origin,
        DnMm = dn,
    };

    // Apos a blindagem, todo normal deve apontar para fora do centroide.
    private static bool AllPointOutward(IReadOnlyList<ConnectorReading> readings)
    {
        Vector3 centroid = Vector3.Zero;
        foreach (ConnectorReading r in readings)
        {
            centroid += r.Origin;
        }

        centroid /= (float)readings.Count;
        return readings.All(r => Vector3.Dot(r.OutwardNormal, r.Origin - centroid) >= -1e-4f);
    }

    [Fact]
    public void Already_outward_normals_are_unchanged()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0)),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-50, 0, 0)),
        };

        var guarded = OutwardNormalGuard.EnsureOutward(readings);

        Assert.Equal(new Vector3(1, 0, 0), guarded[0].OutwardNormal);
        Assert.Equal(new Vector3(-1, 0, 0), guarded[1].OutwardNormal);
    }

    [Fact]
    public void Inward_normal_is_flipped_to_outward()
    {
        // r1 deveria apontar (-1,0,0) mas o BasisZ veio inward como (1,0,0).
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0)),
            Conn(1, new Vector3(1, 0, 0), new Vector3(-50, 0, 0)),
        };

        var guarded = OutwardNormalGuard.EnsureOutward(readings);

        Assert.Equal(new Vector3(1, 0, 0), guarded[0].OutwardNormal);
        Assert.Equal(new Vector3(-1, 0, 0), guarded[1].OutwardNormal);
        Assert.True(AllPointOutward(guarded));
    }

    [Fact]
    public void Single_connector_is_returned_intact()
    {
        var readings = new[] { Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0)) };

        var guarded = OutwardNormalGuard.EnsureOutward(readings);

        Assert.Single(guarded);
        Assert.Equal(new Vector3(1, 0, 0), guarded[0].OutwardNormal);
    }

    [Fact]
    public void Empty_returns_empty()
    {
        var guarded = OutwardNormalGuard.EnsureOutward(Array.Empty<ConnectorReading>());

        Assert.Empty(guarded);
    }

    [Fact]
    public void Is_idempotent()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0)),
            Conn(1, new Vector3(1, 0, 0), new Vector3(-50, 0, 0)), // inward
        };

        var once = OutwardNormalGuard.EnsureOutward(readings);
        var twice = OutwardNormalGuard.EnsureOutward(once);

        Assert.Equal(once.Select(r => r.OutwardNormal), twice.Select(r => r.OutwardNormal));
    }

    [Fact]
    public void Guard_corrects_straight_pipe_that_engine_would_otherwise_call_Elbow()
    {
        // Luva reta com um BasisZ invertido: sem guard os normais (1,0,0)+(1,0,0)
        // dao angulo 0 -> nao-inline -> Elbow (ERRADO). Com guard -> anti-paralelo
        // -> inline -> Union. Esta e a protecao do dinheiro (Codex #2).
        var raw = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0)),
            Conn(1, new Vector3(1, 0, 0), new Vector3(-50, 0, 0)), // inward
        };

        var withoutGuard = TopologyInferenceEngine.Infer(
            raw, "Union", Discipline.Plumbing, ProductCategory.PipeFitting);
        var withGuard = TopologyInferenceEngine.Infer(
            OutwardNormalGuard.EnsureOutward(raw), "Union", Discipline.Plumbing, ProductCategory.PipeFitting);

        Assert.Equal(BaseKind.Elbow, withoutGuard.Topology!.InferredBaseKind);
        Assert.Equal(BaseKind.Union, withGuard.Topology!.InferredBaseKind);
    }
}
