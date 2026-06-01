using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Mep.Classification.Connections.Rules;
using DarivaBIM.Domain.Mep.Classification.Lexical;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Enriquecimento do match (2.B-4): promocao por disambiguator (cam. 4 do secao 21,
    /// secao 11) e deteccao de features (cam. 6). PURO (Domain). A cam. 5 (linha /
    /// ProductLine) NAO entra aqui — os <c>lexicalLines</c> sao derivados do catalogo
    /// na fase 3.A (decisao Opcao A, Matheus). Reusa <see cref="TopologyMatcher"/> para
    /// validar a topologia do subtipo-filho.
    /// </summary>
    public static class ClassificationEnrichment
    {
        // Penalidade quando um gatilho de disambiguator aparece no texto mas NAO valida
        // (mandatory ausente ou topologia incompativel): ambiguidade sinalizada, sem
        // promover. [AJUSTAR NO SMOKE — alinhado ao WarningPenalty do ClassificationScoring.]
        private const double UnvalidatedPenalty = 0.10;

        // Tabela de features (cam. 6): todos os requiredTokens presentes (AND) -> Flag.
        // Tokens ja normalizados (lower, sem acento) — casados via o mesmo Tokenize do
        // texto. SlidingSleeve / BellAndSpigot / SocketEnd ficam de fora por ora (sem
        // token lexical claro). // extensao futura, validar Codex panoramico.
        private static readonly (string[] RequiredTokens, Feature Flag)[] FeatureTable =
        {
            (new[] { "rosca" }, Feature.ThreadedEnd),
            (new[] { "bucha", "latao" }, Feature.BrassBushing),
            (new[] { "inspecao" }, Feature.Inspection),
            (new[] { "visita" }, Feature.VisitCap),
            (new[] { "invertida" }, Feature.Inverted),
            (new[] { "macho" }, Feature.MaleEnd),
            (new[] { "femea" }, Feature.FemaleEnd),
            (new[] { "flange" }, Feature.FlangedEnd),
        };

        // Hints/triggers normalizados via o MESMO Tokenize do texto, SEM expandir alias
        // nem remover negatives (igual ClassificationScoring): o trigger e o token
        // canonico; a expansao de sinonimo ja aconteceu no TEXTO (em allTokens).
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoNegatives
            = new Dictionary<string, IReadOnlyList<string>>();

        private static readonly TokenizerOptions TokenOptions = new()
        {
            ExpandAliases = false,
            NegativeTokens = NoNegatives,
        };

        /// <summary>
        /// Cam. 4: promove o winner a um subtipo-filho quando um disambiguator dispara
        /// (gatilho presente + mandatory presentes + topologia do filho compativel).
        /// UM nivel de promocao (sem cadeia recursiva), fiel ao secao 21. Se um gatilho
        /// aparece mas NAO valida e nada e promovido, devolve penalidade de ambiguidade.
        /// </summary>
        public static (ConnectionRule Promoted, double DisambiguatorPenalty, IReadOnlyList<string> Reasons) PromoteWinner(
            ConnectionRule winner,
            ISet<string> allTokens,
            ConnectionTopology topology,
            RulebookTolerances tolerances,
            IReadOnlyDictionary<string, ConnectionRule> byId)
        {
            ConnectionRule promoted = winner;
            string? promoteReason = null;
            var unvalidatedTriggers = new List<string>();

            foreach (LexicalDisambiguator disambiguator in winner.LexicalDisambiguators)
            {
                string? trigger = NormalizeOne(disambiguator.Trigger);
                if (trigger is null || !allTokens.Contains(trigger))
                {
                    continue;
                }

                // Gatilho presente. O loader garante que PromoteTo existe; defensivo aqui.
                if (!byId.TryGetValue(disambiguator.PromoteTo, out ConnectionRule? child))
                {
                    continue;
                }

                bool topologyOk = !disambiguator.TopologyMustMatch
                    || TopologyMatcher.IsCompatible(child, topology, tolerances);
                bool mandatoryOk = AllPresent(disambiguator.MandatoryLexical, allTokens);

                if (topologyOk && mandatoryOk)
                {
                    promoted = child;
                    promoteReason = $"DisambiguatorPromoted:{disambiguator.Trigger}->{disambiguator.PromoteTo}";
                    break;
                }

                // Gatilho disparou mas nao validou -> ambiguidade.
                unvalidatedTriggers.Add(disambiguator.Trigger);
            }

            var reasons = new List<string>();
            double penalty = 0.0;
            if (promoteReason is not null)
            {
                // Promoveu: a ambiguidade foi resolvida; gatilhos nao-validados anteriores
                // sao irrelevantes (penalty 0).
                reasons.Add(promoteReason);
            }
            else if (unvalidatedTriggers.Count > 0)
            {
                penalty = UnvalidatedPenalty;
                foreach (string trigger in unvalidatedTriggers)
                {
                    reasons.Add($"DisambiguatorUnvalidated:{trigger}");
                }
            }

            return (promoted, penalty, reasons);
        }

        /// <summary>
        /// Cam. 6: features ortogonais ao BaseKind. Lexicais via <see cref="FeatureTable"/>
        /// (AND dos tokens) + o sinal GEOMETRICO <c>Reduced</c> de
        /// <see cref="ConnectionTopology.HasReduction"/> (calculado pelo motor 1.B-1).
        /// </summary>
        public static Feature DetectFeatures(ISet<string> allTokens, ConnectionTopology topology)
        {
            Feature features = Feature.None;

            foreach ((string[] requiredTokens, Feature flag) in FeatureTable)
            {
                if (AllPresent(requiredTokens, allTokens))
                {
                    features |= flag;
                }
            }

            // Sinal geometrico, nao lexical: reducao ja inferida pelo motor (com tolerancia).
            if (topology.HasReduction)
            {
                features |= Feature.Reduced;
            }

            return features;
        }

        // Todos os termos presentes em allTokens apos normalizar cada um (mesmo Tokenize
        // dos hints). Lista vazia -> trivialmente verdadeira.
        private static bool AllPresent(IReadOnlyList<string> requiredTerms, ISet<string> allTokens)
        {
            foreach (string term in requiredTerms)
            {
                string? normalized = NormalizeOne(term);
                if (normalized is null || !allTokens.Contains(normalized))
                {
                    return false;
                }
            }

            return true;
        }

        // Normaliza um termo (trigger/mandatory/feature) para o token canonico. Termos
        // sao palavras unicas; pega o primeiro token (null se o termo tokeniza vazio).
        private static string? NormalizeOne(string raw)
        {
            IReadOnlyList<string> tokens = LexicalNormalizer.Tokenize(raw, TokenOptions);
            return tokens.Count > 0 ? tokens[0] : null;
        }
    }
}
