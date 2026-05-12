using DarivaBIM.Application.UseCases.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases.UtilizationPoints
{
    public class ResolveUtilizationPointRuleUseCaseTests
    {
        private static UtilizationPointRule MakeRule(string name, double min, double max)
        {
            return new UtilizationPointRule(
                name,
                new FamilyTypeReference($"Família {name}", $"Tipo {name}", "Plumbing"),
                new HeightRangeMeters(min, max));
        }

        [Fact]
        public void Execute_picks_first_matching_rule_within_range()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Vaso sanitário", 0.10, 0.30));
            group.Rules.Add(MakeRule("Lavatório", 0.50, 0.80));
            group.Rules.Add(MakeRule("Chuveiro", 1.90, 2.20));

            ResolveUtilizationPointRuleUseCase useCase = new();
            ResolveRuleResult result = useCase.Execute(group, 0.20);

            Assert.True(result.HasMatch);
            Assert.Equal("Vaso sanitário", result.Rule!.Name);
            Assert.False(result.OverlapDetected);
        }

        [Fact]
        public void Execute_returns_no_match_when_height_outside_all_ranges()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Vaso sanitário", 0.10, 0.30));
            group.Rules.Add(MakeRule("Chuveiro", 1.90, 2.20));

            ResolveUtilizationPointRuleUseCase useCase = new();
            ResolveRuleResult result = useCase.Execute(group, 1.20);

            Assert.False(result.HasMatch);
            Assert.Null(result.Rule);
        }

        [Fact]
        public void Execute_returns_first_rule_when_ranges_overlap_and_flags_overlap()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Lavatório", 0.50, 0.90));
            group.Rules.Add(MakeRule("Pia da cozinha", 0.80, 1.20));

            ResolveUtilizationPointRuleUseCase useCase = new();
            ResolveRuleResult result = useCase.Execute(group, 0.85);

            Assert.True(result.HasMatch);
            Assert.Equal("Lavatório", result.Rule!.Name);
            Assert.True(result.OverlapDetected);
        }

        [Fact]
        public void Execute_skips_invalid_rules()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            // Faixa inválida (min > max) — deve ser pulada.
            group.Rules.Add(MakeRule("Inválida", 1.0, 0.5));
            // Mesma faixa-alvo, mas válida.
            group.Rules.Add(MakeRule("Válida", 0.5, 1.0));

            ResolveUtilizationPointRuleUseCase useCase = new();
            ResolveRuleResult result = useCase.Execute(group, 0.75);

            Assert.True(result.HasMatch);
            Assert.Equal("Válida", result.Rule!.Name);
        }

        [Fact]
        public void Execute_returns_match_at_lower_and_upper_bounds_inclusive()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Chuveiro", 1.90, 2.20));

            ResolveUtilizationPointRuleUseCase useCase = new();
            Assert.True(useCase.Execute(group, 1.90).HasMatch);
            Assert.True(useCase.Execute(group, 2.20).HasMatch);
        }
    }
}
