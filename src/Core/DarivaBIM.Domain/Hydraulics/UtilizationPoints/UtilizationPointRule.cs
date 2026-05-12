using System;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Regra individual dentro de um <see cref="UtilizationPointGroup"/>: associa
    /// um nome de ponto (ex.: Chuveiro, Vaso sanitário) a uma família/tipo Revit
    /// e à faixa de altura em metros em que ele deve ser inserido.
    /// </summary>
    public sealed class UtilizationPointRule
    {
        public UtilizationPointRule(
            string name,
            FamilyTypeReference familyType,
            HeightRangeMeters heightRange)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            Name = name;
            FamilyType = familyType ?? throw new ArgumentNullException(nameof(familyType));
            HeightRange = heightRange;
        }

        public string Name { get; }
        public FamilyTypeReference FamilyType { get; }
        public HeightRangeMeters HeightRange { get; }

        /// <summary>
        /// A regra é estruturalmente válida quando tem nome, tipo de família
        /// referenciado e faixa de altura coerente. Não verifica se o tipo
        /// existe no documento Revit ativo — esse check vive na camada de
        /// adaptação, porque depende da Revit API.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Name)
            && FamilyType != null
            && !FamilyType.IsEmpty
            && HeightRange.IsValid;
    }
}
