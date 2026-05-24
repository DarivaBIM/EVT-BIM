using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Revit.Adapters.Features.TigreQuantifica
{
    /// <summary>
    /// Catálogo declarativo das categorias do Revit que o "Tigre Quantifica"
    /// inclui no relatório, com o tipo de medição associada. O mapeamento é
    /// por <see cref="BuiltInCategory"/> — NÃO por <see cref="Element.Location"/>
    /// nem por testes <c>is Pipe</c> — pra ser robusto a families custom que
    /// herdam categorias padrão.
    /// </summary>
    internal static class QuantityCategoryMap
    {
        private static readonly Dictionary<BuiltInCategory, MeasurementKind> Map = new()
        {
            // Comprimento (metros)
            [BuiltInCategory.OST_PipeCurves] = MeasurementKind.LengthMeters,
            [BuiltInCategory.OST_DuctCurves] = MeasurementKind.LengthMeters,
            [BuiltInCategory.OST_Conduit] = MeasurementKind.LengthMeters,
            [BuiltInCategory.OST_CableTray] = MeasurementKind.LengthMeters,

            // Área (m²)
            [BuiltInCategory.OST_Walls] = MeasurementKind.AreaSquareMeters,
            [BuiltInCategory.OST_Floors] = MeasurementKind.AreaSquareMeters,
            [BuiltInCategory.OST_Ceilings] = MeasurementKind.AreaSquareMeters,
            [BuiltInCategory.OST_Roofs] = MeasurementKind.AreaSquareMeters,

            // Contagem (un) — declarada explicitamente em vez de fallback
            // default, pra deixar AllTargetCategoryIds bem definido e evitar
            // que o FilteredElementCollector traga categorias não previstas
            // (mobiliário, anotação, etc.) que poluiriam o relatório.
            [BuiltInCategory.OST_PipeFitting] = MeasurementKind.Count,
            [BuiltInCategory.OST_PipeAccessory] = MeasurementKind.Count,
            [BuiltInCategory.OST_DuctFitting] = MeasurementKind.Count,
            [BuiltInCategory.OST_DuctAccessory] = MeasurementKind.Count,
            [BuiltInCategory.OST_PlumbingFixtures] = MeasurementKind.Count,
            [BuiltInCategory.OST_MechanicalEquipment] = MeasurementKind.Count,
            [BuiltInCategory.OST_Sprinklers] = MeasurementKind.Count,
            [BuiltInCategory.OST_LightingFixtures] = MeasurementKind.Count,
            [BuiltInCategory.OST_LightingDevices] = MeasurementKind.Count,
            [BuiltInCategory.OST_ElectricalEquipment] = MeasurementKind.Count,
            [BuiltInCategory.OST_ElectricalFixtures] = MeasurementKind.Count,
        };

        /// <summary>
        /// Lista de <see cref="ElementId"/> das categorias-alvo, no formato
        /// pronto pra <see cref="ElementMulticategoryFilter"/>.
        /// </summary>
        public static IReadOnlyList<ElementId> AllTargetCategoryIds { get; } =
            Map.Keys.Select(b => new ElementId(b)).ToList();

        /// <summary>
        /// Categorias para as quais é esperado encontrar o shared parameter
        /// <c>Tigre: Código</c> preenchido. Tubos recebem o parâmetro pelo
        /// PipeCodes; fittings/accessories/fixtures recebem pelas próprias
        /// families Tigre. Paredes, pisos, forros e coberturas NUNCA esperam.
        /// </summary>
        public static IReadOnlyCollection<BuiltInCategory> CategoriesExpectingTigreCode { get; } =
            new HashSet<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_PlumbingFixtures,
            };

        /// <summary>
        /// Categorias para as quais o parâmetro <c>Manufacturer</c> é
        /// esperado pelo relatório de compras. Mesma lista do código Tigre —
        /// componentes catálogo (tubos + conexões + fixtures) precisam de
        /// fabricante. Paredes/pisos não.
        /// </summary>
        public static IReadOnlyCollection<BuiltInCategory> CategoriesExpectingManufacturer { get; } =
            CategoriesExpectingTigreCode;

        /// <summary>
        /// Categorias que pertencem a sistemas MEP nomeáveis (água fria,
        /// esgoto, ventilação, elétrico). Wall/Floor/Ceiling/Roof NUNCA
        /// pertencem a sistema; PlumbingFixtures normalmente herda do
        /// connector, então também entra.
        /// </summary>
        public static IReadOnlyCollection<BuiltInCategory> CategoriesExpectingSystem { get; } =
            new HashSet<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_CableTray,
            };

        public static bool TryGetMeasurementKind(BuiltInCategory bic, out MeasurementKind kind)
        {
            return Map.TryGetValue(bic, out kind);
        }

        public static bool ExpectsTigreCode(BuiltInCategory bic)
        {
            return CategoriesExpectingTigreCode.Contains(bic);
        }

        public static bool ExpectsManufacturer(BuiltInCategory bic)
        {
            return CategoriesExpectingManufacturer.Contains(bic);
        }

        public static bool ExpectsSystem(BuiltInCategory bic)
        {
            return CategoriesExpectingSystem.Contains(bic);
        }
    }
}
