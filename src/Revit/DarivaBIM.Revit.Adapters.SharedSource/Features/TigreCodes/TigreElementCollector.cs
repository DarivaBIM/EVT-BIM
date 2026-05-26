using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Features.TigreQuantifica;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Coleta elementos das 4 categorias relevantes pro Codificar Tigre
    /// (Pipes + PipeFittings + PipeAccessories + PlumbingFixtures) e
    /// filtra cada um via <see cref="TigreManufacturerDetector"/>.
    ///
    /// Cache por TypeId reduz o custo do detector (espelha o pattern do
    /// QuantityScanner — Slice 2D). Famílias Knauf/Amanco em
    /// PipeFitting/PipeAccessory/PlumbingFixtures são excluídas pelo
    /// detector, então NÃO entram nesse coletor.
    ///
    /// Substitui o <see cref="TigrePipeCollector"/> a partir do Slice 3 —
    /// o antigo cobria só Pipes.
    /// </summary>
    public static class TigreElementCollector
    {
        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PlumbingFixtures,
        };

        public static IReadOnlyList<BuiltInCategory> Categories => TargetCategories;

        public static IList<Element> CollectTigreElements(Document doc, TigreCatalog catalog)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            ICollection<ElementId> categoryIds = TargetCategories
                .Select(b => new ElementId(b))
                .ToList();

            ElementMulticategoryFilter filter = new ElementMulticategoryFilter(categoryIds);
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(filter);

            // Cache do veredito do detector por TypeId — todos elementos
            // do mesmo type herdam Family.Name/Manufacturer/Tigre:Código
            // type-level, então o veredito é o mesmo. Em projetos típicos
            // (3000 elementos / ~50 types únicos) reduz ~60× o overhead
            // do detector. Trade-off (instance Manufacturer override): se
            // virar bug em smoke, ajusta — vide backlog do Slice 2D.
            Dictionary<ElementId, bool> isTigreCache = new();
            List<Element> result = new();
            foreach (Element element in collector)
            {
                if (IsTigreCached(element, catalog, isTigreCache))
                    result.Add(element);
            }
            return result;
        }

        private static bool IsTigreCached(
            Element element,
            TigreCatalog catalog,
            Dictionary<ElementId, bool> cache)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return TigreManufacturerDetector.IsTigreElement(element, catalog);

            if (cache.TryGetValue(typeId, out bool cached))
                return cached;

            bool result = TigreManufacturerDetector.IsTigreElement(element, catalog);
            cache[typeId] = result;
            return result;
        }
    }
}
