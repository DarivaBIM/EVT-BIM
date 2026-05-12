using System;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Regra individual dentro de um <see cref="UtilizationPointGroup"/>: associa
    /// uma família/tipo Revit à faixa de altura em metros em que ele deve ser
    /// inserido. A identificação visual da regra na UI vem do próprio
    /// <see cref="FamilyType"/> — não há nome livre separado.
    /// </summary>
    public sealed class UtilizationPointRule
    {
        public UtilizationPointRule(
            FamilyTypeReference familyType,
            HeightRangeMeters heightRange)
        {
            FamilyType = familyType ?? throw new ArgumentNullException(nameof(familyType));
            HeightRange = heightRange;
        }

        public FamilyTypeReference FamilyType { get; }
        public HeightRangeMeters HeightRange { get; }

        /// <summary>
        /// Regra estruturalmente válida quando tem tipo de família referenciado
        /// e faixa de altura coerente. Não verifica se o tipo existe no
        /// documento Revit ativo — esse check vive na camada de adaptação.
        /// </summary>
        public bool IsValid =>
            FamilyType != null
            && !FamilyType.IsEmpty
            && HeightRange.IsValid;
    }
}
