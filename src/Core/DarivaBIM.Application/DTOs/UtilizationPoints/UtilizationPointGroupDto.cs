using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// DTO de grupo persistido em JSON. Reflete <c>UtilizationPointGroup</c>
    /// achatado para serialização.
    /// </summary>
    public sealed class UtilizationPointGroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<UtilizationPointRuleDto> Rules { get; set; } = new();
    }
}
