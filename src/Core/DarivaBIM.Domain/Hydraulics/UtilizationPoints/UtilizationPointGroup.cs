using System;
using System.Collections.Generic;
using System.Linq;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Conjunto reutilizável de <see cref="UtilizationPointRule"/> agrupadas
    /// por ambiente (ex.: Banheiro, Cozinha, Área de serviço). A ordem das
    /// regras na coleção define o critério de desempate quando mais de uma
    /// faixa contém a mesma altura — a primeira regra compatível ganha.
    /// </summary>
    public sealed class UtilizationPointGroup
    {
        public UtilizationPointGroup(
            string id,
            string name,
            IEnumerable<UtilizationPointRule>? rules = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            Id = id;
            Name = name ?? string.Empty;
            Rules = rules?.ToList() ?? new List<UtilizationPointRule>();
        }

        public string Id { get; }
        public string Name { get; set; }
        public List<UtilizationPointRule> Rules { get; }

        public bool HasAnyValidRule => Rules.Any(r => r.IsValid);

        /// <summary>
        /// Devolve a primeira regra cuja faixa de altura contém
        /// <paramref name="heightMeters"/>. Implementa exatamente o critério
        /// do algoritmo Python de referência: ordem da lista define
        /// precedência em caso de sobreposição.
        /// </summary>
        public UtilizationPointRule? FindRuleForHeight(double heightMeters)
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                UtilizationPointRule rule = Rules[i];
                if (rule.IsValid && rule.HeightRange.Contains(heightMeters))
                    return rule;
            }
            return null;
        }
    }
}
