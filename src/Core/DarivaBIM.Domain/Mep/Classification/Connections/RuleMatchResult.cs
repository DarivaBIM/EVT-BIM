using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Um candidato sobrevivente ao filtro topologico (2.B-3a) com seu score lexical
    /// (cam. 3 do secao 21). Imutavel; agrega regra + pontuacao para o caller auditar
    /// o ranking completo, nao so o vencedor.
    /// </summary>
    public sealed record ScoredCandidate
    {
        public required ConnectionRule Rule { get; init; }

        public required int LexicalScore { get; init; }
    }

    /// <summary>
    /// Resultado INTERMEDIARIO do nucleo de classificacao (2.B-3b): o vencedor por
    /// score lexical, todos os candidatos pontuados e o confidence (cam. 7, secao 16).
    /// NAO e a identidade final — a 2.B-4 (disambiguators/linha/features) e a 2.B-5
    /// (ConnectionIdentity/MepClassifier) enriquecem a partir daqui. <see cref="Winner"/>
    /// null = nenhuma regra casou; <see cref="FallbackBaseKind"/> carrega o BaseKind
    /// inferido pela geometria para o caller degradar com elegancia.
    /// </summary>
    public sealed record RuleMatchResult
    {
        public ConnectionRule? Winner { get; init; }

        public IReadOnlyList<ScoredCandidate> ScoredCandidates { get; init; }
            = Array.Empty<ScoredCandidate>();

        public required ClassificationConfidence Confidence { get; init; }

        public BaseKind FallbackBaseKind { get; init; } = BaseKind.Unknown;
    }
}
