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

            // Codex HIGH#4 fix: cache por TypeId REMOVIDO. O detector
            // (TigreManufacturerDetector / TigreDetectionRules) lê sinais
            // INSTANCE-LEVEL: ExistingCodeMatch (Tigre: Código instance),
            // ManufacturerTigre/Veto (Manufacturer instance, antes do type).
            // Cachear veredito por TypeId fazia o PRIMEIRO elemento do type
            // determinar todos os outros — dois pipes do mesmo PipeType, A
            // com `Tigre: Código=22150251` instance, B vazio: se B vinha
            // primeiro no foreach, cache=false contaminava A. Resultado
            // não-determinístico, dependente da ordem da iteração.
            //
            // Perf antes: ~3000 elementos / 50 types únicos → 50 chamadas
            // ao detector. Agora: 3000 chamadas, cada uma lê ~5 parâmetros
            // (~µs cada) → ~15-50ms total. Aceitável.
            //
            // Se perf cliff aparecer em modelos 10k+ elementos, reintroduzir
            // cache parcial: só cacheia quando elemento NÃO tem sinal
            // instance-level (Tigre: Código instance vazio E Manufacturer
            // instance vazio → type signals predominam → cache OK).
            List<Element> result = new();
            foreach (Element element in collector)
            {
                if (TigreManufacturerDetector.IsTigreElement(element, catalog))
                    result.Add(element);
            }
            return result;
        }
    }
}
