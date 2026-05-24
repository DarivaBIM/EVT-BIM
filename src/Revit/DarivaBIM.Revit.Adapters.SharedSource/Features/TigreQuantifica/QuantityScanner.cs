using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Quantifica;
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

        public QuantityScanner(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
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

            foreach (Element element in collector)
            {
                BuiltInCategory bic = ResolveBuiltInCategory(element);
                if (!QuantityCategoryMap.TryGetMeasurementKind(bic, out MeasurementKind kind))
                    continue; // categoria não mapeada — defesa, o filtro já deveria barrar

                ElementData data = ReadElementData(element, bic, kind);

                GroupKey key = new GroupKey(
                    data.Category,
                    data.Family,
                    data.Type,
                    data.Diameter,
                    data.TigreCode,
                    data.Description,
                    data.Manufacturer,
                    data.System,
                    kind);

                if (!groups.TryGetValue(key, out GroupAccumulator? acc))
                {
                    acc = new GroupAccumulator(bic, data.IsPipeCurvesCategory);
                    groups[key] = acc;
                }
                acc.Add(data.Quantity);

                // Audit por categoria — só rastreia BICs nas quais algum
                // dos 3 campos é esperado. Se a categoria não espera nada,
                // não criamos counter (evita poluir findings).
                bool expectsCode = QuantityCategoryMap.ExpectsTigreCode(bic);
                bool expectsManufacturer = QuantityCategoryMap.ExpectsManufacturer(bic);
                bool expectsSystem = QuantityCategoryMap.ExpectsSystem(bic);
                if (expectsCode || expectsManufacturer || expectsSystem)
                {
                    if (!categoryCounters.TryGetValue(bic, out CategoryAuditCounters? counters))
                    {
                        counters = new CategoryAuditCounters(data.Category, expectsCode, expectsManufacturer, expectsSystem);
                        categoryCounters[bic] = counters;
                    }
                    counters.Total++;
                    if (!string.IsNullOrWhiteSpace(data.TigreCode))
                        counters.WithCode++;
                    if (!string.IsNullOrWhiteSpace(data.Manufacturer))
                        counters.WithManufacturer++;
                    if (!string.IsNullOrWhiteSpace(data.System))
                        counters.WithSystem++;
                }
            }

            List<QuantityGroup> groupDtos = BuildGroupDtos(groups);
            List<QuantityAuditFinding> findings = new(projectInfoResult.Findings);
            findings.AddRange(BuildCategoryFindings(categoryCounters));

            return new QuantitySnapshot
            {
                ProjectInfo = projectInfoResult.Info,
                Groups = groupDtos,
                AuditFindings = findings,
            };
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
            string? diameter = ReadDiameterText(element);
            string? tigreCode = ReadTigreCode(element);
            string description = ReadDescription(element);
            string? manufacturer = ReadManufacturer(element);
            string? system = ReadSystem(element);

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

        private static string? ReadDiameterText(Element element)
        {
            // Tubos / dutos / conduítes: parâmetro double em feet.
            Parameter? p = element.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (p != null && p.StorageType == StorageType.Double)
            {
                try
                {
                    double feet = p.AsDouble();
                    if (feet > 0)
                    {
                        int mm = (int)Math.Round(RevitUnitConverter.FeetToMillimeters(feet));
                        return mm.ToString(CultureInfo.InvariantCulture) + " mm";
                    }
                }
                catch
                {
                    // continua
                }
            }

            // Fittings/accessories: tamanho já formatado pelo Revit (ex.: "25 mm - 32 mm").
            p = element.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
            if (p != null)
            {
                try
                {
                    string? s = p.AsString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s!.Trim();
                }
                catch
                {
                    // continua
                }
            }

            return null;
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
        {
            string?[] candidates = new[]
            {
                element.LookupParameter("Descrição")?.AsString(),
                element.LookupParameter("Description")?.AsString(),
                SafeAsString(element.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)),
            };
            foreach (string? c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c))
                    return c!.Trim();
            }

            Element? type = _doc.GetElement(element.GetTypeId());
            if (type != null)
            {
                string?[] typeCandidates = new[]
                {
                    type.LookupParameter("Descrição")?.AsString(),
                    type.LookupParameter("Description")?.AsString(),
                    SafeAsString(type.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)),
                };
                foreach (string? c in typeCandidates)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                        return c!.Trim();
                }
            }

            return string.Empty;
        }

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
                    bool expectsCode = QuantityCategoryMap.ExpectsTigreCode(kv.Value.BuiltInCategory);
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
                        System = kv.Key.System,
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

                // Vermelho — categoria inteira sem código Tigre: bloqueia o
                // relatório de compras. Mantém a regra estrita ("zero
                // elementos com código") porque qualquer código já indica
                // que o usuário está usando families Tigre na categoria.
                if (c.ExpectsCode && c.WithCode == 0)
                {
                    yield return new QuantityAuditFinding
                    {
                        FamilyType = c.CategoryName,
                        MissingFields = new[] { "Tigre: Código" },
                        Severity = AuditSeverity.Red,
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
                    };
                }
            }
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(
                string category,
                string family,
                string type,
                string? diameter,
                string? tigreCode,
                string description,
                string? manufacturer,
                string? system,
                MeasurementKind measurementKind)
            {
                Category = category ?? string.Empty;
                Family = family ?? string.Empty;
                Type = type ?? string.Empty;
                Diameter = diameter;
                TigreCode = tigreCode;
                Description = description ?? string.Empty;
                Manufacturer = manufacturer;
                System = system;
                MeasurementKind = measurementKind;
            }

            public string Category { get; }
            public string Family { get; }
            public string Type { get; }
            public string? Diameter { get; }
            public string? TigreCode { get; }
            public string Description { get; }
            public string? Manufacturer { get; }
            public string? System { get; }
            public MeasurementKind MeasurementKind { get; }

            public bool Equals(GroupKey other) =>
                StringComparer.Ordinal.Equals(Category, other.Category) &&
                StringComparer.Ordinal.Equals(Family, other.Family) &&
                StringComparer.Ordinal.Equals(Type, other.Type) &&
                StringComparer.Ordinal.Equals(Diameter, other.Diameter) &&
                StringComparer.Ordinal.Equals(TigreCode, other.TigreCode) &&
                StringComparer.Ordinal.Equals(Description, other.Description) &&
                StringComparer.Ordinal.Equals(Manufacturer, other.Manufacturer) &&
                StringComparer.Ordinal.Equals(System, other.System) &&
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
                    h = (h * 397) ^ (System == null ? 0 : StringComparer.Ordinal.GetHashCode(System));
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

            public void Add(decimal q)
            {
                ElementCount++;
                Quantity += q;
            }
        }

        private sealed class CategoryAuditCounters
        {
            public CategoryAuditCounters(
                string categoryName,
                bool expectsCode,
                bool expectsManufacturer,
                bool expectsSystem)
            {
                CategoryName = categoryName ?? string.Empty;
                ExpectsCode = expectsCode;
                ExpectsManufacturer = expectsManufacturer;
                ExpectsSystem = expectsSystem;
            }

            public string CategoryName { get; }
            public bool ExpectsCode { get; }
            public bool ExpectsManufacturer { get; }
            public bool ExpectsSystem { get; }
            public int Total { get; set; }
            public int WithCode { get; set; }
            public int WithManufacturer { get; set; }
            public int WithSystem { get; set; }
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
            public decimal Quantity { get; }
            public bool IsPipeCurvesCategory { get; }
        }
    }
}
