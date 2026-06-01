using System;
using System.Collections.Generic;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Nucleo de classificacao (ClassifyCore) end-to-end sobre o rulebook REAL: filtro
/// topologico (2.B-3a) + score lexical + winner + confidence (2.B-3b). Prova alias e
/// negative do JSON, o desempate por RequiresLexicalConfirmation e os fallbacks.
/// </summary>
public class ConnectionRulebookTests
{
    private const string LogicalName = "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

    private static readonly ConnectionRulebook Rulebook =
        new(ConnectionRulebookLoader.LoadEmbedded(typeof(ConnectionRulebookLoader).Assembly, LogicalName));

    private static MepPort Port(PortRole role, int dnMm)
        => new() { Role = role, DnMm = dnMm, Direction = Vector3.UnitX, Origin = Vector3.Zero };

    private static TopologyReadResult ElbowRead(double rawAngle, string partType = "Elbow")
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology
            {
                PartType = partType,
                Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
                AngleMatrix = new IReadOnlyList<double>[] { new[] { 0.0, rawAngle }, new[] { rawAngle, 0.0 } },
                InferredBaseKind = BaseKind.Elbow,
            },
        };

    private static TopologyReadResult TeeRead()
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology
            {
                PartType = "Tee",
                Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50), Port(PortRole.Branch, 50) },
                AngleMatrix = new IReadOnlyList<double>[]
                {
                    new[] { 0.0, 180.0, 90.0 },
                    new[] { 180.0, 0.0, 90.0 },
                    new[] { 90.0, 90.0, 0.0 },
                },
                InferredBaseKind = BaseKind.Tee,
            },
        };

    private static ElementTexts Texts(string family = "", string type = "", string desc = "")
        => new() { FamilyName = family, TypeName = type, Description = desc };

    [Fact]
    public void ClassifyCore_elbow90_picks_elbow90_with_high_confidence()
    {
        RuleMatchResult result = Rulebook.ClassifyCore(ElbowRead(90.0), Texts(family: "Joelho 90"));

        Assert.NotNull(result.Winner);
        Assert.Equal("elbow-90", result.Winner!.Id);
        Assert.Equal(BaseKind.Elbow, result.FallbackBaseKind);
        Assert.Equal(ConfidenceBucket.High, result.Confidence.Bucket);
    }

    [Fact]
    public void ClassifyCore_alias_text_elbow_matches_hint_joelho()
    {
        // Texto "elbow" (ingles) -> via tokenAliases do JSON expande p/ "joelho" -> pontua.
        RuleMatchResult result = Rulebook.ClassifyCore(ElbowRead(90.0, partType: "Undefined"), Texts(family: "elbow"));

        Assert.NotNull(result.Winner);
        Assert.Equal(BaseKind.Elbow, result.Winner!.BaseKind);
        Assert.Contains(result.Confidence.Reasons, r => r.StartsWith("LexicalHint:joelho", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifyCore_negative_token_te_terminal_does_not_score_te()
    {
        // "te terminal": negativeTokens do JSON suprime "te" no texto -> tee nao ganha
        // o ponto lexical de "te".
        RuleMatchResult result = Rulebook.ClassifyCore(TeeRead(), Texts(family: "te terminal"));

        Assert.NotNull(result.Winner);
        Assert.DoesNotContain(result.Confidence.Reasons, r => r.StartsWith("LexicalHint:te@", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifyCore_tie_break_prefers_non_confirmation_subtype()
    {
        // "Joelho": elbow-90 (sem confirmacao) e seus filhos (RequiresLexicalConfirmation)
        // empatam no hint "joelho"; vence o que NAO exige confirmacao = elbow-90.
        RuleMatchResult result = Rulebook.ClassifyCore(ElbowRead(90.0), Texts(family: "Joelho"));

        Assert.Equal("elbow-90", result.Winner!.Id);
        Assert.False(result.Winner.RequiresLexicalConfirmation);
    }

    [Fact]
    public void ClassifyCore_no_matching_rule_falls_back_to_inferred_baseKind()
    {
        // count 2, raw 120: nenhuma faixa casa (nem elbow 85-95/130-140 nem reta 175-185).
        RuleMatchResult result = Rulebook.ClassifyCore(ElbowRead(120.0, partType: "Undefined"), Texts());

        Assert.Null(result.Winner);
        Assert.Equal(BaseKind.Elbow, result.FallbackBaseKind);
        Assert.Equal(ConfidenceBucket.Low, result.Confidence.Bucket);
        Assert.Contains("NoMatchingRule", result.Confidence.Reasons);
    }

    [Fact]
    public void ClassifyCore_topology_read_failed_falls_back_to_unknown()
    {
        var failed = new TopologyReadResult { Success = false, Topology = null };

        RuleMatchResult result = Rulebook.ClassifyCore(failed, Texts());

        Assert.Null(result.Winner);
        Assert.Equal(BaseKind.Unknown, result.FallbackBaseKind);
        Assert.Equal(0.0, result.Confidence.Score, 3);
        Assert.Contains("TopologyReadFailed", result.Confidence.Reasons);
    }

    [Fact]
    public void ClassifyCore_scores_all_candidates_not_just_winner()
    {
        // O ranking completo fica disponivel para auditoria (varios subtipos de elbow
        // sobrevivem ao filtro topologico).
        RuleMatchResult result = Rulebook.ClassifyCore(ElbowRead(90.0), Texts(family: "Joelho 90"));

        Assert.True(result.ScoredCandidates.Count > 1);
        Assert.Contains(result.ScoredCandidates, sc => sc.Rule.Id == "elbow-90");
    }
}
