using System.Collections.Generic;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Application.UseCases.UtilizationPoints
{
    /// <summary>
    /// Conversão pura Domain ↔ DTO para persistir/recuperar grupos do
    /// <see cref="DarivaBIM.Application.Contracts.UtilizationPoints.IUtilizationPointSettingsStore"/>.
    /// </summary>
    public static class UtilizationPointProfilesMapper
    {
        public static UtilizationPointGroup ToDomain(UtilizationPointGroupDto dto)
        {
            List<UtilizationPointRule> rules = new();
            for (int i = 0; i < dto.Rules.Count; i++)
            {
                UtilizationPointRuleDto rd = dto.Rules[i];
                FamilyTypeReference reference = new(
                    familyName: rd.FamilyName,
                    typeName: rd.TypeName,
                    categoryName: rd.CategoryName,
                    elementId: rd.ElementId,
                    uniqueId: rd.UniqueId);

                rules.Add(new UtilizationPointRule(
                    reference,
                    new HeightRangeMeters(rd.MinMeters, rd.MaxMeters)));
            }

            return new UtilizationPointGroup(
                id: string.IsNullOrWhiteSpace(dto.Id) ? System.Guid.NewGuid().ToString("N") : dto.Id,
                name: dto.Name,
                rules: rules);
        }

        public static UtilizationPointGroupDto ToDto(UtilizationPointGroup group)
        {
            UtilizationPointGroupDto dto = new()
            {
                Id = group.Id,
                Name = group.Name,
            };

            for (int i = 0; i < group.Rules.Count; i++)
            {
                UtilizationPointRule r = group.Rules[i];
                dto.Rules.Add(new UtilizationPointRuleDto
                {
                    FamilyName = r.FamilyType.FamilyName,
                    TypeName = r.FamilyType.TypeName,
                    CategoryName = r.FamilyType.CategoryName,
                    ElementId = r.FamilyType.ElementId,
                    UniqueId = r.FamilyType.UniqueId,
                    MinMeters = r.HeightRange.MinMeters,
                    MaxMeters = r.HeightRange.MaxMeters,
                });
            }

            return dto;
        }
    }
}
