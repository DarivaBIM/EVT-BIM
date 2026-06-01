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
/// Enriquecimento da 2.B-4: promocao por disambiguator (cam. 4) e features (cam. 6),
/// contra o rulebook REAL. Cobre promocao validada, bloqueio por mandatory/topologia
/// (com penalidade de ambiguidade) e a deteccao de features lexicais + geometrica.
/// </summary>
public class ClassificationEnrichmentTests
{
    private const string LogicalName = "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

    private static readonly ConnectionRulebookDocument Doc =
        ConnectionRulebookLoader.LoadEmbedded(typeof(ConnectionRulebookLoader).Assembly, LogicalName);

    private static readonly IReadOnlyDictionary<string, ConnectionRule> ById =
        Doc.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);

    private static readonly ConnectionRulebook Rulebook = new(Doc);

    private static MepPort Port(PortRole role, int dnMm)
        => new() { Role = role, DnMm = dnMm, Direction = Vector3.UnitX, Origin = Vector3.Zero };

    private static IReadOnlyList<IReadOnlyList<double>> Matrix(params double[][] rows)
        => Array.ConvertAll(rows, r => (IReadOnlyList<double>)r);

    private static ISet<string> Tokens(params string[] tokens)
        => new HashSet<string>(tokens, StringComparer.Ordinal);

    private static ConnectionTopology Elbow90Topo()
        => new()
        {
            PartType = "Elbow",
            InferredBaseKind = BaseKind.Elbow,
            Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
            AngleMatrix = Matrix(new[] { 0.0, 90.0 }, new[] { 90.0, 0.0 }),
        };

    private static ConnectionTopology TeeTopo(int branchDn, ReductionKind reduction = ReductionKind.None)
        => new()
        {
            PartType = "Tee",
            InferredBaseKind = BaseKind.Tee,
            ReductionKind = reduction,
            Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50), Port(PortRole.Branch, branchDn) },
            AngleMatrix = Matrix(
                new[] { 0.0, 180.0, 90.0 },
                new[] { 180.0, 0.0, 90.0 },
                new[] { 90.0, 90.0, 0.0 }),
        };

    private static TopologyReadResult Read(ConnectionTopology topology)
        => new() { Success = true, Topology = topology };

    private static ElementTexts Texts(string family = "", string type = "", string desc = "")
        => new() { FamilyName = family, TypeName = type, Description = desc };

    // ---- cam 4: PromoteWinner ----

    [Fact]
    public void PromoteWinner_promotes_elbow90_to_threaded_when_rosca_present()
    {
        (ConnectionRule promoted, double penalty, IReadOnlyList<string> reasons) =
            ClassificationEnrichment.PromoteWinner(
                ById["elbow-90"], Tokens("joelho", "rosca"), Elbow90Topo(), Doc.Tolerances, ById);

        Assert.Equal("elbow-threaded", promoted.Id);
        Assert.Equal(0.0, penalty, 3);
        Assert.Contains("DisambiguatorPromoted:rosca->elbow-threaded", reasons);
    }

    [Fact]
    public void PromoteWinner_blocked_by_missing_mandatory_emits_penalty()
    {
        // "bucha" dispara o disambiguator de elbow-brass-bushing, mas o mandatory exige
        // tambem "latao" (AND); ausente -> nao promove, penaliza, sinaliza Unvalidated.
        (ConnectionRule promoted, double penalty, IReadOnlyList<string> reasons) =
            ClassificationEnrichment.PromoteWinner(
                ById["elbow-90"], Tokens("joelho", "bucha"), Elbow90Topo(), Doc.Tolerances, ById);

        Assert.Equal("elbow-90", promoted.Id);
        Assert.Equal(0.10, penalty, 3);
        Assert.Contains("DisambiguatorUnvalidated:bucha", reasons);
    }

    [Fact]
    public void PromoteWinner_blocked_by_incompatible_topology_emits_penalty()
    {
        // "reducao" dispara tee-reducer (topologyMustMatch), mas o branch NAO e menor que
        // o run (50 == 50) -> topologia incompativel -> nao promove, penaliza.
        (ConnectionRule promoted, double penalty, IReadOnlyList<string> reasons) =
            ClassificationEnrichment.PromoteWinner(
                ById["tee"], Tokens("te", "reducao"), TeeTopo(branchDn: 50), Doc.Tolerances, ById);

        Assert.Equal("tee", promoted.Id);
        Assert.Equal(0.10, penalty, 3);
        Assert.Contains("DisambiguatorUnvalidated:reducao", reasons);
    }

    [Fact]
    public void PromoteWinner_no_trigger_keeps_winner_without_penalty()
    {
        (ConnectionRule promoted, double penalty, IReadOnlyList<string> reasons) =
            ClassificationEnrichment.PromoteWinner(
                ById["elbow-90"], Tokens("joelho"), Elbow90Topo(), Doc.Tolerances, ById);

        Assert.Equal("elbow-90", promoted.Id);
        Assert.Equal(0.0, penalty, 3);
        Assert.Empty(reasons);
    }

    // ---- cam 6: DetectFeatures ----

    [Fact]
    public void DetectFeatures_rosca_sets_threadedEnd()
        => Assert.Equal(Feature.ThreadedEnd, ClassificationEnrichment.DetectFeatures(Tokens("rosca"), Elbow90Topo()));

    [Fact]
    public void DetectFeatures_bucha_and_latao_sets_brassBushing_but_bucha_alone_does_not()
    {
        Assert.Equal(Feature.BrassBushing, ClassificationEnrichment.DetectFeatures(Tokens("bucha", "latao"), Elbow90Topo()));
        Assert.Equal(Feature.None, ClassificationEnrichment.DetectFeatures(Tokens("bucha"), Elbow90Topo()));
    }

    [Fact]
    public void DetectFeatures_hasReduction_sets_reduced_geometrically()
    {
        ConnectionTopology reduced = TeeTopo(branchDn: 25, reduction: ReductionKind.BranchOnly);

        Assert.True(ClassificationEnrichment.DetectFeatures(Tokens(), reduced).HasFlag(Feature.Reduced));
    }

    [Fact]
    public void DetectFeatures_combines_lexical_flags()
    {
        Feature features = ClassificationEnrichment.DetectFeatures(Tokens("rosca", "bucha", "latao"), Elbow90Topo());

        Assert.Equal(Feature.ThreadedEnd | Feature.BrassBushing, features);
    }

    // ---- integracao: ClassifyCore ----

    [Fact]
    public void ClassifyCore_detects_threadedEnd_feature_from_rosca()
    {
        // "rosca" e hint EXCLUSIVO de elbow-threaded -> ele vence por SCORE (cam 3), nao
        // por promocao; o que validamos aqui e a deteccao da FEATURE ThreadedEnd (cam 6).
        RuleMatchResult result = Rulebook.ClassifyCore(Read(Elbow90Topo()), Texts(family: "Joelho Rosca"));

        Assert.True(result.Features.HasFlag(Feature.ThreadedEnd));
    }

    [Fact]
    public void ClassifyCore_promotes_to_long_radius_bend_on_shared_token_tie()
    {
        // "curva" esta em baseKindTokens[elbow] (COMPARTILHADO por todos os Elbow), entao
        // elbow-90 e long-radius-bend-90 EMPATAM no score; o pai (non-confirmation) vence o
        // empate e a cam 4 promove para o filho via disambiguator validado.
        RuleMatchResult result = Rulebook.ClassifyCore(Read(Elbow90Topo()), Texts(family: "Curva 90"));

        Assert.Equal("long-radius-bend-90", result.Winner!.Id);
        Assert.Contains(result.Confidence.Reasons, r => r.StartsWith("DisambiguatorPromoted:curva", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifyCore_unvalidated_trigger_reduces_confidence_by_penalty()
    {
        // tee-reducer NAO entra nos candidatos (branch == run, topologia incompativel),
        // entao "reducao" nao muda o score; o winner segue tee. O gatilho "reducao" dispara
        // a promocao, que falha na topologia -> penalidade de ambiguidade de 0.10 (so isso
        // difere do baseline sem o gatilho).
        RuleMatchResult baseline = Rulebook.ClassifyCore(Read(TeeTopo(branchDn: 50)), Texts(family: "Te"));
        RuleMatchResult unvalidated = Rulebook.ClassifyCore(Read(TeeTopo(branchDn: 50)), Texts(family: "Te Reducao"));

        Assert.Equal(0.10, baseline.Confidence.Score - unvalidated.Confidence.Score, 3);
        Assert.Contains(unvalidated.Confidence.Reasons, r => r.StartsWith("DisambiguatorUnvalidated:reducao", StringComparison.Ordinal));
    }
}
