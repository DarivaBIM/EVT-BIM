using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.Parameters;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.Common.Units;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Revit.Adapters.Features.TigreQuantifica
{
    /// <summary>
    /// Implementação Revit-side de <see cref="IQuantityScanService"/>:
    /// percorre todos os elementos das categorias mapeadas em
    /// <see cref="QuantityCategoryMap"/>, lê metadados (família, tipo,
    /// diâmetro, código Tigre, descrição, fabricante, sistema), agrega em
    /// uma única passada via streaming + dicionário, e devolve o snapshot
    /// pronto pra ViewModel/CSV. Não abre transação — é leitura pura.
    /// </summary>
    public sealed class QuantityScanner : IQuantityScanService
    {
        private const string AuditWithoutTigreCodeMessage =
            "Nenhum elemento desta categoria tem 'Tigre: Código' preenchido. " +
            "Verifique se está usando families Tigre.";

        private readonly Document _doc;
        private readonly TigreCatalog _catalog;

        public QuantityScanner(Document doc, TigreCatalog catalog)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public QuantitySnapshot Scan()
        {
            if (_doc.IsFamilyDocument)
            {
                return new QuantitySnapshot
                {
                    ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                };
            }

            ProjectInfoReadResult projectInfoResult = ProjectInfoReader.Read(_doc);

            // Streaming + dicionário evita materializar duas vezes em memória —
            // espelha TigreCodeScanner.cs:49. NUNCA chamar .ToList() antes do
            // agrupamento em projetos grandes.
            ICollection<ElementId> categoryIds = QuantityCategoryMap.AllTargetCategoryIds.ToList();
            ElementMulticategoryFilter filter = new ElementMulticategoryFilter(categoryIds);
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(filter);

            Dictionary<GroupKey, GroupAccumulator> groups = new();
            Dictionary<BuiltInCategory, CategoryAuditCounters> categoryCounters = new();

            // Cache do veredito do detector por TypeId — todos os elementos
            // do mesmo type compartilham Family.Name / Manufacturer type /
            // Tigre: Código type, então o veredito não muda. Em projetos
            // típicos (3000 elements, ~50 types) reduz ~60x o overhead do
            // detector. Não bypassamos pra instances com Manufacturer
            // instance-level — em famílias Tigre instance-Manufacturer
            // sobrescrito é caso edge raríssimo, e o cache puro mantém
            // correção em ~99.9% dos elementos.
            Dictionary<ElementId, bool> isTigreCache = new();

            foreach (Element element in collector)
            {
                BuiltInCategory bic = ResolveBuiltInCategory(element);
                if (!QuantityCategoryMap.TryGetMeasurementKind(bic, out MeasurementKind kind))
                    continue; // categoria não mapeada — defesa, o filtro já deveria barrar

                ElementData data = ReadElementData(element, bic, kind);
                bool isTigre = IsTigreCached(element, isTigreCache);

                // Slice 4.5 — Sistema sai da GroupKey: o relatório passa a
                // agrupar por (Categoria, Família, Tipo, Diâmetro, Código,
                // Descrição, Fabricante, TigreDescription, MeasurementKind).
                // Sistema continua sendo lido (data.System) e alimenta o
                // audit Yellow "Sistema ausente em N elemento(s)" — só não
                // é mais chave de agrupamento. Sistema fica null na chave
                // (consistente com os outros campos opcionais).
                GroupKey key = new GroupKey(
                    data.Category,
                    data.Family,
                    data.Type,
                    data.Diameter,
                    data.TigreCode,
                    data.Description,
                    data.Manufacturer,
                    data.TigreDescription,
                    kind);

                if (!groups.TryGetValue(key, out GroupAccumulator? acc))
                {
                    acc = new GroupAccumulator(bic, data.IsPipeCurvesCategory);
                    groups[key] = acc;
                }
                acc.Add(data.Quantity);
                if (isTigre)
                    acc.MarkTigre();
                if (isTigre && string.IsNullOrWhiteSpace(data.TigreDescription))
                    acc.MarkTigreDescriptionMissing(element.Id.Value);

                // Audit por categoria — só rastreia BICs nas quais algum
                // dos 3 campos é esperado. Se a categoria não espera nada,
                // não criamos counter (evita poluir findings).
                // Audit Tigre: Código agora é POR ELEMENTO (detector
                // decide), não por categoria. Findings Fabricante/Sistema
                // continuam category-based.
                bool expectsManufacturer = QuantityCategoryMap.ExpectsManufacturer(bic);
                bool expectsSystem = QuantityCategoryMap.ExpectsSystem(bic);
                if (isTigre || expectsManufacturer || expectsSystem)
                {
                    if (!categoryCounters.TryGetValue(bic, out CategoryAuditCounters? counters))
                    {
                        counters = new CategoryAuditCounters(data.Category, expectsManufacturer, expectsSystem);
                        categoryCounters[bic] = counters;
                    }
                    counters.Total++;
                    long elementIdLong = element.Id.Value;
                    if (!string.IsNullOrWhiteSpace(data.Manufacturer))
                        counters.WithManufacturer++;
                    else if (expectsManufacturer)
                        counters.MissingManufacturerIds.Add(elementIdLong);
                    if (!string.IsNullOrWhiteSpace(data.System))
                        counters.WithSystem++;
                    else if (expectsSystem)
                        counters.MissingSystemIds.Add(elementIdLong);
                    if (isTigre)
                    {
                        counters.TigreTotal++;
                        if (!string.IsNullOrWhiteSpace(data.TigreCode))
                            counters.TigreWithCode++;
                        else
                            counters.MissingTigreCodeIds.Add(elementIdLong);
                    }
                }
            }

            // F6-LITE/F1 — Agrega "Tigre: Descrição" ausente por categoria
            // a partir dos acumuladores (apenas grupos IsTigre). Categoria é
            // a chave (não BIC) pra alinhar com os outros findings que já
            // saem por CategoryName. Lista de IDs vai pro DTO ElementIds.
            Dictionary<string, List<long>> tigreDescriptionMissingByCategory =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<GroupKey, GroupAccumulator> kv in groups)
            {
                if (kv.Value.TigreDescriptionMissingCount <= 0) continue;
                string catName = kv.Key.Category;
                if (!tigreDescriptionMissingByCategory.TryGetValue(catName, out List<long>? sink))
                {
                    sink = new List<long>();
                    tigreDescriptionMissingByCategory[catName] = sink;
                }
                sink.AddRange(kv.Value.TigreDescriptionMissingIds);
            }

            List<QuantityGroup> groupDtos = BuildGroupDtos(groups);
            List<QuantityAuditFinding> findings = new(projectInfoResult.Findings);
            findings.AddRange(BuildCategoryFindings(categoryCounters));
            findings.AddRange(BuildTigreDescriptionFindings(tigreDescriptionMissingByCategory));

            return new QuantitySnapshot
            {
                ProjectInfo = projectInfoResult.Info,
                Groups = groupDtos,
                AuditFindings = findings,
            };
        }

        private static IEnumerable<QuantityAuditFinding> BuildTigreDescriptionFindings(
            Dictionary<string, List<long>> missingByCategory)
        {
            foreach (KeyValuePair<string, List<long>> kv in missingByCategory)
            {
                if (kv.Value.Count <= 0) continue;
                yield return new QuantityAuditFinding
                {
                    FamilyType = kv.Key,
                    MissingFields = new[] { $"Tigre: Descrição ausente em {kv.Value.Count} elemento(s)" },
                    Severity = AuditSeverity.Yellow,
                    ElementIds = kv.Value.ToArray(),
                };
            }
        }

        private bool IsTigreCached(Element element, Dictionary<ElementId, bool> cache)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
            {
                // Sem type — vai direto. Caso raro (Pipes podem ter type
                // válido, ProjectInfo poderia cair aqui mas é filtrado).
                return QuantityCategoryMap.ShouldExpectTigreCode(element, _catalog);
            }

            if (cache.TryGetValue(typeId, out bool cached))
                return cached;

            bool result = QuantityCategoryMap.ShouldExpectTigreCode(element, _catalog);
            cache[typeId] = result;
            return result;
        }

        private static BuiltInCategory ResolveBuiltInCategory(Element element)
        {
            Category? cat = element.Category;
            if (cat == null) return BuiltInCategory.INVALID;
#if REVIT2024_OR_GREATER || REVIT2025 || REVIT2026
            return cat.BuiltInCategory;
#else
            return (BuiltInCategory)cat.Id.IntegerValue;
#endif
        }

        private ElementData ReadElementData(Element element, BuiltInCategory bic, MeasurementKind kind)
        {
            string category = element.Category?.Name ?? string.Empty;
            (string family, string type) = ReadFamilyAndType(element);
            string? diameter = ReadDiameterText(element, bic);
            string? tigreCode = ReadTigreCode(element);
            string description = ReadDescription(element);
            string? manufacturer = ReadManufacturer(element);
            string? system = ReadSystem(element);
            // F6-LITE: "Tigre: Descrição" via PipeMetadataReader (instance →
            // type fallback por LookupParameter por nome — não cria GUID
            // novo, família catálogo já traz o param embutido).
            string? tigreDescription = PipeMetadataReader.GetTigreDescriptionOrNull(_doc, element);

            decimal quantity = kind switch
            {
                MeasurementKind.LengthMeters => ReadLengthMeters(element),
                MeasurementKind.AreaSquareMeters => ReadAreaSquareMeters(element),
                MeasurementKind.Count => 1m,
                _ => 1m,
            };

            return new ElementData(
                category,
                family,
                type,
                diameter,
                tigreCode,
                description,
                manufacturer,
                system,
                tigreDescription,
                quantity,
                bic == BuiltInCategory.OST_PipeCurves);
        }

        private (string Family, string Type) ReadFamilyAndType(Element element)
        {
            Element? type = _doc.GetElement(element.GetTypeId());
            string typeName = type?.Name ?? string.Empty;
            string familyName;

            if (type is ElementType et)
                familyName = et.FamilyName ?? string.Empty;
            else if (element is FamilyInstance fi)
                familyName = fi.Symbol?.Family?.Name ?? string.Empty;
            else
                familyName = element.Category?.Name ?? string.Empty;

            return (familyName, typeName);
        }

        private static string? ReadDiameterText(Element element, BuiltInCategory bic)
        {
            switch (bic)
            {
                case BuiltInCategory.OST_PipeCurves:
                case BuiltInCategory.OST_PipeFitting:
                case BuiltInCategory.OST_PipeAccessory:
                case BuiltInCategory.OST_PlumbingFixtures:
                    return ReadPipeDiameter(element);

                case BuiltInCategory.OST_DuctCurves:
                case BuiltInCategory.OST_DuctFitting:
                case BuiltInCategory.OST_DuctAccessory:
                    return ReadDuctDiameter(element);

                case BuiltInCategory.OST_Conduit:
                    return ReadConduitDiameter(element);

                case BuiltInCategory.OST_CableTray:
                    return ReadRectFromBuiltIns(
                        element,
                        BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM,
                        BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);

                default:
                    return null;
            }
        }

        private static string? ReadPipeDiameter(Element element)
        {
            string? d = ReadDiameterFromDoubleFeet(element, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (d != null) return d;

            // Fittings/accessories: Revit também expõe tamanho pré-formatado
            // em RBS_CALCULATED_SIZE (ex.: "25 mm" ou "25 mm - 32 mm"
            // pra reduções).
            return ReadCalculatedSize(element);
        }

        private static string? ReadDuctDiameter(Element element)
        {
            // Duto redondo primeiro; se vazio/zero cai pra retangular.
            string? d = ReadDiameterFromDoubleFeet(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            if (d != null) return d;

            string? rect = ReadRectFromBuiltIns(
                element,
                BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
            if (rect != null) return rect;

            return ReadCalculatedSize(element);
        }

        private static string? ReadConduitDiameter(Element element)
        {
            string? d = ReadDiameterFromDoubleFeet(element, BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
            if (d != null) return d;
            return ReadCalculatedSize(element);
        }

        private static string? ReadDiameterFromDoubleFeet(Element element, BuiltInParameter bip)
        {
            Parameter? p = element.get_Parameter(bip);
            if (p == null || p.StorageType != StorageType.Double) return null;

            try
            {
                double feet = p.AsDouble();
                if (feet <= 0) return null;
                int mm = (int)Math.Round(RevitUnitConverter.FeetToMillimeters(feet));
                return mm.ToString(CultureInfo.InvariantCulture) + " mm";
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadRectFromBuiltIns(Element element, BuiltInParameter widthBip, BuiltInParameter heightBip)
        {
            int? widthMm = TryReadMillimeters(element, widthBip);
            int? heightMm = TryReadMillimeters(element, heightBip);
            if (widthMm == null || heightMm == null) return null;

            return widthMm.Value.ToString(CultureInfo.InvariantCulture) + " × " +
                   heightMm.Value.ToString(CultureInfo.InvariantCulture) + " mm";
        }

        private static int? TryReadMillimeters(Element element, BuiltInParameter bip)
        {
            Parameter? p = element.get_Parameter(bip);
            if (p == null || p.StorageType != StorageType.Double) return null;
            try
            {
                double feet = p.AsDouble();
                if (feet <= 0) return null;
                return (int)Math.Round(RevitUnitConverter.FeetToMillimeters(feet));
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadCalculatedSize(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
            if (p == null) return null;
            try
            {
                string? s = p.AsString();
                return string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadTigreCode(Element element)
        {
            // Families Tigre fornecidas pelo fabricante geralmente expõem
            // "Tigre: Código" como TYPE parameter (todas instâncias do
            // mesmo type compartilham o mesmo SKU). GetParameter (instance
            // only) é usado pelo PipeCodes pra escrita; aqui precisamos do
            // fallback type pra leitura.
            Parameter? p = SharedParameterAccessor.GetParameterIncludingType(element, TigreCodesSharedParameters.Code);
            if (p == null)
                return null;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int v = p.AsInteger();
                        // 0 é o default do Revit para parâmetros Integer e nunca
                        // é código Tigre válido — tratamos como vazio.
                        return v == 0 ? null : v.ToString(CultureInfo.InvariantCulture);
                    }
                    case StorageType.String:
                    {
                        string? s = p.AsString();
                        return string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
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

        private string ReadDescription(Element element)
            => ElementDescriptionReader.Read(element) ?? string.Empty;

        private string? ReadManufacturer(Element element)
        {
            string? candidate = SafeAsString(element.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER));
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate!.Trim();

            Element? type = _doc.GetElement(element.GetTypeId());
            if (type != null)
            {
                candidate = SafeAsString(type.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER));
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate!.Trim();
            }

            return null;
        }

        private static string? ReadSystem(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (p == null)
                return null;

            try
            {
                string? s = p.AsString();
                if (!string.IsNullOrWhiteSpace(s)) return s!.Trim();
                string? vs = p.AsValueString();
                if (!string.IsNullOrWhiteSpace(vs)) return vs!.Trim();
            }
            catch
            {
                // continua
            }

            return null;
        }

        private static decimal ReadLengthMeters(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (p == null || p.StorageType != StorageType.Double) return 0m;
            try
            {
                double feet = p.AsDouble();
                double meters = RevitUnitConverter.FeetToMeters(feet);
                return (decimal)meters;
            }
            catch
            {
                return 0m;
            }
        }

        private static decimal ReadAreaSquareMeters(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (p == null || p.StorageType != StorageType.Double) return 0m;
            try
            {
                double ft2 = p.AsDouble();
                // ft² → m²: 1 ft² = 0.09290304 m² (exato, conforme NIST).
                return (decimal)(ft2 * 0.09290304);
            }
            catch
            {
                return 0m;
            }
        }

        private static string? SafeAsString(Parameter? p)
        {
            if (p == null) return null;
            try { return p.AsString(); } catch { return null; }
        }

        private static List<QuantityGroup> BuildGroupDtos(Dictionary<GroupKey, GroupAccumulator> source)
        {
            return source
                .OrderBy(kv => kv.Key.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.Family, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.Diameter, StringComparer.OrdinalIgnoreCase)
                .Select(kv =>
                {
                    // AuditNote agora é POR GRUPO (não categoria) — usa
                    // a flag IsTigre marcada durante o loop pelo detector.
                    bool expectsCode = kv.Value.IsTigre;
                    string? auditNote = (expectsCode && string.IsNullOrWhiteSpace(kv.Key.TigreCode))
                        ? "Sem código Tigre"
                        : null;

                    return new QuantityGroup
                    {
                        Category = kv.Key.Category,
                        Family = kv.Key.Family,
                        Type = kv.Key.Type,
                        Diameter = kv.Key.Diameter,
                        TigreCode = kv.Key.TigreCode,
                        Description = kv.Key.Description,
                        Manufacturer = kv.Key.Manufacturer,
                        // Slice 4.5 — System saiu da GroupKey (não unifica
                        // mais linhas) e da UI/CSV. Continua existindo no
                        // DTO porque o audit Yellow "Sistema ausente" usa
                        // a leitura individual via scanner. Como a chave
                        // não tem mais Sistema, esta propriedade fica null
                        // no DTO agregado — somente os contadores de
                        // CategoryAuditCounters carregam a visão por gap.
                        System = null,
                        TigreDescription = kv.Key.TigreDescription,
                        MeasurementKind = kv.Key.MeasurementKind,
                        ElementCount = kv.Value.ElementCount,
                        Quantity = kv.Value.Quantity,
                        IsPipeCurvesCategory = kv.Value.IsPipeCurvesCategory,
                        AuditNote = auditNote,
                    };
                })
                .ToList();
        }

        private static IEnumerable<QuantityAuditFinding> BuildCategoryFindings(
            Dictionary<BuiltInCategory, CategoryAuditCounters> categoryCounters)
        {
            foreach (KeyValuePair<BuiltInCategory, CategoryAuditCounters> kv in categoryCounters)
            {
                CategoryAuditCounters c = kv.Value;
                if (c.Total <= 0) continue;

                // Vermelho — categoria tem elementos Tigre SEM código.
                // Codex HIGH#3 fix: dispara em QUALQUER gap parcial, não só
                // quando categoria está 100% sem código. Antes
                // `c.TigreWithCode == 0` deixava 5 conexões sem código
                // entre 100 codificadas invisíveis no audit/F1 "Corrigir
                // agora". Agora MissingTigreCodeIds.Count > 0 dispara.
                // Guard `c.TigreTotal > 0` ainda evita falso positivo em
                // categoria 100% não-Tigre (Knauf/Amanco puros).
                if (c.TigreTotal > 0 && c.MissingTigreCodeIds.Count > 0)
                {
                    yield return new QuantityAuditFinding
                    {
                        FamilyType = c.CategoryName,
                        MissingFields = new[] { "Tigre: Código" },
                        Severity = AuditSeverity.Red,
                        ElementIds = c.MissingTigreCodeIds.ToArray(),
                        IsTigreCodigoMissing = true,
                    };
                }

                // Amarelo — fabricante ausente em N elementos. Aparece
                // sempre que houver gap, não só quando 100% faltam, porque
                // qualquer fittings sem fabricante quebra agrupamento por
                // marca no relatório de compras.
                if (c.ExpectsManufacturer && c.WithManufacturer < c.Total)
                {
                    int missing = c.Total - c.WithManufacturer;
                    yield return new QuantityAuditFinding
                    {
                        FamilyType = c.CategoryName,
                        MissingFields = new[] { $"Fabricante ausente em {missing} elemento(s)" },
                        Severity = AuditSeverity.Yellow,
                        ElementIds = c.MissingManufacturerIds.ToArray(),
                    };
                }

                // Amarelo — sistema ausente em N elementos. Sistema é
                // como o relatório isola Água Fria vs Esgoto vs Águas
                // Pluviais; sem ele as totalizações ficam misturadas.
                if (c.ExpectsSystem && c.WithSystem < c.Total)
                {
                    int missing = c.Total - c.WithSystem;
                    yield return new QuantityAuditFinding
                    {
                        FamilyType = c.CategoryName,
                        MissingFields = new[] { $"Sistema ausente em {missing} elemento(s)" },
                        Severity = AuditSeverity.Yellow,
                        ElementIds = c.MissingSystemIds.ToArray(),
                    };
                }
            }
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            // Slice 4.5 — Sistema saiu da chave. Linhas com o mesmo
            // (categoria, família, tipo, diâmetro, código, descrição,
            // fabricante, tigreDescription, kind) agora unificam mesmo
            // estando em Águas Pluviais vs Água Fria. O audit Yellow
            // "Sistema ausente em N elemento(s)" continua disparando via
            // CategoryAuditCounters (lê data.System diretamente do elemento)
            // — só perdemos a coluna no relatório, não a checagem.
            public GroupKey(
                string category,
                string family,
                string type,
                string? diameter,
                string? tigreCode,
                string description,
                string? manufacturer,
                string? tigreDescription,
                MeasurementKind measurementKind)
            {
                Category = category ?? string.Empty;
                Family = family ?? string.Empty;
                Type = type ?? string.Empty;
                Diameter = diameter;
                TigreCode = tigreCode;
                Description = description ?? string.Empty;
                Manufacturer = manufacturer;
                TigreDescription = tigreDescription;
                MeasurementKind = measurementKind;
            }

            public string Category { get; }
            public string Family { get; }
            public string Type { get; }
            public string? Diameter { get; }
            public string? TigreCode { get; }
            public string Description { get; }
            public string? Manufacturer { get; }
            // F6-LITE — entra na chave: se duas instâncias do mesmo type
            // tiverem "Tigre: Descrição" diferentes (raro mas possível em
            // famílias custom com instance override), viram grupos
            // separados. Comportamento explícito conversado com Matheus
            // — preserva a fidelidade do dado lido.
            public string? TigreDescription { get; }
            public MeasurementKind MeasurementKind { get; }

            public bool Equals(GroupKey other) =>
                StringComparer.Ordinal.Equals(Category, other.Category) &&
                StringComparer.Ordinal.Equals(Family, other.Family) &&
                StringComparer.Ordinal.Equals(Type, other.Type) &&
                StringComparer.Ordinal.Equals(Diameter, other.Diameter) &&
                StringComparer.Ordinal.Equals(TigreCode, other.TigreCode) &&
                StringComparer.Ordinal.Equals(Description, other.Description) &&
                StringComparer.Ordinal.Equals(Manufacturer, other.Manufacturer) &&
                StringComparer.Ordinal.Equals(TigreDescription, other.TigreDescription) &&
                MeasurementKind == other.MeasurementKind;

            public override bool Equals(object? obj) => obj is GroupKey k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = StringComparer.Ordinal.GetHashCode(Category);
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(Family);
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(Type);
                    h = (h * 397) ^ (Diameter == null ? 0 : StringComparer.Ordinal.GetHashCode(Diameter));
                    h = (h * 397) ^ (TigreCode == null ? 0 : StringComparer.Ordinal.GetHashCode(TigreCode));
                    h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(Description);
                    h = (h * 397) ^ (Manufacturer == null ? 0 : StringComparer.Ordinal.GetHashCode(Manufacturer));
                    h = (h * 397) ^ (TigreDescription == null ? 0 : StringComparer.Ordinal.GetHashCode(TigreDescription));
                    h = (h * 397) ^ (int)MeasurementKind;
                    return h;
                }
            }
        }

        private sealed class GroupAccumulator
        {
            public GroupAccumulator(BuiltInCategory bic, bool isPipeCurvesCategory)
            {
                BuiltInCategory = bic;
                IsPipeCurvesCategory = isPipeCurvesCategory;
            }

            public BuiltInCategory BuiltInCategory { get; }
            public bool IsPipeCurvesCategory { get; }
            public int ElementCount { get; private set; }
            public decimal Quantity { get; private set; }

            /// <summary>
            /// True quando ao menos um elemento do grupo foi classificado
            /// como Tigre pelo detector. Usado em BuildGroupDtos pra
            /// decidir se AuditNote "Sem código Tigre" aplica.
            /// </summary>
            public bool IsTigre { get; private set; }

            /// <summary>
            /// IDs dos elementos Tigre do grupo com "Tigre: Descrição"
            /// ausente/vazio. F6-LITE — alimenta o audit Yellow por
            /// categoria; F1 — passa pro DTO ElementIds.
            /// </summary>
            public List<long> TigreDescriptionMissingIds { get; } = new();

            public int TigreDescriptionMissingCount => TigreDescriptionMissingIds.Count;

            public void Add(decimal q)
            {
                ElementCount++;
                Quantity += q;
            }

            public void MarkTigre() => IsTigre = true;

            public void MarkTigreDescriptionMissing(long elementId) =>
                TigreDescriptionMissingIds.Add(elementId);
        }

        private sealed class CategoryAuditCounters
        {
            public CategoryAuditCounters(
                string categoryName,
                bool expectsManufacturer,
                bool expectsSystem)
            {
                CategoryName = categoryName ?? string.Empty;
                ExpectsManufacturer = expectsManufacturer;
                ExpectsSystem = expectsSystem;
            }

            public string CategoryName { get; }
            public bool ExpectsManufacturer { get; }
            public bool ExpectsSystem { get; }
            public int Total { get; set; }
            public int WithManufacturer { get; set; }
            public int WithSystem { get; set; }

            /// <summary>Contagem dos elementos da categoria classificados
            /// como Tigre pelo detector.</summary>
            public int TigreTotal { get; set; }

            /// <summary>Subset de TigreTotal que tem Tigre: Código preenchido.</summary>
            public int TigreWithCode { get; set; }

            // Slice 4.3.A F1 — Listas de IDs por gap, populadas em paralelo
            // com os contadores. Findings agregados (Yellow/Red) levam essas
            // listas pro DTO, alimentando o "Corrigir agora" e a
            // SelectInRevit. ElementId.Value já é long (cross-version 2024+).
            public List<long> MissingTigreCodeIds { get; } = new();
            public List<long> MissingManufacturerIds { get; } = new();
            public List<long> MissingSystemIds { get; } = new();
        }

        private readonly struct ElementData
        {
            public ElementData(
                string category,
                string family,
                string type,
                string? diameter,
                string? tigreCode,
                string description,
                string? manufacturer,
                string? system,
                string? tigreDescription,
                decimal quantity,
                bool isPipeCurvesCategory)
            {
                Category = category;
                Family = family;
                Type = type;
                Diameter = diameter;
                TigreCode = tigreCode;
                Description = description;
                Manufacturer = manufacturer;
                System = system;
                TigreDescription = tigreDescription;
                Quantity = quantity;
                IsPipeCurvesCategory = isPipeCurvesCategory;
            }

            public string Category { get; }
            public string Family { get; }
            public string Type { get; }
            public string? Diameter { get; }
            public string? TigreCode { get; }
            public string Description { get; }
            public string? Manufacturer { get; }
            public string? System { get; }
            public string? TigreDescription { get; }
            public decimal Quantity { get; }
            public bool IsPipeCurvesCategory { get; }
        }
    }
}
