using System;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// Tests do ConnectionRulebookLoader (2.B-1) com fixtures JSON minimos inline.
/// Cobrem parse -> POCOs, deep-merge de inherits, shortcut de diameterRule e as
/// validacoes (ciclo / Id duplicado / inherits e promoteTo orfaos).
/// </summary>
public class ConnectionRulebookLoaderTests
{
    private const string MinimalJson = """
    {
      "version": "2.0",
      "discipline": "Plumbing",
      "baseKindTokens": { "elbow": ["joelho", "curva"] },
      "tokenAliases": { "te": ["tee"] },
      "negativeTokens": { "te": ["terminal"] },
      "tolerances": { "angleDeg": 5, "diameterMm": 2 },
      "rules": [
        {
          "id": "elbow-90",
          "baseKind": "Elbow",
          "geometryKind": "ShortRadius",
          "nominalAngleDeg": 90,
          "topology": {
            "partTypeAccepts": ["Elbow", "Other"],
            "connectorCount": 2,
            "diameterRule": { "mode": "roles", "constraints": [ { "ports": ["RunA", "RunB"], "relation": "equal" } ] },
            "primaryAngleRule": { "minDeg": 85, "maxDeg": 95 }
          },
          "lexicalDisambiguators": [
            { "trigger": "rosca", "promoteTo": "elbow-threaded", "mandatoryLexical": ["rosca"], "topologyMustMatch": true }
          ],
          "lexicalHints": ["joelho"],
          "requiresLexicalConfirmation": false
        },
        {
          "id": "elbow-threaded",
          "baseKind": "Elbow",
          "topology": { "inherits": "elbow-90" }
        }
      ]
    }
    """;

    [Fact]
    public void Parses_minimal_document_into_pocos()
    {
        ConnectionRulebookDocument doc = ConnectionRulebookLoader.Load(MinimalJson);

        Assert.Equal("2.0", doc.Version);
        Assert.Equal("Plumbing", doc.Discipline);
        Assert.Equal(new[] { "joelho", "curva" }, doc.BaseKindTokens["elbow"]);
        Assert.Equal(5, doc.Tolerances.AngleDeg);
        Assert.Equal(2, doc.Rules.Count);

        ConnectionRule elbow = doc.Rules.First(r => r.Id == "elbow-90");
        Assert.Equal(BaseKind.Elbow, elbow.BaseKind);
        Assert.Equal(GeometryKind.ShortRadius, elbow.GeometryKind);
        Assert.Equal(90, elbow.NominalAngleDeg);
        Assert.Equal(2, elbow.Topology.ConnectorCount);
        Assert.Equal(new[] { "Elbow", "Other" }, elbow.Topology.PartTypeAccepts);
        Assert.Equal(85, elbow.Topology.PrimaryAngleRule!.MinDeg);

        DiameterConstraint constraint = Assert.Single(elbow.Topology.DiameterRule!.Constraints);
        Assert.Equal(new[] { PortRole.RunA, PortRole.RunB }, constraint.Ports);
        Assert.Equal(DiameterRelation.Equal, constraint.Relation);

        LexicalDisambiguator disambiguator = Assert.Single(elbow.LexicalDisambiguators);
        Assert.Equal("rosca", disambiguator.Trigger);
        Assert.Equal("elbow-threaded", disambiguator.PromoteTo);
        Assert.Equal(new[] { "rosca" }, disambiguator.MandatoryLexical);
        Assert.True(disambiguator.TopologyMustMatch);
        Assert.Equal(new[] { "joelho" }, elbow.LexicalHints);
    }

    [Fact]
    public void Inherits_resolves_child_topology_from_parent()
    {
        ConnectionRulebookDocument doc = ConnectionRulebookLoader.Load(MinimalJson);

        ConnectionRule threaded = doc.Rules.First(r => r.Id == "elbow-threaded");

        // Resolvido: herdou a topologia do elbow-90 e nao tem inherits pendente.
        Assert.Null(threaded.Topology.Inherits);
        Assert.Equal(2, threaded.Topology.ConnectorCount);
        Assert.Equal(85, threaded.Topology.PrimaryAngleRule!.MinDeg);
        Assert.NotNull(threaded.Topology.DiameterRule);
    }

    [Fact]
    public void Inherits_with_overrides_merges_deeply()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [
            {
              "id": "tee",
              "baseKind": "Tee",
              "topology": {
                "connectorCount": 3,
                "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
                "diameterRule": { "mode": "roles", "constraints": [ { "ports": ["RunA", "RunB"], "relation": "equal" } ] }
              }
            },
            {
              "id": "tee-reducer",
              "baseKind": "Tee",
              "topology": {
                "inherits": "tee",
                "overrides": {
                  "diameterRule": { "mode": "roles", "constraints": [ { "ports": ["Branch"], "relation": "lessThan", "target": "RunA" } ] }
                }
              }
            }
          ]
        }
        """;

        ConnectionRulebookDocument doc = ConnectionRulebookLoader.Load(json);
        ConnectionRule reducer = doc.Rules.First(r => r.Id == "tee-reducer");

        // Herdado do pai (nao estava no override):
        Assert.Equal(3, reducer.Topology.ConnectorCount);
        Assert.Equal(175, reducer.Topology.PrimaryAngleRule!.MinDeg);
        // Sobrescrito pelo override:
        DiameterConstraint constraint = Assert.Single(reducer.Topology.DiameterRule!.Constraints);
        Assert.Equal(DiameterRelation.LessThan, constraint.Relation);
        Assert.Equal(new[] { PortRole.Branch }, constraint.Ports);
        Assert.Equal("RunA", constraint.Target);
    }

    [Fact]
    public void DiameterRule_string_shortcut_expands_to_canonical_object()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [ { "id": "union", "baseKind": "Union", "topology": { "diameterRule": "equal" } } ]
        }
        """;

        ConnectionRulebookDocument doc = ConnectionRulebookLoader.Load(json);
        DiameterRule rule = doc.Rules.Single().Topology.DiameterRule!;

        Assert.Equal("roles", rule.Mode);
        DiameterConstraint constraint = Assert.Single(rule.Constraints);
        Assert.Equal(DiameterRelation.Equal, constraint.Relation);
        Assert.Empty(constraint.Ports);
    }

    [Fact]
    public void Cyclic_inherits_throws()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [
            { "id": "a", "baseKind": "Elbow", "topology": { "inherits": "b" } },
            { "id": "b", "baseKind": "Elbow", "topology": { "inherits": "a" } }
          ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ConnectionRulebookLoader.Load(json));
    }

    [Fact]
    public void Duplicate_id_throws()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [
            { "id": "x", "baseKind": "Elbow", "topology": {} },
            { "id": "x", "baseKind": "Tee", "topology": {} }
          ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ConnectionRulebookLoader.Load(json));
    }

    [Fact]
    public void Orphan_inherits_throws()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [ { "id": "a", "baseKind": "Elbow", "topology": { "inherits": "ghost" } } ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ConnectionRulebookLoader.Load(json));
    }

    [Fact]
    public void Orphan_promoteTo_throws()
    {
        const string json = """
        {
          "version": "2.0",
          "discipline": "Plumbing",
          "rules": [
            {
              "id": "a",
              "baseKind": "Elbow",
              "topology": {},
              "lexicalDisambiguators": [ { "trigger": "x", "promoteTo": "ghost" } ]
            }
          ]
        }
        """;

        Assert.Throws<InvalidOperationException>(() => ConnectionRulebookLoader.Load(json));
    }
}
