using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Score lexical (cam. 3) e confidence (cam. 7) da 2.B-3b, testados em isolamento
/// com inputs montados a mao. As constantes de calibracao (pesos 3/2/1, base 0.5,
/// bonus topologico 0.30/0.20, teto lexical 0.20 com saturacao 6, Warning -0.10) sao
/// exercitadas pelos numeros esperados — se a Revisao recalibrar, estes testes pegam.
/// </summary>
public class ClassificationScoringTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoBaseKindTokens
        = new Dictionary<string, IReadOnlyList<string>>();

    private static ISet<string> Set(params string[] tokens)
        => new HashSet<string>(tokens, StringComparer.Ordinal);

    private static ConnectionRule Rule(BaseKind kind, params string[] hints)
        => new() { Id = kind.ToString().ToLowerInvariant(), BaseKind = kind, LexicalHints = hints };

    private static TopologyReadResult ReadOk(string partType, params TopologyDiagnostic[] diagnostics)
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology { PartType = partType, Ports = Array.Empty<MepPort>() },
            Diagnostics = diagnostics,
        };

    private static TopologyDiagnostic Warn(TopologyDiagnosticCode code)
        => new() { Code = code, Severity = DiagnosticSeverity.Warning };

    // ---- score lexical ----

    [Fact]
    public void ScoreCandidate_weights_family3_type2_desc1_and_sums_across_fields()
    {
        ConnectionRule rule = Rule(BaseKind.Elbow, "joelho");

        Assert.Equal(3, ClassificationScoring.ScoreCandidate(rule, NoBaseKindTokens, Set("joelho"), Set(), Set()));
        Assert.Equal(2, ClassificationScoring.ScoreCandidate(rule, NoBaseKindTokens, Set(), Set("joelho"), Set()));
        Assert.Equal(1, ClassificationScoring.ScoreCandidate(rule, NoBaseKindTokens, Set(), Set(), Set("joelho")));
        // Mesmo hint nos 3 campos soma 3+2+1.
        Assert.Equal(6, ClassificationScoring.ScoreCandidate(rule, NoBaseKindTokens, Set("joelho"), Set("joelho"), Set("joelho")));
        // Sem match -> 0.
        Assert.Equal(0, ClassificationScoring.ScoreCandidate(rule, NoBaseKindTokens, Set("luva"), Set(), Set()));
    }

    [Fact]
    public void ScoreCandidate_dedups_hint_shared_by_baseKindTokens_and_lexicalHints()
    {
        // baseKindTokens[elbow] e LexicalHints ambos com "joelho": conta UMA vez (dedup).
        var baseKindTokens = new Dictionary<string, IReadOnlyList<string>> { ["elbow"] = new[] { "joelho" } };
        ConnectionRule rule = Rule(BaseKind.Elbow, "joelho");

        Assert.Equal(3, ClassificationScoring.ScoreCandidate(rule, baseKindTokens, Set("joelho"), Set(), Set()));
    }

    // ---- bucket ----

    [Theory]
    [InlineData(1.0, ConfidenceBucket.High)]
    [InlineData(0.75, ConfidenceBucket.High)]
    [InlineData(0.7499, ConfidenceBucket.Medium)]
    [InlineData(0.45, ConfidenceBucket.Medium)]
    [InlineData(0.4499, ConfidenceBucket.Low)]
    [InlineData(0.0, ConfidenceBucket.Low)]
    public void ToBucket_thresholds(double score, ConfidenceBucket expected)
        => Assert.Equal(expected, ClassificationScoring.ToBucket(score));

    // ---- confidence ----

    [Fact]
    public void ComputeConfidence_partTypeMatch_strongLexical_is_high()
    {
        ConnectionRule winner = Rule(BaseKind.Elbow, "joelho");
        TopologyReadResult topo = ReadOk("Elbow");
        var lexReasons = new[] { "LexicalHint:joelho@familyName" };

        ClassificationConfidence c = ClassificationScoring.ComputeConfidence(winner, 6, topo, lexReasons);

        // 0.5 + 0.30 (match) + 0.20 (lex 6/6) + 0.05 (native) = 1.05 -> clamp 1.0.
        Assert.Equal(1.0, c.Score, 3);
        Assert.Equal(ConfidenceBucket.High, c.Bucket);
        Assert.Contains("PartTypeMatched:Elbow", c.Reasons);
        Assert.Contains("LexicalHint:joelho@familyName", c.Reasons);
        Assert.Contains("PartTypeNative", c.Reasons);
    }

    [Fact]
    public void ComputeConfidence_undefined_noLexical_is_medium()
    {
        ConnectionRule winner = Rule(BaseKind.Elbow, "joelho");
        TopologyReadResult topo = ReadOk("Undefined");

        ClassificationConfidence c = ClassificationScoring.ComputeConfidence(winner, 0, topo, Array.Empty<string>());

        // 0.5 + 0.20 (inferred) + 0 (lex) + 0 (Undefined nao e native) = 0.70 -> Medium.
        Assert.Equal(0.70, c.Score, 3);
        Assert.Equal(ConfidenceBucket.Medium, c.Bucket);
        Assert.Contains("PartTypeUndefined:InferredFromGeometry", c.Reasons);
    }

    [Fact]
    public void ComputeConfidence_undefined_with_three_warnings_is_low()
    {
        ConnectionRule winner = Rule(BaseKind.Tee, "te");
        TopologyReadResult topo = ReadOk(
            "Undefined",
            Warn(TopologyDiagnosticCode.OriginOutsideExpectedIntersection),
            Warn(TopologyDiagnosticCode.PartTypeMismatchInferred),
            Warn(TopologyDiagnosticCode.ComplexReductionUnclassified));

        ClassificationConfidence c = ClassificationScoring.ComputeConfidence(winner, 0, topo, Array.Empty<string>());

        // 0.5 + 0.20 (inferred) - 0.30 (3 warnings) = 0.40 -> Low.
        Assert.Equal(0.40, c.Score, 3);
        Assert.Equal(ConfidenceBucket.Low, c.Bucket);
        Assert.Equal(3, c.Reasons.Count(r => r.StartsWith("DiagnosticPenalty:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ComputeConfidence_partTypeMismatch_emits_reason()
    {
        // PartType "Elbow" -> hint Elbow; winner Tee -> mismatch (mesmo bonus +0.20).
        ConnectionRule winner = Rule(BaseKind.Tee, "te");
        TopologyReadResult topo = ReadOk("Elbow");

        ClassificationConfidence c = ClassificationScoring.ComputeConfidence(winner, 0, topo, Array.Empty<string>());

        Assert.Contains("PartTypeMismatchInferred:Elbow->Tee", c.Reasons);
    }
}
