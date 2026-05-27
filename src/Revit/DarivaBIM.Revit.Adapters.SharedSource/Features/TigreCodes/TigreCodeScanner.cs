using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreCodeScanService"/>:
    /// percorre os elementos Tigre nas 4 categorias relevantes (Pipes +
    /// Conexões + Acessórios + Aparelhos hidrossanitários) via
    /// <see cref="TigreElementCollector"/>, lê descrição / segmento (só
    /// pipe) / tipo / família / diâmetro / kind, casa contra o
    /// <see cref="TigreCatalog"/> com kindFilter e devolve snapshot
    /// agrupado por (Categoria, Kind, TipoNome, Diâmetro, Status). Não
    /// abre transação — leitura pura.
    /// </summary>
    public sealed class TigreCodeScanner : ITigreCodeScanService
    {
        private readonly Document _doc;
        private readonly ITigreCatalogProvider _catalogProvider;
        // Slice 4.3.A F1 ampliado — set opcional de IDs pra filtrar o
        // collector. Vem do "Corrigir agora" do Tigre Quantifica: scanner
        // só processa esses elementos. Null quando varredura é completa
        // (default). HashSet pra lookup O(1) no foreach.
        private readonly HashSet<long>? _prefilterIds;

        public TigreCodeScanner(Document doc, ITigreCatalogProvider catalogProvider)
            : this(doc, catalogProvider, prefilterIds: null)
        {
        }

        public TigreCodeScanner(
            Document doc,
            ITigreCatalogProvider catalogProvider,
            IReadOnlyCollection<long>? prefilterIds)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
            _prefilterIds = prefilterIds == null || prefilterIds.Count == 0
                ? null
                : new HashSet<long>(prefilterIds);
        }

        public TigreScanResult Scan()
        {
            TigreCatalog catalog = _catalogProvider.Load();

            if (catalog.Entries.Count == 0)
            {
                return new TigreScanResult
                {
                    ErrorMessage = "Catálogo Tigre vazio.",
                };
            }

            IList<Element> elements = TigreElementCollector.CollectTigreElements(_doc, catalog);
            if (_prefilterIds != null)
            {
                // Filtra após collector pra preservar a heurística do detector
                // (TigreManufacturerDetector dentro de CollectTigreElements).
                // Caso edge: usuário corrigiu o catálogo entre o scan
                // do Quantifica e o "Corrigir agora", e o detector agora
                // recusa um ID antes elegível — silenciosamente sumido.
                List<Element> filtered = new(elements.Count);
                foreach (Element e in elements)
                {
                    if (_prefilterIds.Contains(e.Id.Value))
                        filtered.Add(e);
                }
                elements = filtered;
            }

            // Streaming via Dictionary — espelha QuantityScanner do Slice 2D
            // (NUNCA materializar duas vezes em projetos grandes).
            Dictionary<GroupKey, GroupAccumulator> map = new();
            Dictionary<string, MutableCategoryStats> categoryStats = new();
            Dictionary<string, bool> bindingAvailable = new();

            int withParam = 0;

            foreach (Element element in elements)
            {
                TigreElementData data = TigreElementDataReader.Read(_doc, element);

                TigreCatalogEntry? match = null;
                if (data.DiameterMm.HasValue && data.Kinds.Count > 0)
                {
                    string combined = TigreTextUtils.Normalize(
                        $"{data.Description} {data.Segment} {data.TypeName}");
                    match = catalog.FindMatch(
                        data.Description, data.Segment, data.TypeName, combined,
                        data.DiameterMm.Value, kindFilters: data.Kinds);
                }

                // GetParameterIncludingType: instance para Pipes (binding
                // global), fallback type para fittings catálogo Tigre que
                // embutem Tigre: Código no type da família.
                Parameter? param = SharedParameterAccessor.GetParameterIncludingType(
                    element, TigreCodesSharedParameters.Code);
                bool hasParam = param != null;
                if (hasParam) withParam++;

                int? currentCode = ReadCurrentCode(param);
                TigrePipeStatus status = ComputeStatus(match, currentCode);

                GroupKey key = new(
                    data.CategoryName, data.Kind, data.FamilyName, data.TypeName, data.DiameterMm, status);
                if (!map.TryGetValue(key, out GroupAccumulator? acc))
                {
                    acc = new GroupAccumulator(match?.Code);
                    map[key] = acc;
                }
                acc.Ids.Add(element.Id.Value);

                if (!categoryStats.TryGetValue(data.CategoryName, out MutableCategoryStats? stats))
                {
                    stats = new MutableCategoryStats();
                    categoryStats[data.CategoryName] = stats;
                }
                stats.Total++;
                if (hasParam) stats.WithParameter++;
                if (match != null) stats.MatchedByCatalog++;

                // Binding by category — true só quando TODOS os elementos
                // Tigre da categoria têm o param acessível. Default ao
                // primeiro elemento; depois colapsa pra false se algum
                // sucessor faltar.
                if (!bindingAvailable.TryGetValue(data.CategoryName, out bool prev))
                    bindingAvailable[data.CategoryName] = hasParam;
                else if (prev && !hasParam)
                    bindingAvailable[data.CategoryName] = false;
            }

            Dictionary<string, CategoryStats> immutableStats = new();
            foreach (KeyValuePair<string, MutableCategoryStats> kv in categoryStats)
            {
                immutableStats[kv.Key] = new CategoryStats
                {
                    Total = kv.Value.Total,
                    WithParameter = kv.Value.WithParameter,
                    WithoutParameter = kv.Value.Total - kv.Value.WithParameter,
                    MatchedByCatalog = kv.Value.MatchedByCatalog,
                };
            }

            List<TigreScanGroup> groups = map
                .OrderBy(kv => kv.Key.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.TypeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.DiameterMm ?? int.MaxValue)
                .Select(kv => new TigreScanGroup(
                    kv.Key.CategoryName,
                    kv.Key.Kind,
                    kv.Key.FamilyName,
                    kv.Key.TypeName,
                    kv.Key.DiameterMm,
                    kv.Key.Status,
                    kv.Value.Ids.ToArray(),
                    kv.Value.MatchedCode))
                .ToList();

            return new TigreScanResult
            {
                CatalogCount = catalog.Entries.Count,
                ElementsTotal = elements.Count,
                ElementsWithParameter = withParam,
                ElementsWithoutParameter = elements.Count - withParam,
                BindingAvailable = bindingAvailable,
                ByCategoryStats = immutableStats,
                Groups = groups,
            };
        }

        private static int? ReadCurrentCode(Parameter? param)
        {
            if (param == null)
                return null;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int v = param.AsInteger();
                        // 0 é default do Revit pra Integer e nunca é um SKU
                        // Tigre válido — tratamos como vazio.
                        return v == 0 ? (int?)null : v;
                    }
                    case StorageType.String:
                    {
                        string? s = param.AsString();
                        if (string.IsNullOrWhiteSpace(s))
                            return null;
                        return int.TryParse(s, System.Globalization.NumberStyles.Integer,
                                            System.Globalization.CultureInfo.InvariantCulture, out int n)
                            ? n
                            : (int?)null;
                    }
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static TigrePipeStatus ComputeStatus(TigreCatalogEntry? match, int? currentCode)
        {
            if (match == null)
                return TigrePipeStatus.NoMatch;

            if (!currentCode.HasValue)
                return TigrePipeStatus.Missing;

            return currentCode.Value == match.Code
                ? TigrePipeStatus.Ok
                : TigrePipeStatus.Divergent;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(
                string categoryName,
                string kind,
                string familyName,
                string typeName,
                int? diameterMm,
                TigrePipeStatus status)
            {
                CategoryName = categoryName ?? string.Empty;
                Kind = kind ?? string.Empty;
                FamilyName = familyName ?? string.Empty;
                TypeName = typeName ?? string.Empty;
                DiameterMm = diameterMm;
                Status = status;
            }

            public string CategoryName { get; }
            public string Kind { get; }
            public string FamilyName { get; }
            public string TypeName { get; }
            public int? DiameterMm { get; }
            public TigrePipeStatus Status { get; }

            public bool Equals(GroupKey other) =>
                StringComparer.Ordinal.Equals(CategoryName, other.CategoryName) &&
                StringComparer.Ordinal.Equals(Kind, other.Kind) &&
                StringComparer.Ordinal.Equals(FamilyName, other.FamilyName) &&
                StringComparer.Ordinal.Equals(TypeName, other.TypeName) &&
                DiameterMm == other.DiameterMm &&
                Status == other.Status;

            public override bool Equals(object? obj) => obj is GroupKey k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = StringComparer.Ordinal.GetHashCode(CategoryName);
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(Kind);
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(FamilyName);
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(TypeName);
                    h = (h * 397) ^ (DiameterMm ?? -1);
                    h = (h * 397) ^ (int)Status;
                    return h;
                }
            }
        }

        private sealed class GroupAccumulator
        {
            public GroupAccumulator(int? matchedCode)
            {
                MatchedCode = matchedCode;
            }

            public int? MatchedCode { get; }
            public List<long> Ids { get; } = new();
        }

        private sealed class MutableCategoryStats
        {
            public int Total { get; set; }
            public int WithParameter { get; set; }
            public int MatchedByCatalog { get; set; }
        }
    }
}
