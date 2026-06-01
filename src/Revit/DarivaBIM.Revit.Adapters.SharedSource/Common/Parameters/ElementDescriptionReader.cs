using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Parameters
{
    /// <summary>
    /// Lê a descrição de um elemento Revit cobrindo as fontes plausíveis
    /// em projetos pt-BR e en-US, em ordem de prioridade:
    ///
    /// 1. Shared parameter "Descrição" (pt-BR) no instance
    /// 2. Shared parameter "Description" (en-US) no instance
    /// 3. BuiltInParameter.ALL_MODEL_DESCRIPTION no instance
    /// 4. (idem) no type, se nada no instance for útil
    ///
    /// Centralizado pra QuantityScanner (audit) e TigreManufacturerDetector
    /// (heurística "é Tigre?") consumirem a mesma lógica — evita drift de
    /// comportamento entre features que dependem da mesma descrição lógica.
    /// </summary>
    public static class ElementDescriptionReader
    {
        public static string? Read(Element element)
        {
            if (element == null) return null;

            string? value = ReadFromOwner(element);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return null;

            Element? type = element.Document?.GetElement(typeId);
            if (type == null) return null;

            return ReadFromOwner(type);
        }

        private static string? ReadFromOwner(Element owner)
        {
            string? viaName = SafeLookupString(owner, "Descrição") ??
                              SafeLookupString(owner, "Description");
            if (!string.IsNullOrWhiteSpace(viaName)) return viaName;

            return SafeBuiltInString(owner, BuiltInParameter.ALL_MODEL_DESCRIPTION);
        }

        private static string? SafeLookupString(Element element, string parameterName)
        {
            Parameter? p;
            try
            {
                p = element.LookupParameter(parameterName);
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

            if (p == null) return null;

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
