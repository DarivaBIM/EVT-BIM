using System.Globalization;
using Autodesk.Revit.DB;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.Parameters;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Revit.Adapters.Features.TigreQuantifica
{
    /// <summary>
    /// Wrapper Revit do <see cref="TigreDetectionRules"/>: extrai
    /// familyName/manufacturer/description/existingCode de um
    /// <see cref="Element"/> e delega pra heurística pura no Domain.
    /// Sem tests headless diretos — LayerIsolation impede instanciar
    /// Element em Core.Tests. Cobertura semântica vem dos tests do
    /// Domain (TigreDetectionRulesTests, 17 fixtures); cobertura runtime
    /// vem do smoke test no Revit.
    /// </summary>
    public static class TigreManufacturerDetector
    {
        public static TigreDetectionResult Detect(Element element, TigreCatalog catalog)
        {
            string? familyName = ReadFamilyName(element);
            string? manufacturer = ReadManufacturer(element);
            string? description = ElementDescriptionReader.Read(element);
            int? existingCode = ReadExistingCode(element);

            return TigreDetectionRules.Detect(
                familyName, manufacturer, description, existingCode, catalog);
        }

        public static bool IsTigreElement(Element element, TigreCatalog catalog)
            => Detect(element, catalog).IsTigre;

        private static string? ReadFamilyName(Element element)
        {
            // FamilyInstance: caminho direto Symbol → Family (fittings,
            // accessories, fixtures, valves).
            if (element is FamilyInstance fi && fi.Symbol?.Family != null)
            {
                string? raw = fi.Symbol.Family.Name;
                return string.IsNullOrWhiteSpace(raw) ? null : raw!.Trim();
            }

            // System families (Pipe, Duct, Conduit, CableTray, Walls...):
            // não têm FamilyInstance; o discriminador é o nome do type
            // ("Tubo Série Normal", "Soldável Marrom", ...).
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return null;

            Element? type = element.Document?.GetElement(typeId);
            string? typeName = type?.Name;
            return string.IsNullOrWhiteSpace(typeName) ? null : typeName!.Trim();
        }

        private static string? ReadManufacturer(Element element)
        {
            string? fromInstance = SafeBuiltInString(element,
                BuiltInParameter.ALL_MODEL_MANUFACTURER);
            if (!string.IsNullOrWhiteSpace(fromInstance))
                return fromInstance;

            // Manufacturer normalmente mora no type das families de
            // catálogo Tigre (todas instâncias compartilham a marca).
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return null;

            Element? type = element.Document?.GetElement(typeId);
            if (type == null) return null;

            return SafeBuiltInString(type, BuiltInParameter.ALL_MODEL_MANUFACTURER);
        }

        private static int? ReadExistingCode(Element element)
        {
            // Tigre: Código pode estar no instance (escrito pelo PipeCodes)
            // ou no type (catalog families). GetParameterIncludingType
            // cobre os dois sem mexer na escrita (slice 1.5 B2).
            Parameter? p = SharedParameterAccessor.GetParameterIncludingType(
                element, TigreCodesSharedParameters.Code);
            if (p == null) return null;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int v = p.AsInteger();
                        return v > 0 ? v : (int?)null;
                    }
                    case StorageType.String:
                    {
                        string? raw = p.AsString();
                        if (string.IsNullOrWhiteSpace(raw)) return null;
                        bool ok = int.TryParse(raw,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int v);
                        return ok && v > 0 ? v : (int?)null;
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

        private static string? SafeBuiltInString(Element element, BuiltInParameter bip)
        {
            Parameter? p;
            try
            {
                p = element.get_Parameter(bip);
            }
            catch
            {
                return null;
            }

            if (p == null || p.StorageType != StorageType.String)
                return null;

            try
            {
                string? raw = p.AsString();
                return string.IsNullOrWhiteSpace(raw) ? null : raw!.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
