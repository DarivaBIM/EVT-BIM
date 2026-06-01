namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Como o quantitativo de um grupo é mensurado. Determina a unidade
    /// exibida na tabela e gravada no CSV (un, m, m²) e dita como o Scanner
    /// agrega cada elemento. O mapeamento de <see cref="Autodesk.Revit.DB.BuiltInCategory"/>
    /// → <see cref="MeasurementKind"/> é feito em
    /// <c>QuantityCategoryMap</c>.
    /// </summary>
    public enum MeasurementKind
    {
        /// <summary>Contagem simples de elementos (fittings, accessories, fixtures).</summary>
        Count,

        /// <summary>Soma de comprimentos em metros (tubos, dutos, eletrocalhas).</summary>
        LengthMeters,

        /// <summary>Soma de áreas em metros quadrados (paredes, pisos, forros, coberturas).</summary>
        AreaSquareMeters,
    }

    /// <summary>
    /// Extensões para <see cref="MeasurementKind"/>. Unidade é DERIVADA, não
    /// guardada no DTO, pra evitar estado inconsistente entre <see cref="MeasurementKind"/>
    /// e o campo Unit.
    /// </summary>
    public static class MeasurementKindExtensions
    {
        /// <summary>Rótulo da unidade exibido na tabela e no CSV.</summary>
        public static string ToUnitLabel(this MeasurementKind kind)
        {
            switch (kind)
            {
                case MeasurementKind.LengthMeters:
                    return "m";
                case MeasurementKind.AreaSquareMeters:
                    return "m²";
                case MeasurementKind.Count:
                default:
                    return "un";
            }
        }
    }
}
