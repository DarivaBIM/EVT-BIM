namespace DarivaBIM.Application.DTOs.UtilizationPoints
{
    /// <summary>
    /// DTO de regra persistida em JSON. Mantém apenas chave por nome
    /// (Família + Tipo) para sobreviver entre projetos; <c>ElementId</c> e
    /// <c>UniqueId</c> são pistas opcionais válidas no documento atual.
    /// </summary>
    public sealed class UtilizationPointRuleDto
    {
        public string Name { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public long? ElementId { get; set; }
        public string? UniqueId { get; set; }
        public double MinMeters { get; set; }
        public double MaxMeters { get; set; }
    }
}
