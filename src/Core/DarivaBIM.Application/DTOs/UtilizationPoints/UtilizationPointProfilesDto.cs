using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// Envelope versionado dos grupos persistidos no arquivo
    /// <c>profiles.json</c>. A propriedade <see cref="Version"/> existe para
    /// permitir migrações futuras sem quebrar instalações antigas.
    /// </summary>
    public sealed class UtilizationPointProfilesDto
    {
        public int Version { get; set; } = 1;
        public List<UtilizationPointGroupDto> Groups { get; set; } = new();
    }
}
