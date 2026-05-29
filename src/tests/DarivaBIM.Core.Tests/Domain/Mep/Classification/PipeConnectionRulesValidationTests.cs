using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Validacao do pipe_connection_rules.json REAL (2.B-2) carregado via
/// LoadEmbedded. Os tests sao a rede contra erro humano no JSON: o loader cai em
/// sentinel (Unknown) silenciosamente em typo de baseKind/relation, entao aqui
/// reforcamos que NENHUM enum caiu em sentinel (gate da 2.B-1).
/// </summary>
public class PipeConnectionRulesValidationTests
{
    private const string LogicalName = "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

    private static readonly string[] ExpectedIds =
    {
        "cap",
        "union-simple", "union-threaded",
        "reducer-concentric", "reducer-eccentric", "bushing", "adapter", "male-female-connector",
        "elbow-45", "elbow-90", "elbow-reducer", "elbow-threaded", "elbow-brass-bushing", "elbow-visit",
        "long-radius-bend-45", "long-radius-bend-90", "transposition-curve",
        "tee", "tee-reducer", "tee-inspection", "tee-misturador", "tee-threaded",
        "wye-simple", "wye-reducer", "wye-inverted", "wye-double",
        "cross",
        "valve-shutoff", "valve-check", "valve-prv", "valve-flush", "meter", "instrument-pressure",
        "manifold",
    };

    private static ConnectionRulebookDocument Load()
        => ConnectionRulebookLoader.LoadEmbedded(typeof(ConnectionRulebookLoader).Assembly, LogicalName);

    [Fact]
    public void Loads_embedded_json_without_exception()
    {
        // O proprio Load valida IDs unicos, inherits/promoteTo orfaos e ciclo —
        // qualquer um desses faria este teste lancar.
        ConnectionRulebookDocument doc = Load();

        Assert.Equal("2.0", doc.Version);
        Assert.Equal("Plumbing", doc.Discipline);
        Assert.NotEmpty(doc.Rules);
    }

    [Fact]
    public void All_section18_subtypes_present_and_no_extras()
    {
        HashSet<string> actual = Load().Rules.Select(r => r.Id).ToHashSet();

        foreach (string id in ExpectedIds)
        {
            Assert.Contains(id, actual);
        }

        Assert.Equal(ExpectedIds.Length, actual.Count);
    }

    [Fact]
    public void All_nine_baseKinds_are_covered()
    {
        HashSet<BaseKind> kinds = Load().Rules.Select(r => r.BaseKind).ToHashSet();

        foreach (BaseKind expected in new[]
        {
            BaseKind.Elbow, BaseKind.Tee, BaseKind.Wye, BaseKind.Cross, BaseKind.Union,
            BaseKind.Reducer, BaseKind.Cap, BaseKind.Valve, BaseKind.MultiPort,
        })
        {
            Assert.Contains(expected, kinds);
        }
    }

    [Fact]
    public void No_enum_falls_into_sentinel()
    {
        // GATE da 2.B-1: typo no JSON cairia em Unknown (baseKind) ou Unknown
        // (relation) / port skipado, que o loader nao acusa. Aqui pegamos.
        ConnectionRulebookDocument doc = Load();

        foreach (ConnectionRule rule in doc.Rules)
        {
            Assert.NotEqual(BaseKind.Unknown, rule.BaseKind);

            if (rule.Topology.DiameterRule is null)
            {
                continue;
            }

            foreach (DiameterConstraint constraint in rule.Topology.DiameterRule.Constraints)
            {
                Assert.NotEqual(DiameterRelation.Unknown, constraint.Relation);

                // Relacao que compara ports exige ports parseados (typo num PortRole
                // seria skipado pelo converter e esvaziaria a lista).
                if (constraint.Relation != DiameterRelation.Any
                    && constraint.Relation != DiameterRelation.Single)
                {
                    Assert.NotEmpty(constraint.Ports);
                }
            }
        }
    }

    [Fact]
    public void All_promoteTo_reference_existing_ids()
    {
        // inherits orfao ja e barrado no Load; aqui reforcamos promoteTo no JSON real.
        ConnectionRulebookDocument doc = Load();
        HashSet<string> ids = doc.Rules.Select(r => r.Id).ToHashSet();

        foreach (ConnectionRule rule in doc.Rules)
        {
            foreach (LexicalDisambiguator disambiguator in rule.LexicalDisambiguators)
            {
                Assert.Contains(disambiguator.PromoteTo, ids);
            }
        }
    }
}
