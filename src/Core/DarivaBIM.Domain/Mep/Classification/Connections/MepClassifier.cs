using System;
using System.Collections.Generic;

namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Ponto de entrada da classificacao MEP (secao 13): resolve a disciplina inferida
    /// pelo motor para o <see cref="IConnectionRulebook"/> correspondente e delega. No
    /// MVP 1 so Plumbing tem rulebook; disciplina sem rulebook -> null (nao-suportada
    /// nesta versao, o caller decide o fallback).
    /// </summary>
    public sealed class MepClassifier
    {
        private readonly IReadOnlyDictionary<Discipline, IConnectionRulebook> _rulebooks;

        public MepClassifier(IReadOnlyDictionary<Discipline, IConnectionRulebook> rulebooks)
        {
            _rulebooks = rulebooks ?? throw new ArgumentNullException(nameof(rulebooks));
        }

        /// <summary>Configuracao padrao: so Plumbing, do pipe_connection_rules.json embarcado.</summary>
        public static MepClassifier CreateDefault()
            => new(new Dictionary<Discipline, IConnectionRulebook>
            {
                [Discipline.Plumbing] = ConnectionRulebook.FromEmbedded(),
            });

        /// <summary>
        /// Classifica pela disciplina inferida pelo motor (1.B-1). Retorna null SO quando
        /// a disciplina nao tem rulebook registrado (nao-suportada).
        /// </summary>
        public ConnectionIdentity? Classify(TopologyReadResult result, ElementTexts texts)
        {
            // Sem leitura nao ha disciplina inferida -> nao-suportada (null). O guard
            // tambem estreita 'result' p/ non-null no fluxo (evita CS8604 ao delegar).
            if (result is null)
            {
                return null;
            }

            Discipline discipline = result.Topology?.InferredDiscipline ?? Discipline.Unknown;
            return _rulebooks.TryGetValue(discipline, out IConnectionRulebook? rulebook)
                ? rulebook.Classify(result, texts)
                : null;
        }
    }
}
