using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Lexical;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Score lexical (cam. 3 do secao 21) e confidence (cam. 7, secao 16.2) do
    /// classificador MEP. PURO (Domain). Os pesos e limiares sao CALIBRACAO inicial
    /// DOCUMENTADA (constantes nomeadas, nunca numeros magicos soltos), marcados
    /// [AJUSTAR NO SMOKE] da Fase 4.
    /// </summary>
    public static class ClassificationScoring
    {
        // Pesos do score lexical (cam. 3): FamilyName x3 + TypeName x2 + Description x1.
        // Um hint que aparece em varios campos SOMA os pesos (evidencia multi-campo).
        private const int FamilyWeight = 3;
        private const int TypeWeight = 2;
        private const int DescriptionWeight = 1;

        // Calibracao do confidence (cam. 7, secao 16.2). [AJUSTAR NO SMOKE]
        private const double BaseScore = 0.5;
        private const double TopologyMatchBonus = 0.30;     // PartType nativo == winner.BaseKind
        private const double TopologyInferredBonus = 0.20;  // PartType ausente OU divergente
        private const double LexicalWeight = 0.20;          // teto do bonus lexical
        private const double LexicalSaturation = 6.0;       // score >= 6 satura (hint em family+type+desc)
        private const double PartTypeNativeBonus = 0.05;    // PartType nao Undefined/Other
        private const double WarningPenalty = 0.10;         // por diagnostic Warning
        private const double InfoPenalty = 0.0;             // Info nao penaliza

        // Limiares do bucket (secao 16): identicos ao XML-doc de ClassificationConfidence.
        private const double HighThreshold = 0.75;
        private const double MediumThreshold = 0.45;

        // Calibracao do confidence TEXTO-ONLY (2.B-5b) — conservador, SEM topologyBonus.
        // [AJUSTAR NO SMOKE] O cap garante que sem geometria nunca se atinge High.
        private const double TextOnlyNoBaseKindScore = 0.20; // BaseKind nao inferido -> NeedsReview
        private const double TextOnlyAmbiguousScore = 0.30;  // gatilho sem mandatory (Codex #4) -> NeedsReview
        private const double TextOnlyBaseScore = 0.45;       // BaseKind inferido por texto
        private const double TextOnlyLexicalWeight = 0.10;   // teto do bonus lexical (< geometrico)
        private const double TextOnlySubtypeBonus = 0.18;    // subtipo promovido por mandatory inequivoco
        private const double TextOnlyConfidenceCap = 0.70;   // CAP: nunca High (>=0.75) sem geometria

        // Hints normalizados via o MESMO Tokenize do texto, mas SEM expandir alias nem
        // remover negatives: o hint E o token canonico. A expansao de sinonimo acontece
        // so no TEXTO (casar texto-expandido vs hint-canonico ja cobre os sinonimos); os
        // negatives so suprimem tokens do TEXTO, nunca o proprio hint. Reuso total da
        // tecnica unicode (strip-acento + lower) sem duplica-la aqui.
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoNegatives
            = new Dictionary<string, IReadOnlyList<string>>();

        private static readonly TokenizerOptions HintTokenizerOptions = new()
        {
            ExpandAliases = false,
            NegativeTokens = NoNegatives,
        };

        /// <summary>
        /// Score lexical do candidato: para cada hint efetivo, soma o peso de CADA campo
        /// onde o hint aparece como TOKEN (family 3 + type 2 + desc 1). Match e por token
        /// de conjunto, nunca substring ("te" nao casa "terminal" — garantido pela
        /// tokenizacao a montante).
        /// </summary>
        public static int ScoreCandidate(
            ConnectionRule candidate,
            IReadOnlyDictionary<string, IReadOnlyList<string>> baseKindTokens,
            ISet<string> familyTokens,
            ISet<string> typeTokens,
            ISet<string> descTokens)
        {
            int score = 0;
            foreach (string hint in EffectiveHints(candidate, baseKindTokens))
            {
                if (familyTokens.Contains(hint))
                {
                    score += FamilyWeight;
                }

                if (typeTokens.Contains(hint))
                {
                    score += TypeWeight;
                }

                if (descTokens.Contains(hint))
                {
                    score += DescriptionWeight;
                }
            }

            return score;
        }

        /// <summary>
        /// Reasons LexicalHint:{hint}@{campo} do VENCEDOR (so o winner os gera, secao
        /// 16.3) — um por (hint, campo) onde houve match.
        /// </summary>
        public static IReadOnlyList<string> CollectLexicalReasons(
            ConnectionRule winner,
            IReadOnlyDictionary<string, IReadOnlyList<string>> baseKindTokens,
            ISet<string> familyTokens,
            ISet<string> typeTokens,
            ISet<string> descTokens)
        {
            var reasons = new List<string>();
            foreach (string hint in EffectiveHints(winner, baseKindTokens))
            {
                if (familyTokens.Contains(hint))
                {
                    reasons.Add($"LexicalHint:{hint}@familyName");
                }

                if (typeTokens.Contains(hint))
                {
                    reasons.Add($"LexicalHint:{hint}@typeName");
                }

                if (descTokens.Contains(hint))
                {
                    reasons.Add($"LexicalHint:{hint}@description");
                }
            }

            return reasons;
        }

        /// <summary>
        /// Confidence (cam. 7, secao 16.2). O caller garante topo.Success + Topology
        /// nao-null. <paramref name="lineBonus"/> e <paramref name="disambiguatorPenalty"/>
        /// entram em 0 nesta fase; a 2.B-4 os preenche SEM mudar a assinatura.
        /// </summary>
        public static ClassificationConfidence ComputeConfidence(
            ConnectionRule winner,
            int winnerLexicalScore,
            TopologyReadResult topo,
            IReadOnlyList<string> lexicalReasons,
            double lineBonus = 0.0,
            double disambiguatorPenalty = 0.0)
        {
            var reasons = new List<string>();
            double score = BaseScore;

            ConnectionTopology topology = topo.Topology!;

            // topologyBonus: PartType nativo confirma o BaseKind (+0.30) ou a geometria
            // assume (PartType ausente OU divergente: +0.20 — o motor ja deu o veto, D7).
            BaseKind? hint = PartTypeHints.ToBaseKindHint(topology.PartType);
            if (hint == winner.BaseKind)
            {
                score += TopologyMatchBonus;
                reasons.Add($"PartTypeMatched:{winner.BaseKind}");
            }
            else if (hint is null)
            {
                score += TopologyInferredBonus;
                reasons.Add("PartTypeUndefined:InferredFromGeometry");
            }
            else
            {
                score += TopologyInferredBonus;
                reasons.Add($"PartTypeMismatchInferred:{hint}->{winner.BaseKind}");
            }

            // lexicalScoreNormalized [0, LexicalWeight]: min(score/SAT, 1) * teto.
            double lexicalNormalized = Math.Min(winnerLexicalScore / LexicalSaturation, 1.0) * LexicalWeight;
            score += lexicalNormalized;
            reasons.AddRange(lexicalReasons);

            // partTypeNativeBonus.
            if (HasNativePartType(topology.PartType))
            {
                score += PartTypeNativeBonus;
                reasons.Add("PartTypeNative");
            }

            // diagnosticsPenalty (Warning/Info; Error nao chega aqui — vira !Success).
            foreach (TopologyDiagnostic diagnostic in topo.Diagnostics)
            {
                double penalty = DiagnosticPenalty(diagnostic.Severity);
                if (penalty != 0.0)
                {
                    score -= penalty;
                    reasons.Add($"DiagnosticPenalty:{diagnostic.Code}");
                }
            }

            // lineBonus / disambiguatorPenalty: 2.B-4 (default 0 aqui).
            score += lineBonus;
            score -= disambiguatorPenalty;

            score = Clamp01(score);
            return new ClassificationConfidence
            {
                Score = score,
                Bucket = ToBucket(score),
                Reasons = reasons,
            };
        }

        /// <summary>Bucket discreto do score (secao 16): High &gt;= 0.75, Medium &gt;= 0.45, else Low.</summary>
        public static ConfidenceBucket ToBucket(double score)
        {
            if (score >= HighThreshold)
            {
                return ConfidenceBucket.High;
            }

            if (score >= MediumThreshold)
            {
                return ConfidenceBucket.Medium;
            }

            return ConfidenceBucket.Low;
        }

        /// <summary>
        /// Confidence do modo TEXTO-ONLY (2.B-5b) — conservador, SEM topologyBonus e CAPADO
        /// (nunca High sem geometria). Onde aterrissa o concern Codex #4: gatilho de subtipo
        /// presente mas mandatory NAO validado (<paramref name="disambiguatorPenalty"/> &gt; 0)
        /// rebaixa p/ NeedsReview (Low), nao so -penalty — SKU errado custa dinheiro e sem
        /// geometria nao ha como contrabalancar.
        /// </summary>
        public static ClassificationConfidence ComputeTextOnlyConfidence(
            BaseKind inferredBaseKind,
            int winnerLexicalScore,
            bool subtypePromoted,
            double disambiguatorPenalty,
            IReadOnlyList<string> lexicalReasons)
        {
            // Sem BaseKind inferido -> NeedsReview.
            if (inferredBaseKind == BaseKind.Unknown)
            {
                return TextOnly(TextOnlyNoBaseKindScore, "TextOnlyNoBaseKind", lexicalReasons);
            }

            // Concern Codex #4: gatilho disparou mas nao validou -> NeedsReview.
            if (disambiguatorPenalty > 0.0)
            {
                return TextOnly(TextOnlyAmbiguousScore, "TextOnlyAmbiguous", lexicalReasons);
            }

            double score = TextOnlyBaseScore
                + (Math.Min(winnerLexicalScore / LexicalSaturation, 1.0) * TextOnlyLexicalWeight);

            if (subtypePromoted)
            {
                score += TextOnlySubtypeBonus;
            }

            score = Clamp01(Math.Min(score, TextOnlyConfidenceCap));
            return new ClassificationConfidence
            {
                Score = score,
                Bucket = ToBucket(score),
                Reasons = new List<string>(lexicalReasons),
            };
        }

        private static ClassificationConfidence TextOnly(double score, string code, IReadOnlyList<string> extra)
        {
            var reasons = new List<string> { code };
            reasons.AddRange(extra);
            return new ClassificationConfidence
            {
                Score = score,
                Bucket = ToBucket(score),
                Reasons = reasons,
            };
        }

        // Hints efetivos = baseKindTokens[BaseKind] UNIAO LexicalHints, normalizados e
        // deduplicados. "MultiPort".ToLowerInvariant() = "multiport" casa a key do JSON.
        private static IReadOnlyList<string> EffectiveHints(
            ConnectionRule candidate,
            IReadOnlyDictionary<string, IReadOnlyList<string>> baseKindTokens)
        {
            var hints = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            string key = candidate.BaseKind.ToString().ToLowerInvariant();
            if (baseKindTokens.TryGetValue(key, out IReadOnlyList<string>? kindTokens))
            {
                foreach (string token in kindTokens)
                {
                    AddNormalizedHint(hints, seen, token);
                }
            }

            foreach (string lexicalHint in candidate.LexicalHints)
            {
                AddNormalizedHint(hints, seen, lexicalHint);
            }

            return hints;
        }

        private static void AddNormalizedHint(List<string> hints, HashSet<string> seen, string raw)
        {
            foreach (string token in LexicalNormalizer.Tokenize(raw, HintTokenizerOptions))
            {
                if (seen.Add(token))
                {
                    hints.Add(token);
                }
            }
        }

        private static double DiagnosticPenalty(DiagnosticSeverity severity)
            => severity switch
            {
                DiagnosticSeverity.Warning => WarningPenalty,
                _ => InfoPenalty, // Info (e Error, que nao chega aqui)
            };

        private static bool HasNativePartType(string partType)
        {
            string trimmed = partType.Trim();
            return trimmed.Length > 0
                && !trimmed.Equals("Undefined", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("Other", StringComparison.OrdinalIgnoreCase);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }
    }
}
