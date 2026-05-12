using System;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Application.UseCases.UtilizationPoints
{
    /// <summary>
    /// Resultado da resolução de regra por altura.
    /// </summary>
    public sealed class ResolveRuleResult
    {
        public ResolveRuleResult(UtilizationPointRule? rule, bool overlapDetected)
        {
            Rule = rule;
            OverlapDetected = overlapDetected;
        }

        public UtilizationPointRule? Rule { get; }
        public bool OverlapDetected { get; }
        public bool HasMatch => Rule != null;
    }

    /// <summary>
    /// Resolve qual <see cref="UtilizationPointRule"/> de um grupo deve ser
    /// usada para uma dada altura em metros. Caso a altura caiba em mais de
    /// uma faixa, a primeira da lista vence (mesmo critério do script
    /// Python) e o resultado sinaliza <see cref="ResolveRuleResult.OverlapDetected"/>.
    /// </summary>
    public sealed class ResolveUtilizationPointRuleUseCase
    {
        public ResolveRuleResult Execute(UtilizationPointGroup group, double heightMeters)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            UtilizationPointRule? first = null;
            int matches = 0;

            for (int i = 0; i < group.Rules.Count; i++)
            {
                UtilizationPointRule rule = group.Rules[i];
                if (!rule.IsValid) continue;
                if (!rule.HeightRange.Contains(heightMeters)) continue;

                if (first == null) first = rule;
                matches++;
            }

            return new ResolveRuleResult(first, overlapDetected: matches > 1);
        }
    }
}
