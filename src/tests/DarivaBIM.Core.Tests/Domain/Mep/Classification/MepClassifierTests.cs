using System;
using System.Collections.Generic;
using System.Numerics;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Ports;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Mep.Classification;

/// <summary>
/// API publica (MepClassifier, secao 13): resolve a disciplina inferida para o rulebook
/// e delega. No MVP 1 so Plumbing tem rulebook; outras disciplinas -> null.
/// </summary>
public class MepClassifierTests
{
    private static MepPort Port(PortRole role, int dnMm)
        => new() { Role = role, DnMm = dnMm, Direction = Vector3.UnitX, Origin = Vector3.Zero };

    private static TopologyReadResult ElbowRead(Discipline discipline)
        => new()
        {
            Success = true,
            Topology = new ConnectionTopology
            {
                PartType = "Elbow",
                InferredBaseKind = BaseKind.Elbow,
                InferredDiscipline = discipline,
                Ports = new[] { Port(PortRole.RunA, 50), Port(PortRole.RunB, 50) },
                AngleMatrix = new IReadOnlyList<double>[] { new[] { 0.0, 90.0 }, new[] { 90.0, 0.0 } },
            },
        };

    private static ElementTexts Texts(string family = "")
        => new() { FamilyName = family };

    [Fact]
    public void CreateDefault_classifies_plumbing_to_non_null_identity()
    {
        MepClassifier classifier = MepClassifier.CreateDefault();

        ConnectionIdentity? id = classifier.Classify(ElbowRead(Discipline.Plumbing), Texts(family: "Joelho 90"));

        Assert.NotNull(id);
        Assert.Equal(BaseKind.Elbow, id!.BaseKind);
        Assert.Equal(Discipline.Plumbing, id.Discipline);
    }

    [Fact]
    public void Classify_discipline_without_rulebook_returns_null()
    {
        MepClassifier classifier = MepClassifier.CreateDefault();

        // Hvac nao tem rulebook no MVP 1 -> null (nao-suportada).
        ConnectionIdentity? id = classifier.Classify(ElbowRead(Discipline.Hvac), Texts());

        Assert.Null(id);
    }

    [Fact]
    public void Classify_topology_read_failed_returns_null_unknown_discipline()
    {
        MepClassifier classifier = MepClassifier.CreateDefault();

        // Success=false -> Topology null -> Discipline.Unknown -> sem rulebook -> null.
        var failed = new TopologyReadResult { Success = false, Topology = null };
        ConnectionIdentity? id = classifier.Classify(failed, Texts());

        Assert.Null(id);
    }
}
