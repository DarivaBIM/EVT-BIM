using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Application.UseCases.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases.UtilizationPoints
{
    public class UtilizationPointProfilesMapperTests
    {
        [Fact]
        public void RoundTrip_preserves_group_and_rule_data()
        {
            UtilizationPointGroupDto dto = new()
            {
                Id = "abc",
                Name = "Banheiro",
                Rules =
                {
                    new UtilizationPointRuleDto
                    {
                        FamilyName = "UT_Chuveiro",
                        TypeName = "UT_Misturador",
                        CategoryName = "Plumbing Fixtures",
                        ElementId = 123,
                        UniqueId = "uid-1",
                        MinMeters = 1.9,
                        MaxMeters = 2.2,
                    },
                },
            };

            UtilizationPointGroup domain = UtilizationPointProfilesMapper.ToDomain(dto);
            UtilizationPointGroupDto roundTrip = UtilizationPointProfilesMapper.ToDto(domain);

            Assert.Equal(dto.Id, roundTrip.Id);
            Assert.Equal(dto.Name, roundTrip.Name);
            Assert.Single(roundTrip.Rules);
            Assert.Equal("UT_Chuveiro", roundTrip.Rules[0].FamilyName);
            Assert.Equal("UT_Misturador", roundTrip.Rules[0].TypeName);
            Assert.Equal(1.9, roundTrip.Rules[0].MinMeters);
            Assert.Equal(2.2, roundTrip.Rules[0].MaxMeters);
        }
    }
}
