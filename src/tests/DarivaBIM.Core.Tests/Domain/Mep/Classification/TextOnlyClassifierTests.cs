using System;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Modo TEXTO-ONLY conservador (2.B-5b): classifica SO por texto (sem geometria) — o que o
/// migrador de catalogo (3.A) usa nos SKUs Tigre. Prova o cap de confidence (nunca High sem
/// geometria), a promocao validada so por mandatory e o aterrissamento do concern Codex #4
/// (gatilho/ambiguidade sem geometria -> NeedsReview).
/// </summary>
public class TextOnlyClassifierTests
{
    private const string LogicalName = "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

    private static readonly ConnectionRulebook Rulebook =
        new(ConnectionRulebookLoader.LoadEmbedded(typeof(ConnectionRulebookLoader).Assembly, LogicalName));

    private static ElementTexts Texts(string family = "", string type = "", string desc = "")
        => new() { FamilyName = family, TypeName = type, Description = desc };

    [Fact]
    public void TextOnly_joelho_infers_elbow_medium_not_high_with_empty_ports()
    {
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Joelho 45 Soldavel"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.NotEqual(ConfidenceBucket.High, id.Confidence.Bucket); // CAP: sem geometria nunca High
        Assert.Equal(ConfidenceBucket.Medium, id.Confidence.Bucket);
        Assert.Empty(id.Ports);
        Assert.Equal(ProductLine.Unknown, id.Line); // cam 5 (linha) -> 3.A
    }

    [Fact]
    public void TextOnly_registro_retencao_promotes_valve_check_capped_medium()
    {
        // Promocao texto-only: valve-shutoff (pai) -> valve-check via mandatory [retencao],
        // SEM validar topologia. Confidence CAPADO em Medium (nunca High sem geometria).
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Registro Retencao"));

        Assert.Equal(BaseKind.Valve, id.BaseKind);
        Assert.Equal(ValveKind.Check, id.ValveKind);
        Assert.NotEqual(ConfidenceBucket.High, id.Confidence.Bucket);
        Assert.Contains(id.Confidence.Reasons, r => r.StartsWith("DisambiguatorPromoted:retencao", StringComparison.Ordinal));
    }

    [Fact]
    public void TextOnly_registro_esfera_is_valve_shutoff()
    {
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Registro Esfera"));

        Assert.Equal(BaseKind.Valve, id.BaseKind);
        Assert.Equal(ValveKind.Shutoff, id.ValveKind);
    }

    [Fact]
    public void TextOnly_empty_text_is_unknown_needsReview_low()
    {
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts());

        Assert.Equal(BaseKind.Unknown, id.BaseKind);
        Assert.Equal(ConfidenceBucket.Low, id.Confidence.Bucket);
        Assert.Contains("TextOnlyNoBaseKind", id.Confidence.Reasons);
        Assert.Empty(id.Ports);
    }

    [Fact]
    public void TextOnly_joelho_bucha_does_not_become_brassBushing_high()
    {
        // ⭐ Concern Codex #4 (texto-only) pos-2.B-7: "joelho"+"bucha" agora infere Elbow ("bucha"
        // tambem evidencia Elbow via o trigger de elbow-brass-bushing), MAS o winner fica no
        // elbow-45 (pai, sem o disambiguator "bucha") -> NAO promove a brass-bushing nem vira High.
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Joelho Bucha"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.NotEqual(ConfidenceBucket.High, id.Confidence.Bucket);
        Assert.False(id.Features.HasFlag(Feature.BrassBushing)); // "bucha" sem "latao" nao ativa
    }

    [Fact]
    public void TextOnly_te_reducao_infers_tee_not_reducer()
    {
        // F2: sem alias, "te"+"reducao" evidenciam Tee (te + o trigger reducao->tee-reducer) = 2
        // > Reducer (reducao) = 1. Antes (com alias) "redutor" inflava Reducer e dava Reducer.
        Assert.Equal(BaseKind.Tee, Rulebook.ClassifyTextOnly(Texts(family: "Te Reducao")).BaseKind);
    }

    [Fact]
    public void TextOnly_hidrometro_infers_valve_via_disambiguator_trigger()
    {
        // F2: "hidrometro" e trigger de valve-shutoff->meter -> evidencia Valve (antes nao
        // inferia, pois nao esta em baseKindTokens).
        Assert.Equal(BaseKind.Valve, Rulebook.ClassifyTextOnly(Texts(family: "Hidrometro")).BaseKind);
    }

    [Fact]
    public void TextOnly_bucha_alone_is_unknown_ambiguous()
    {
        // "bucha" evidencia Elbow (trigger brass-bushing) E Reducer (baseKindToken) -> empate -> Unknown.
        Assert.Equal(BaseKind.Unknown, Rulebook.ClassifyTextOnly(Texts(family: "Bucha")).BaseKind);
    }

    [Fact]
    public void TextOnly_joelho_90_sets_nominalAngle_90()
    {
        // F1: o angulo do texto desempata os elbows e fixa NominalAngleDeg=90 (antes chutava 45).
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Joelho 90 Soldavel"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.NotNull(id.NominalAngleDeg);
        Assert.Equal(90.0, id.NominalAngleDeg!.Value, 3);
    }

    [Fact]
    public void TextOnly_joelho_45_sets_nominalAngle_45()
    {
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Joelho 45"));

        Assert.NotNull(id.NominalAngleDeg);
        Assert.Equal(45.0, id.NominalAngleDeg!.Value, 3);
    }

    [Fact]
    public void TextOnly_joelho_without_angle_does_not_guess_nominalAngle()
    {
        // F1: sem angulo no texto, NominalAngleDeg fica null (NAO chuta 45).
        ConnectionIdentity id = Rulebook.ClassifyTextOnly(Texts(family: "Joelho Soldavel"));

        Assert.Equal(BaseKind.Elbow, id.BaseKind);
        Assert.Null(id.NominalAngleDeg);
    }

    [Fact]
    public void ComputeTextOnlyConfidence_ambiguous_trigger_is_low_needsReview()
    {
        // Caminho direto do concern #4: gatilho presente mas mandatory falhou (penalty>0)
        // -> NeedsReview (Low), nao so -penalty.
        ClassificationConfidence c = ClassificationScoring.ComputeTextOnlyConfidence(
            BaseKind.Elbow, winnerLexicalScore: 3, subtypePromoted: false,
            disambiguatorPenalty: 0.10, new[] { "DisambiguatorUnvalidated:bucha" });

        Assert.Equal(ConfidenceBucket.Low, c.Bucket);
        Assert.Contains("TextOnlyAmbiguous", c.Reasons);
    }

    [Fact]
    public void ComputeTextOnlyConfidence_caps_below_high_even_at_max_score()
    {
        // CAP: mesmo subtipo promovido com score maximo nao atinge High (>=0.75).
        ClassificationConfidence c = ClassificationScoring.ComputeTextOnlyConfidence(
            BaseKind.Valve, winnerLexicalScore: 99, subtypePromoted: true,
            disambiguatorPenalty: 0.0, Array.Empty<string>());

        Assert.True(c.Score < 0.75);
        Assert.NotEqual(ConfidenceBucket.High, c.Bucket);
    }
}
