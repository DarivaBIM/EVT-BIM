using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Lexical;
using DarivaBIM.Domain.Mep.Classification.Ports;

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
    public sealed class ConnectionRulebook : IConnectionRulebook
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

        /// <summary>
        /// API publica (cam. 8 do secao 21): classifica num <see cref="ConnectionIdentity"/>
        /// canonico. Resolve disciplina/categoria do motor, monta a identidade facetada a
        /// partir do <see cref="ClassifyCore"/> e popula as granulacoes (secao 14). SEMPRE
        /// devolve uma identidade (os 3 caminhos do core cobrem full / NoMatchingRule /
        /// TopologyReadFailed). Linha (cam. 5) fica Unknown — vem do catalogo na fase 3.A.
        /// </summary>
        public ConnectionIdentity Classify(TopologyReadResult topo, ElementTexts texts)
        {
            RuleMatchResult core = ClassifyCore(topo, texts);
            ConnectionTopology? topology = topo?.Topology;

            return new ConnectionIdentity
            {
                Discipline = topology?.InferredDiscipline ?? Discipline.Unknown,
                Category = topology?.InferredCategory ?? ProductCategory.Unknown,
                BaseKind = core.Winner?.BaseKind ?? core.FallbackBaseKind,
                GeometryKind = core.Winner?.GeometryKind ?? GeometryKind.Unspecified,
                NominalAngleDeg = core.Winner?.NominalAngleDeg,
                Ports = topology?.Ports ?? Array.Empty<MepPort>(),
                Features = core.Features,
                Line = ProductLine.Unknown,
                Confidence = core.Confidence,
                ValveKind = SubtypeGranulation.ValveKindFor(core.Winner?.Id),
                InstrumentKind = SubtypeGranulation.InstrumentKindFor(core.Winner?.Id),
                FilterKind = null,
            };
        }

        /// <summary>
        /// Classificacao TEXTO-ONLY conservadora (2.B-5b, D2/C3): SEM geometria — so
        /// <see cref="ElementTexts"/>. E o que o migrador de catalogo (3.A) usa nos SKUs
        /// Tigre. Infere BaseKind por contagem de baseKindTokens, filtra candidatos por
        /// BaseKind (NAO topologicamente), pontua, elege so entre pais nao-confirmaveis,
        /// promove validando SO o mandatory (sem topologia) e CAPA o confidence (nunca High
        /// sem geometria). SEMPRE devolve uma identidade (BaseKind=Unknown -> NeedsReview).
        /// </summary>
        public ConnectionIdentity ClassifyTextOnly(ElementTexts texts)
        {
            ElementTexts safeTexts = texts ?? new ElementTexts();
            var opts = new TokenizerOptions
            {
                Aliases = _doc.TokenAliases,
                NegativeTokens = _doc.NegativeTokens,
            };

            ISet<string> familyTokens = TokenSet(safeTexts.FamilyName, opts);
            ISet<string> typeTokens = TokenSet(safeTexts.TypeName, opts);
            ISet<string> descTokens = TokenSet(safeTexts.Description, opts);

            var allTokens = new HashSet<string>(familyTokens, StringComparer.Ordinal);
            allTokens.UnionWith(typeTokens);
            allTokens.UnionWith(descTokens);

            BaseKind inferred = InferBaseKindFromText(allTokens);
            if (inferred == BaseKind.Unknown)
            {
                return TextOnlyIdentity(
                    BaseKind.Unknown,
                    winner: null,
                    Feature.None,
                    ClassificationScoring.ComputeTextOnlyConfidence(
                        BaseKind.Unknown, 0, subtypePromoted: false, 0.0, Array.Empty<string>()));
            }

            var scored = new List<ScoredCandidate>();
            foreach (ConnectionRule rule in _doc.Rules)
            {
                if (rule.BaseKind != inferred)
                {
                    continue;
                }

                int lexical = ClassificationScoring.ScoreCandidate(
                    rule, _doc.BaseKindTokens, familyTokens, typeTokens, descTokens);
                scored.Add(new ScoredCandidate { Rule = rule, LexicalScore = lexical });
            }

            if (scored.Count == 0)
            {
                return TextOnlyIdentity(
                    inferred,
                    winner: null,
                    Feature.None,
                    ClassificationScoring.ComputeTextOnlyConfidence(
                        inferred, 0, subtypePromoted: false, 0.0, Array.Empty<string>()));
            }

            ScoredCandidate winner = SelectWinner(scored);

            // Promocao SEM validacao topologica (sem geometria) — valida SO o mandatory.
            (ConnectionRule promoted, double disambiguatorPenalty, IReadOnlyList<string> promotionReasons) =
                ClassificationEnrichment.PromoteWinner(
                    winner.Rule, allTokens, topology: null, tolerances: null, _byId, validateTopology: false);

            bool subtypePromoted = !ReferenceEquals(promoted, winner.Rule);

            // Features SO lexicais (Reduced e geometrico, ausente sem topologia).
            Feature features = ClassificationEnrichment.DetectFeatures(allTokens);

            var reasons = new List<string>(ClassificationScoring.CollectLexicalReasons(
                winner.Rule, _doc.BaseKindTokens, familyTokens, typeTokens, descTokens));
            reasons.AddRange(promotionReasons);

            ClassificationConfidence confidence = ClassificationScoring.ComputeTextOnlyConfidence(
                inferred, winner.LexicalScore, subtypePromoted, disambiguatorPenalty, reasons);

            return TextOnlyIdentity(inferred, promoted, features, confidence);
        }

        // Infere o BaseKind por CONTAGEM de baseKindTokens presentes (sem geometria). Mais
        // hits vence; ZERO hits ou EMPATE -> Unknown (NeedsReview). Conservador por design
        // (2.B-5b): "joelho"+"bucha" (elbow vs reducer) empata em 1 -> Unknown.
        private BaseKind InferBaseKindFromText(ISet<string> allTokens)
        {
            BaseKind best = BaseKind.Unknown;
            int bestHits = 0;
            bool tie = false;

            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in _doc.BaseKindTokens)
            {
                if (!Enum.TryParse(entry.Key, ignoreCase: true, out BaseKind kind)
                    || !Enum.IsDefined(typeof(BaseKind), kind))
                {
                    continue;
                }

                int hits = 0;
                foreach (string token in entry.Value)
                {
                    if (allTokens.Contains(token))
                    {
                        hits++;
                    }
                }

                if (hits > bestHits)
                {
                    bestHits = hits;
                    best = kind;
                    tie = false;
                }
                else if (hits == bestHits && hits > 0)
                {
                    tie = true;
                }
            }

            return (bestHits == 0 || tie) ? BaseKind.Unknown : best;
        }

        private static ConnectionIdentity TextOnlyIdentity(
            BaseKind baseKind, ConnectionRule? winner, Feature features, ClassificationConfidence confidence)
            => new()
            {
                Discipline = Discipline.Plumbing, // texto-only assume hidraulica (unico rulebook do MVP 1)
                Category = CategoryForTextOnly(baseKind),
                BaseKind = baseKind,
                GeometryKind = winner?.GeometryKind ?? GeometryKind.Unspecified,
                NominalAngleDeg = winner?.NominalAngleDeg, // do JSON do winner, nao da geometria
                Ports = Array.Empty<MepPort>(), // sem geometria
                Features = features,
                Line = ProductLine.Unknown, // cam 5 -> 3.A
                Confidence = confidence,
                ValveKind = SubtypeGranulation.ValveKindFor(winner?.Id),
                InstrumentKind = SubtypeGranulation.InstrumentKindFor(winner?.Id),
                FilterKind = null,
            };

        // Heuristica simples de categoria por BaseKind (sem motor): valvula = accessory,
        // fixture = plumbing fixture, Unknown = Unknown, resto (fittings) = pipe fitting.
        private static ProductCategory CategoryForTextOnly(BaseKind baseKind)
            => baseKind switch
            {
                BaseKind.Unknown => ProductCategory.Unknown,
                BaseKind.Valve => ProductCategory.PipeAccessory,
                BaseKind.Fixture => ProductCategory.PlumbingFixture,
                _ => ProductCategory.PipeFitting,
            };

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
            // Furo cam 3 x cam 4 (2.B-4b, Codex Opcao 1): subtipos requiresLexicalConfirmation
            // tem hints exclusivos que pontuam na cam 3 e os fariam vencer por SCORE,
            // BYPASSANDO o mandatoryLexical validado na cam 4 (ex.: "Joelho Bucha" sem "latao"
            // elegia elbow-brass-bushing). Elege SO entre os pais canonicos (nao-confirmaveis);
            // os confirmaveis chegam exclusivamente via promocao validada (PromoteWinner). O
            // fallback p/ scored e defensivo — o guardrail anti-orfao garante eligible nao-vazio
            // sempre que ha candidatos.
            List<ScoredCandidate> eligible = scored.Where(c => !c.Rule.RequiresLexicalConfirmation).ToList();
            IReadOnlyList<ScoredCandidate> pool = eligible.Count > 0 ? eligible : scored;

            ScoredCandidate best = pool[0];
            for (int i = 1; i < pool.Count; i++)
            {
                if (IsBetter(pool[i], best))
                {
                    best = pool[i];
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
