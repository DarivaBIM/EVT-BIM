using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts.UtilizationPoints;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Revit.Adapters.Common.Units;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Implementação Revit-side de
    /// <see cref="IFamilyTypeCatalogService"/>. Varre o documento ativo
    /// procurando <c>FamilySymbol</c>s relevantes para inserção de pontos
    /// hidrossanitários e devolve-os já projetados em DTO neutro.
    /// </summary>
    public sealed class RevitFamilyTypeCatalogService : IFamilyTypeCatalogService
    {
        private static readonly BuiltInCategory[] PreferredCategories =
        {
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_GenericModel,
        };

        private readonly Document _doc;

        public RevitFamilyTypeCatalogService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IReadOnlyList<FamilyTypeOptionDto> GetAvailableFamilyTypes()
        {
            List<FamilyTypeOptionDto> result = new();

            ElementMulticategoryFilter filter = new(PreferredCategories);

            FilteredElementCollector collector = new(_doc);
            collector.OfClass(typeof(FamilySymbol)).WherePasses(filter);

            foreach (Element element in collector)
            {
                if (element is not FamilySymbol symbol) continue;

                string familyName;
                try { familyName = symbol.Family?.Name ?? string.Empty; }
                catch { familyName = string.Empty; }

                string typeName;
                try { typeName = symbol.Name ?? string.Empty; }
                catch { typeName = string.Empty; }

                string? categoryName;
                try { categoryName = symbol.Category?.Name; }
                catch { categoryName = null; }

                string uniqueId;
                try { uniqueId = symbol.UniqueId ?? string.Empty; }
                catch { uniqueId = string.Empty; }

                result.Add(new FamilyTypeOptionDto(
                    elementId: symbol.Id.Value,
                    uniqueId: uniqueId,
                    familyName: familyName,
                    typeName: typeName,
                    categoryName: categoryName));
            }

            result.Sort((a, b) =>
            {
                int byFamily = string.Compare(a.FamilyName, b.FamilyName, StringComparison.OrdinalIgnoreCase);
                if (byFamily != 0) return byFamily;
                return string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        public IReadOnlyList<LevelOptionDto> GetLevels()
        {
            List<LevelOptionDto> result = new();

            FilteredElementCollector collector = new(_doc);
            collector.OfClass(typeof(Level));

            foreach (Element element in collector)
            {
                if (element is not Level level) continue;

                double elevationFeet;
                try { elevationFeet = level.Elevation; }
                catch { elevationFeet = 0.0; }

                double elevationMeters = RevitUnitConverter.FeetToMeters(elevationFeet);

                string name;
                try { name = level.Name ?? string.Empty; }
                catch { name = string.Empty; }

                result.Add(new LevelOptionDto(level.Id.Value, name, elevationMeters));
            }

            result.Sort((a, b) => a.ElevationMeters.CompareTo(b.ElevationMeters));
            return result;
        }
    }
}
