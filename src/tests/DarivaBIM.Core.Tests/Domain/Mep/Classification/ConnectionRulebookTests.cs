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
    public void ClassifyCore_elects_only_non_confirmation_parent()
    {
        // "Joelho" sem gatilho de subtipo: na cam 3 SO os pais nao-confirmaveis sao elegiveis
        // (2.B-4b), entao elbow-90 e eleito e nenhum filho confirmavel e promovido.
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

    // ---- cam 8: Classify -> ConnectionIdentity + granulacoes secao 14 (2.B-5a) ----

    private static TopologyReadResult ValveRead()
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology
            {
                PartType = "ValveNormal",
                InferredBaseKind = BaseKind.Union, // motor infere Union p/ 2-conn inline; o lexico leva a Valve
                InferredDiscipline = Discipline.Plumbing,
                InferredCategory = ProductCategory.PipeAccessory,
                Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
                AngleMatrix = new IReadOnlyList<double>[] { new[] { 0.0, 180.0 }, new[] { 180.0, 0.0 } },
            },
        };

    [Fact]
    public void Classify_elbow90_builds_full_identity()
    {
        ConnectionIdentity id = Rulebook.Classify(ElbowRead(90.0), Texts(family: "Joelho 90"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.NotNull(id.NominalAngleDeg);
        Assert.Equal(90.0, id.NominalAngleDeg!.Value, 3);
        Assert.Equal(2, id.Ports.Count);
        Assert.Equal(ConfidenceBucket.High, id.Confidence.Bucket);
        Assert.Null(id.ValveKind);
        Assert.Equal(ProductLine.Unknown, id.Line); // cam 5 (linha) -> 3.A
    }

    [Fact]
    public void Classify_valve_check_sets_valveKind_check()
    {
        ConnectionIdentity id = Rulebook.Classify(ValveRead(), Texts(family: "Registro Retencao"));

        Assert.Equal(BaseKind.Valve, id.BaseKind);
        Assert.Equal(ValveKind.Check, id.ValveKind);
        Assert.Null(id.InstrumentKind);
    }

    [Fact]
    public void Classify_meter_sets_instrumentKind_flowMeter_and_no_valveKind()
    {
        ConnectionIdentity id = Rulebook.Classify(ValveRead(), Texts(family: "Hidrometro"));

        Assert.Equal(InstrumentKind.FlowMeter, id.InstrumentKind);
        Assert.Null(id.ValveKind);
    }

    [Fact]
    public void Classify_elbow_has_no_valve_or_instrument_granulation()
    {
        ConnectionIdentity id = Rulebook.Classify(ElbowRead(90.0), Texts(family: "Joelho 90"));

        Assert.Null(id.ValveKind);
        Assert.Null(id.InstrumentKind);
    }

    [Fact]
    public void Classify_no_matching_rule_uses_inferred_baseKind_and_low_confidence()
    {
        ConnectionIdentity id = Rulebook.Classify(ElbowRead(120.0, partType: "Undefined"), Texts());

        Assert.Equal(BaseKind.Elbow, id.BaseKind); // = InferredBaseKind (sem winner)
        Assert.Equal(ConfidenceBucket.Low, id.Confidence.Bucket);
        Assert.Null(id.ValveKind);
    }

    [Fact]
    public void Classify_topology_read_failed_is_unknown_with_empty_ports()
    {
        var failed = new TopologyReadResult { Success = false, Topology = null };

        ConnectionIdentity id = Rulebook.Classify(failed, Texts());

        Assert.Equal(BaseKind.Unknown, id.BaseKind);
        Assert.Empty(id.Ports);
    }

    [Fact]
    public void Granulation_covers_every_valve_rule()
    {
        // GUARDRAIL secao 14: toda rule BaseKind=Valve tem ValveKind OU InstrumentKind no
        // mapa, senao a distincao (registro/retencao/hidrometro/manometro) se perderia no
        // ConnectionIdentity.
        foreach (ConnectionRule rule in Rulebook.Document.Rules)
        {
            if (rule.BaseKind != BaseKind.Valve)
            {
                continue;
            }

            bool covered = SubtypeGranulation.ValveKindFor(rule.Id) is not null
                || SubtypeGranulation.InstrumentKindFor(rule.Id) is not null;
            Assert.True(covered, $"Rule Valve '{rule.Id}' sem granulacao (ValveKind/InstrumentKind).");
        }
    }

    // ---- F4 (Codex panoramico): NominalAngleDeg derivado p/ elbow sem angulo fixo ----

    private static TopologyReadResult ElbowReducerRead(double rawAngle)
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology
            {
                PartType = "Elbow",
                InferredBaseKind = BaseKind.Elbow,
                ReductionKind = ReductionKind.Concentric,
                Ports = new[] { Port(PortRole.RunLarge, 50), Port(PortRole.RunSmall, 32) },
                AngleMatrix = new IReadOnlyList<double>[] { new[] { 0.0, rawAngle }, new[] { rawAngle, 0.0 } },
            },
        };

    [Fact]
    public void Classify_elbowReducer_raw135_derives_nominalAngle_45()
    {
        // elbow-reducer NAO tem nominalAngleDeg no JSON -> a cam 8 deriva da geometria:
        // deflexao = 180 - raw 135 = 45.
        ConnectionIdentity id = Rulebook.Classify(ElbowReducerRead(135.0), Texts(family: "Joelho Reducao"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.NotNull(id.NominalAngleDeg);
        Assert.Equal(45.0, id.NominalAngleDeg!.Value, 3);
    }

    [Fact]
    public void Classify_elbowReducer_raw90_derives_nominalAngle_90()
    {
        ConnectionIdentity id = Rulebook.Classify(ElbowReducerRead(90.0), Texts(family: "Joelho Reducao"));

        Assert.NotNull(id.NominalAngleDeg);
        Assert.Equal(90.0, id.NominalAngleDeg!.Value, 3);
    }
}
