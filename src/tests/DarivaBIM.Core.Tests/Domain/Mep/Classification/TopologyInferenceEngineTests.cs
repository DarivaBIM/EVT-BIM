using System;
using System.Linq;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests headless do motor de inferencia topologica (secao 9 do rulebook, com a
/// correcao C1/D1 do roadmap). Cobrem todos os casos de contagem de conector + os
/// 3 edge canonicos (joelho 45/90; te com PartType=Undefined; juncao rotulada
/// Tee que vira Wye) + determinismo de role. Sem Revit: conectores sao Vector3
/// sinteticos. LEMBRAR: peca reta = OutwardNormals ANTI-paralelos (dot ~ -1, ~180
/// graus).
/// </summary>
public class TopologyInferenceEngineTests
{
    // cos(45) = sin(45), usado para montar ramais/joelhos a 45 graus.
    private static readonly float Cos45 = (float)(Math.Sqrt(2.0) / 2.0);

    private static ConnectorReading Conn(int nativeIndex, Vector3 normal, Vector3 origin, int dn) => new()
    {
        NativeIndex = nativeIndex,
        OutwardNormal = normal,
        Origin = origin,
        DnMm = dn,
    };

    private static MepPort? PortWith(ConnectionTopology t, PortRole role)
        => t.Ports.FirstOrDefault(p => p.Role == role);

    [Fact]
    public void Count0_returns_failure_with_error_diagnostic()
    {
        var result = TopologyInferenceEngine.Infer(
            Array.Empty<ConnectorReading>(), "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);

        Assert.False(result.Success);
        Assert.Null(result.Topology);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == TopologyDiagnosticCode.InsufficientConnectorsAfterFilter
            && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Count1_is_Cap_with_single_outlet()
    {
        var readings = new[] { Conn(0, Vector3.UnitX, Vector3.Zero, 50) };

        var result = TopologyInferenceEngine.Infer(readings, "Cap", Discipline.Plumbing, ProductCategory.PipeFitting);

        Assert.True(result.Success);
        var t = result.Topology!;
        Assert.Equal(BaseKind.Cap, t.InferredBaseKind);
        Assert.Single(t.Ports);
        Assert.Equal(PortRole.Outlet, t.Ports[0].Role);
        Assert.False(t.IsInlinePairDetected);
    }

    [Fact]
    public void Count2_inline_equal_dn_is_Union()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-50, 0, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Union", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Union, t.InferredBaseKind);
        Assert.True(t.IsInlinePairDetected);
        Assert.Equal(ReductionKind.None, t.ReductionKind);
        Assert.NotNull(PortWith(t, PortRole.RunA));
        Assert.NotNull(PortWith(t, PortRole.RunB));
    }

    [Fact]
    public void Count2_inline_different_dn_is_Reducer_with_RunLarge_RunSmall()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0), 100),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-50, 0, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Transition", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Reducer, t.InferredBaseKind);
        Assert.True(t.IsInlinePairDetected);
        Assert.Equal(ReductionKind.Concentric, t.ReductionKind);
        Assert.Equal(100, PortWith(t, PortRole.RunLarge)!.DnMm);
        Assert.Equal(50, PortWith(t, PortRole.RunSmall)!.DnMm);
    }

    [Fact]
    public void Count2_not_inline_is_Elbow_with_raw_90_in_matrix()
    {
        // Normais a 90 graus -> nao-inline -> Elbow. A AngleMatrix carrega o angulo
        // RAW (90). NOTA: 90 e o caso em que raw e deflexao COINCIDEM (180-90=90),
        // por isso este caso nao denunciava a inversao raw-vs-deflexao (vide o teste
        // do joelho de deflexao 45, cujo raw e 135).
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0), 50),
            Conn(1, new Vector3(0, 1, 0), new Vector3(0, 50, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Elbow", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Elbow, t.InferredBaseKind);
        Assert.False(t.IsInlinePairDetected);
        Assert.InRange(t.AngleMatrix[0][1], 89.0, 91.0);         // raw ~ 90
        Assert.InRange(180.0 - t.AngleMatrix[0][1], 89.0, 91.0); // contrato: deflexao = 180 - raw = 90 (coincide)
    }

    [Fact]
    public void Count2_not_inline_physical_45deg_elbow_has_raw_135_in_matrix()
    {
        // Joelho de DEFLEXAO 45 (o "Joelho 45" do catalogo Tigre) tem os BasisZ
        // outward a 135 graus entre si: deflexao = 180 - raw. O motor NAO converte;
        // a 2.B traduz raw 135 -> "Joelho 45". (Errata rulebook secao 9, 2026-05-28.)
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0), 50),
            Conn(1, new Vector3(-Cos45, Cos45, 0), new Vector3(-35, 35, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Elbow", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Elbow, t.InferredBaseKind);
        Assert.False(t.IsInlinePairDetected);
        Assert.InRange(t.AngleMatrix[0][1], 134.0, 136.0);       // raw ~ 135
        Assert.InRange(180.0 - t.AngleMatrix[0][1], 44.0, 46.0); // contrato: deflexao = 180 - raw = 45
    }

    [Fact]
    public void Count3_inline_branch90_undefined_partType_is_Tee_with_undefined_diagnostic()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Undefined", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Tee, t.InferredBaseKind);
        Assert.True(t.IsInlinePairDetected);
        Assert.NotNull(PortWith(t, PortRole.RunA));
        Assert.NotNull(PortWith(t, PortRole.RunB));
        Assert.NotNull(PortWith(t, PortRole.Branch));
        Assert.Contains(result.Diagnostics, d =>
            d.Code == TopologyDiagnosticCode.PartTypeUndefined && d.Severity == DiagnosticSeverity.Info);
        // Sem hint, nao ha como divergir: nenhum mismatch.
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == TopologyDiagnosticCode.PartTypeMismatchInferred);
    }

    [Fact]
    public void Count3_inline_branch45_labeled_Tee_is_Wye_with_mismatch_diagnostic()
    {
        // Modelador rotulou como Tee, mas o ramal sai a 45 graus do eixo -> Wye.
        // A geometria prevalece (D7) e emite PartTypeMismatchInferred (Warning).
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(-Cos45, Cos45, 0), new Vector3(-70, 70, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Wye, t.InferredBaseKind);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == TopologyDiagnosticCode.PartTypeMismatchInferred && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Count3_inline_branch90_partType_Tee_matches_without_partType_diagnostic()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Tee, t.InferredBaseKind);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == TopologyDiagnosticCode.PartTypeUndefined);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == TopologyDiagnosticCode.PartTypeMismatchInferred);
    }

    [Fact]
    public void Count3_tee_with_reduced_branch_has_BranchOnly_reduction()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 100),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 100),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Tee, t.InferredBaseKind);
        Assert.Equal(ReductionKind.BranchOnly, t.ReductionKind);
        Assert.Equal(100, t.RunDn);
        Assert.Equal(50, t.BranchDn);
    }

    [Fact]
    public void Count4_inline_is_Cross_with_run_and_branch_roles()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
            Conn(3, new Vector3(0, -1, 0), new Vector3(0, -100, 0), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Cross", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.Cross, t.InferredBaseKind);
        Assert.True(t.IsInlinePairDetected);
        Assert.NotNull(PortWith(t, PortRole.RunA));
        Assert.NotNull(PortWith(t, PortRole.RunB));
        Assert.NotNull(PortWith(t, PortRole.BranchLeft));
        Assert.NotNull(PortWith(t, PortRole.BranchRight));
    }

    [Fact]
    public void Count5_is_MultiPort()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 25),
            Conn(3, new Vector3(0, 1, 0), new Vector3(0, 200, 0), 25),
            Conn(4, new Vector3(0, 1, 0), new Vector3(0, 300, 0), 25),
        };

        var result = TopologyInferenceEngine.Infer(readings, "MultiPort", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.Equal(BaseKind.MultiPort, t.InferredBaseKind);
        Assert.Equal(5, t.Ports.Count);
        Assert.NotNull(PortWith(t, PortRole.Inlet));
    }

    [Fact]
    public void Count3_not_inline_is_MultiPort_with_anomaly_diagnostic()
    {
        // 3 conectores mutuamente perpendiculares: nenhum par anti-paralelo (sem
        // eixo passante) -> anomalia geometrica.
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
            Conn(2, new Vector3(0, 0, 1), new Vector3(0, 0, 100), 50),
        };

        var result = TopologyInferenceEngine.Infer(readings, "Undefined", Discipline.Plumbing, ProductCategory.PipeFitting);

        var t = result.Topology!;
        Assert.False(t.IsInlinePairDetected);
        Assert.Equal(BaseKind.MultiPort, t.InferredBaseKind);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == TopologyDiagnosticCode.OriginOutsideExpectedIntersection
            && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Discipline_and_category_pass_through_to_topology()
    {
        var readings = new[] { Conn(0, Vector3.UnitX, Vector3.Zero, 50) };

        var result = TopologyInferenceEngine.Infer(readings, "Cap", Discipline.Plumbing, ProductCategory.PipeAccessory);

        Assert.Equal(Discipline.Plumbing, result.Topology!.InferredDiscipline);
        Assert.Equal(ProductCategory.PipeAccessory, result.Topology!.InferredCategory);
    }

    [Fact]
    public void Matrices_are_square_with_zero_diagonal_and_expected_values()
    {
        var readings = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(50, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-50, 0, 0), 50),
        };

        var t = TopologyInferenceEngine.Infer(readings, "Union", Discipline.Plumbing, ProductCategory.PipeFitting).Topology!;

        Assert.Equal(2, t.AngleMatrix.Count);
        Assert.Equal(2, t.AngleMatrix[0].Count);
        Assert.InRange(t.AngleMatrix[0][0], -0.01, 0.01); // diagonal = 0
        Assert.InRange(t.AngleMatrix[0][1], 179.5, 180.01); // anti-paralelo ~180
        Assert.InRange(t.DistanceMatrix[0][1], 99.0, 101.0); // distancia entre Origins
        Assert.InRange(t.DistanceMatrix[0][0], -0.01, 0.01); // diagonal = 0
    }

    [Fact]
    public void Roles_are_deterministic_regardless_of_input_list_order()
    {
        // Mesma peca; lista de entrada embaralhada mas com NativeIndex fixo. O motor
        // ordena por NativeIndex, entao os roles saem identicos independente da
        // ordem com que o caller passou os conectores.
        var ordered = new[]
        {
            Conn(0, new Vector3(1, 0, 0), new Vector3(100, 0, 0), 50),
            Conn(1, new Vector3(-1, 0, 0), new Vector3(-100, 0, 0), 50),
            Conn(2, new Vector3(0, 1, 0), new Vector3(0, 100, 0), 50),
        };
        var shuffled = new[] { ordered[2], ordered[0], ordered[1] };

        var r1 = TopologyInferenceEngine.Infer(ordered, "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);
        var r2 = TopologyInferenceEngine.Infer(shuffled, "Tee", Discipline.Plumbing, ProductCategory.PipeFitting);

        Assert.Equal(r1.Topology!.InferredBaseKind, r2.Topology!.InferredBaseKind);
        Assert.Equal(
            r1.Topology!.Ports.Select(p => p.Role),
            r2.Topology!.Ports.Select(p => p.Role));
    }
}
