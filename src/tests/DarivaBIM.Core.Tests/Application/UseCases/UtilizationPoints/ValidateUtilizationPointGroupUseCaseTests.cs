using DarivaBIM.Application.UseCases.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases.UtilizationPoints
{
    public class ValidateUtilizationPointGroupUseCaseTests
    {
        private static UtilizationPointRule MakeRule(string name, double min, double max, string familyName = "FAM", string typeName = "TYPE")
        {
            return new UtilizationPointRule(
                name,
                new FamilyTypeReference(familyName, typeName),
                new HeightRangeMeters(min, max));
        }

        [Fact]
        public void Group_with_one_valid_rule_is_valid()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Chuveiro", 1.90, 2.20));

            ValidateUtilizationPointGroupUseCase useCase = new();
            UtilizationPointGroupValidationResult result = useCase.Execute(group);

            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public void Empty_group_is_not_valid()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");

            ValidateUtilizationPointGroupUseCase useCase = new();
            UtilizationPointGroupValidationResult result = useCase.Execute(group);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Invalid_height_range_is_reported()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(MakeRule("Inválida", 0.8, 0.4));

            ValidateUtilizationPointGroupUseCase useCase = new();
            UtilizationPointGroupValidationResult result = useCase.Execute(group);

            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Issue == UtilizationPointRuleIssue.HeightRangeInvalid);
        }

        [Fact]
        public void Missing_family_type_is_reported()
        {
            UtilizationPointGroup group = new("g1", "Banheiro");
            group.Rules.Add(new UtilizationPointRule(
                "Chuveiro",
                new FamilyTypeReference(string.Empty, string.Empty),
                new HeightRangeMeters(1.90, 2.20)));

            ValidateUtilizationPointGroupUseCase useCase = new();
            UtilizationPointGroupValidationResult result = useCase.Execute(group);

            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Issue == UtilizationPointRuleIssue.FamilyTypeMissing);
        }
    }
}
