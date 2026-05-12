using System;
using System.Collections.Generic;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Application.UseCases.UtilizationPoints
{
    /// <summary>
    /// Tipos de problema que uma regra pode apresentar durante a validação
    /// pré-execução. Não inclui "tipo não encontrado", que depende da
    /// resolução contra o documento Revit ativo (vive na camada de adapter).
    /// </summary>
    public enum UtilizationPointRuleIssue
    {
        FamilyTypeMissing = 0,
        HeightRangeInvalid = 1,
    }

    public sealed class UtilizationPointRuleValidation
    {
        public UtilizationPointRuleValidation(int ruleIndex, UtilizationPointRuleIssue issue)
        {
            RuleIndex = ruleIndex;
            Issue = issue;
        }

        public int RuleIndex { get; }
        public UtilizationPointRuleIssue Issue { get; }
    }

    public sealed class UtilizationPointGroupValidationResult
    {
        public UtilizationPointGroupValidationResult(
            bool isValid,
            IReadOnlyList<UtilizationPointRuleValidation> issues)
        {
            IsValid = isValid;
            Issues = issues;
        }

        public bool IsValid { get; }
        public IReadOnlyList<UtilizationPointRuleValidation> Issues { get; }
    }

    /// <summary>
    /// Validações estruturais do grupo ativo antes da inserção. Cada regra
    /// precisa ter um tipo de família escolhido e uma faixa de altura coerente
    /// (min ≤ max).
    /// </summary>
    public sealed class ValidateUtilizationPointGroupUseCase
    {
        public UtilizationPointGroupValidationResult Execute(UtilizationPointGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            List<UtilizationPointRuleValidation> issues = new();
            for (int i = 0; i < group.Rules.Count; i++)
            {
                UtilizationPointRule rule = group.Rules[i];

                if (rule.FamilyType == null || rule.FamilyType.IsEmpty)
                    issues.Add(new UtilizationPointRuleValidation(i, UtilizationPointRuleIssue.FamilyTypeMissing));

                if (!rule.HeightRange.IsValid)
                    issues.Add(new UtilizationPointRuleValidation(i, UtilizationPointRuleIssue.HeightRangeInvalid));
            }

            bool hasValidRule = group.HasAnyValidRule;
            return new UtilizationPointGroupValidationResult(
                isValid: hasValidRule && issues.Count == 0,
                issues: issues);
        }
    }
}
