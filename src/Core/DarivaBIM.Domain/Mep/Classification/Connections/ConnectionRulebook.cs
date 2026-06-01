using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Lexical;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Fachada do rulebook de conexoes (fase 2.B). Envolve um
    /// <see cref="ConnectionRulebookDocument"/> ja carregado/validado e orquestra o
    /// nucleo de classificacao: filtro topologico (2.B-3a, <see cref="TopologyMatcher"/>)
    /// -> score lexical + winner + confidence (2.B-3b, <see cref="ClassificationScoring"/>).
    /// PARCIAL — disambiguators/linha/features (2.B-4) e a API publica por disciplina +
    /// ConnectionIdentity (2.B-5) entram depois.
    /// </summary>
    public sealed class ConnectionRulebook
    {
        private const string EmbeddedResourceName =
            "DarivaBIM.Domain.Mep.Classification.Resources.pipe_connection_rules.json";

        private readonly ConnectionRulebookDocument _doc;

        // Indice id -> regra, construido UMA vez (reusado pela promocao da 2.B-4). O
        // loader ja garantiu IDs unicos, entao a indexacao nao colide.
        private readonly IReadOnlyDictionary<string, ConnectionRule> _byId;

        public ConnectionRulebook(ConnectionRulebookDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _byId = BuildIndex(doc.Rules);
        }

        public ConnectionRulebookDocument Document => _doc;

        /// <summary>Carrega o rulebook embarcado de producao (pipe_connection_rules.json).</summary>
        public static ConnectionRulebook FromEmbedded()
            => new(ConnectionRulebookLoader.LoadEmbedded(
                typeof(ConnectionRulebook).Assembly, EmbeddedResourceName));

        /// <summary>
        /// Nucleo da classificacao (cam. 1-3, 7): filtra candidatos por topologia,
        /// pontua por texto, elege o vencedor e calcula o confidence. Resultado
        /// INTERMEDIARIO (sem disambiguators/identity). NUNCA lanca: leitura invalida
        /// ou ausencia de candidato degradam para fallback.
        /// </summary>
        public RuleMatchResult ClassifyCore(TopologyReadResult topo, ElementTexts texts)
        {
            // Leitura topologica invalida (sem sucesso ou sem topology) -> nada a classificar.
            if (topo is null || !topo.Success || topo.Topology is null)
            {
                return Fallback(BaseKind.Unknown, 0.0, "TopologyReadFailed");
            }

            IReadOnlyList<ConnectionRule> candidates = TopologyMatcher.FilterCandidates(_doc, topo.Topology);
            if (candidates.Count == 0)
            {
                // Geometria leu, mas nenhuma regra casou: degrada para o BaseKind inferido
                // pelo motor (o caller decide o que fazer — provavel NeedsReview na 2.B-5).
                return Fallback(topo.Topology.InferredBaseKind, 0.3, "NoMatchingRule");
            }

            ElementTexts safeTexts = texts ?? new ElementTexts();

            // ⚠️ Passa os dicionarios do JSON (aliases/negatives do rulebook), NAO os
            // defaults da 2.A. Tokeniza UMA vez cada campo; o Tokenize ja expande aliases
            // e remove negatives nos tokens do TEXTO.
            var opts = new TokenizerOptions
            {
                Aliases = _doc.TokenAliases,
                NegativeTokens = _doc.NegativeTokens,
            };

            ISet<string> familyTokens = TokenSet(safeTexts.FamilyName, opts);
            ISet<string> typeTokens = TokenSet(safeTexts.TypeName, opts);
            ISet<string> descTokens = TokenSet(safeTexts.Description, opts);

            var scored = new List<ScoredCandidate>(candidates.Count);
            foreach (ConnectionRule candidate in candidates)
            {
                int lexical = ClassificationScoring.ScoreCandidate(
                    candidate, _doc.BaseKindTokens, familyTokens, typeTokens, descTokens);
                scored.Add(new ScoredCandidate { Rule = candidate, LexicalScore = lexical });
            }

            ScoredCandidate winner = SelectWinner(scored);

            // 2.B-4 (cam. 4 + 6): promove o winner a um subtipo via disambiguator e
            // detecta features. allTokens = presenca basta (nao ponderado).
            var allTokens = new HashSet<string>(familyTokens, StringComparer.Ordinal);
            allTokens.UnionWith(typeTokens);
            allTokens.UnionWith(descTokens);

            (ConnectionRule promoted, double disambiguatorPenalty, IReadOnlyList<string> promotionReasons) =
                ClassificationEnrichment.PromoteWinner(
                    winner.Rule, allTokens, topo.Topology, _doc.Tolerances, _byId);

            Feature features = ClassificationEnrichment.DetectFeatures(allTokens, topo.Topology);

            // Reasons lexicais do PAI (o match que elegeu o winner) + o registro da promocao.
            var reasons = new List<string>(ClassificationScoring.CollectLexicalReasons(
                winner.Rule, _doc.BaseKindTokens, familyTokens, typeTokens, descTokens));
            reasons.AddRange(promotionReasons);

            // Score lexical do PAI: a promocao muda a IDENTIDADE, nao o match do texto.
            ClassificationConfidence confidence = ClassificationScoring.ComputeConfidence(
                promoted, winner.LexicalScore, topo, reasons,
                lineBonus: 0.0, disambiguatorPenalty: disambiguatorPenalty);

            return new RuleMatchResult
            {
                Winner = promoted,
                ScoredCandidates = scored,
                Confidence = confidence,
                FallbackBaseKind = promoted.BaseKind,
                Features = features,
            };
        }

        private static IReadOnlyDictionary<string, ConnectionRule> BuildIndex(IReadOnlyList<ConnectionRule> rules)
        {
            var byId = new Dictionary<string, ConnectionRule>(StringComparer.Ordinal);
            foreach (ConnectionRule rule in rules)
            {
                byId[rule.Id] = rule;
            }

            return byId;
        }

        private static ISet<string> TokenSet(string text, TokenizerOptions opts)
            => new HashSet<string>(LexicalNormalizer.Tokenize(text, opts), StringComparer.Ordinal);

        // Maior score vence. Empate -> prefere quem NAO exige confirmacao lexical (subtipo
        // canonico sobre a variante que precisa de gatilho); persistindo o empate, mantem
        // o primeiro (FilterCandidates ja retorna em ordem do JSON).
        private static ScoredCandidate SelectWinner(IReadOnlyList<ScoredCandidate> scored)
        {
            ScoredCandidate best = scored[0];
            for (int i = 1; i < scored.Count; i++)
            {
                if (IsBetter(scored[i], best))
                {
                    best = scored[i];
                }
            }

            return best;
        }

        private static bool IsBetter(ScoredCandidate candidate, ScoredCandidate best)
        {
            if (candidate.LexicalScore != best.LexicalScore)
            {
                return candidate.LexicalScore > best.LexicalScore;
            }

            return !candidate.Rule.RequiresLexicalConfirmation && best.Rule.RequiresLexicalConfirmation;
        }

        private static RuleMatchResult Fallback(BaseKind fallbackBaseKind, double score, string reason)
            => new()
            {
                Winner = null,
                FallbackBaseKind = fallbackBaseKind,
                Confidence = new ClassificationConfidence
                {
                    Score = score,
                    Bucket = ClassificationScoring.ToBucket(score),
                    Reasons = new[] { reason },
                },
            };
    }
}
