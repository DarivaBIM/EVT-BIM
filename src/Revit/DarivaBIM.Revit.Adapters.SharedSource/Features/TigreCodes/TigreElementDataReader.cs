using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.Common.Parameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Dados extraídos de um <see cref="Element"/> (não-Pipe-only) pra
    /// casamento contra o catálogo Tigre. Generaliza
    /// <see cref="TigrePipeData"/> pra cobrir Pipes + PipeFittings +
    /// PipeAccessories + PlumbingFixtures.
    /// </summary>
    internal readonly struct TigreElementData
    {
        public TigreElementData(
            string description,
            string segment,
            string typeName,
            string familyName,
            int? diameterMm,
            string categoryName,
            string kind,
            BuiltInCategory builtInCategory)
        {
            Description = description;
            Segment = segment;
            TypeName = typeName;
            FamilyName = familyName;
            DiameterMm = diameterMm;
            CategoryName = categoryName;
            Kind = kind;
            BuiltInCategory = builtInCategory;
        }

        public string Description { get; }
        public string Segment { get; }
        public string TypeName { get; }
        public string FamilyName { get; }
        public int? DiameterMm { get; }
        public string CategoryName { get; }
        public string Kind { get; }
        public BuiltInCategory BuiltInCategory { get; }
    }

    /// <summary>
    /// Lê os campos necessários pra casar elemento contra o catálogo
    /// Tigre. Cobre as 4 categorias do Slice 3 (PipeCurves + PipeFitting +
    /// PipeAccessory + PlumbingFixtures). Description via
    /// <see cref="ElementDescriptionReader"/> (Slice 2C.3) — pt-BR/en-US/
    /// BuiltIn, instance + type. Diameter via cascade pipe-specific
    /// (RBS_PIPE_DIAMETER_PARAM) + fallback texto (RBS_CALCULATED_SIZE)
    /// pras categorias que não têm o param de pipe.
    /// </summary>
    internal static class TigreElementDataReader
    {
        private static readonly Regex FirstIntRegex = new(@"\d+", RegexOptions.Compiled);

        public static TigreElementData Read(Document doc, Element element)
        {
            string description = ElementDescriptionReader.Read(element) ?? string.Empty;
            string typeName = ReadTypeName(doc, element);
            string familyName = ReadFamilyName(doc, element);
            string categoryName = element.Category?.Name ?? string.Empty;
            BuiltInCategory bic = ResolveBuiltInCategory(element);
            string kind = MapKind(bic);
            int? diameterMm = ReadDiameterMm(element);
            string segment = ReadSegmentText(element);

            return new TigreElementData(
                description, segment, typeName, familyName,
                diameterMm, categoryName, kind, bic);
        }

        public static string MapKind(BuiltInCategory bic) => bic switch
        {
            BuiltInCategory.OST_PipeCurves => "pipe",
            BuiltInCategory.OST_PipeFitting => "fitting",
            BuiltInCategory.OST_PipeAccessory => "accessory",
            BuiltInCategory.OST_PlumbingFixtures => "fixture",
            _ => string.Empty,
        };

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

        private static string ReadTypeName(Document doc, Element element)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return string.Empty;
            Element? type = doc.GetElement(typeId);
            return type?.Name ?? string.Empty;
        }

        private static string ReadFamilyName(Document doc, Element element)
        {
            // FamilyInstance: Symbol.Family.Name (fittings/accessories/fixtures/valves)
            if (element is FamilyInstance fi && fi.Symbol?.Family != null)
            {
                string? raw = fi.Symbol.Family.Name;
                if (!string.IsNullOrWhiteSpace(raw)) return raw!.Trim();
            }
            // System families (Pipe): caem no type.Name
            return ReadTypeName(doc, element);
        }

        private static int? ReadDiameterMm(Element element)
        {
            int? fromPipeParam = TryReadDoubleFeetAsMm(element, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (fromPipeParam.HasValue) return fromPipeParam;

            // Fallback: RBS_CALCULATED_SIZE expõe diâmetro como string
            // pré-formatada ("25 mm" / "25 mm - 32 mm" pra reduções).
            // Pega o primeiro inteiro.
            int? fromCalculated = TryReadFirstIntFromString(element, BuiltInParameter.RBS_CALCULATED_SIZE);
            if (fromCalculated.HasValue) return fromCalculated;

            return null;
        }

        private static int? TryReadDoubleFeetAsMm(Element element, BuiltInParameter bip)
        {
            try
            {
                Parameter? p = element.get_Parameter(bip);
                if (p == null || p.StorageType != StorageType.Double) return null;
                double feet = p.AsDouble();
                if (feet <= 0) return null;
                double mm = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
                return (int)Math.Round(mm);
            }
            catch
            {
                return null;
            }
        }

        private static int? TryReadFirstIntFromString(Element element, BuiltInParameter bip)
        {
            try
            {
                Parameter? p = element.get_Parameter(bip);
                if (p == null) return null;
                string? raw = p.AsString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                Match m = FirstIntRegex.Match(raw!);
                if (m.Success && int.TryParse(m.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int v) && v > 0)
                {
                    return v;
                }
            }
            catch
            {
                // continua
            }
            return null;
        }

        private static string ReadSegmentText(Element element)
        {
            // RBS_PIPE_SEGMENT_PARAM existe só em Pipes — fittings/accessories/
            // fixtures retornam null e ficam com segment vazio (que NÃO
            // atrapalha o matcher porque tokens são combinados via OR).
            try
            {
                Parameter? p = element.get_Parameter(BuiltInParameter.RBS_PIPE_SEGMENT_PARAM);
                if (p == null) return string.Empty;
                string? raw = p.AsValueString();
                if (!string.IsNullOrWhiteSpace(raw)) return raw!.Trim();
                raw = p.AsString();
                return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw!.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
