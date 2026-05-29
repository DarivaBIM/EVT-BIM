using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Ports;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// POCO retornado pelo Adapter Revit com o snapshot topologico completo de
    /// uma peca MEP (PartType raw, ports com role atribuido, matrizes
    /// geometricas e veredictos inferidos). PartType e exposto como string e
    /// nao como enum Autodesk.Revit.DB.PartType porque Domain e Revit-agnostic
    /// (ADR-0001). Vide secoes 6 e 8 do rulebook canonico.
    /// </summary>
    public sealed record ConnectionTopology
    {
        /// <summary>
        /// Revit PartType raw como string ("Elbow", "Tee", "Undefined", ...).
        /// String em vez de enum Revit para preservar o isolamento de camada.
        /// </summary>
        public string PartType { get; init; } = "";

        public required IReadOnlyList<MepPort> Ports { get; init; }

        /// <summary>
        /// Matriz NxN onde angle[i,j] e o angulo (graus, 0..180) entre BasisZ
        /// dos conectores i e j. Vide algoritmo da secao 9 do rulebook.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<double>> AngleMatrix { get; init; }
            = Array.Empty<IReadOnlyList<double>>();

        /// <summary>
        /// Matriz NxN com distancias euclidianas entre Origins dos conectores.
        /// Usada em conjunto com AngleMatrix para detectar par anti-paralelo.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<double>> DistanceMatrix { get; init; }
            = Array.Empty<IReadOnlyList<double>>();

        public BaseKind InferredBaseKind { get; init; } = BaseKind.Unknown;

        public Discipline InferredDiscipline { get; init; } = Discipline.Unknown;

        public ProductCategory InferredCategory { get; init; } = ProductCategory.Unknown;

        public ReductionKind ReductionKind { get; init; } = ReductionKind.None;

        /// <summary>
        /// True quando o reader encontrou um par de conectores anti-paralelos
        /// (angulo ~180) com a maior distancia entre Origins, isto e, o eixo
        /// passante de um tee/cross/wye.
        /// </summary>
        public bool IsInlinePairDetected { get; init; }

        public IReadOnlyList<int> AllDns => Ports.Select(p => p.DnMm).ToList();

        public int? RunDn => Ports.FirstOrDefault(p => p.Role == PortRole.RunA)?.DnMm;

        public int? BranchDn => Ports.FirstOrDefault(p => p.Role == PortRole.Branch)?.DnMm;

        /// <summary>
        /// True quando ha reducao. DERIVA de <see cref="ReductionKind"/> (calculado
        /// uma unica vez no engine, ja com a tolerancia de DN aplicada), e NAO de um
        /// Distinct() exato dos DNs — assim fica consistente com ReductionKind e o
        /// BaseKind: uma luva DN 50/51 e Union/None, nunca "Union reduzida".
        /// </summary>
        public bool HasReduction => ReductionKind != ReductionKind.None;
    }
}
