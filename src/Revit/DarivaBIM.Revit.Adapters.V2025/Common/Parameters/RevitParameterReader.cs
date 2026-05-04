using System;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2025.Common.Parameters
{
    /// <summary>
    /// Helpers de leitura de <see cref="Parameter"/> centralizados:
    /// busca por <see cref="BuiltInParameter"/>, por nome, por
    /// <see cref="Guid"/> (shared parameter) e por
    /// <see cref="ElementId"/>. Sempre tolerantes a falhas — devolvem
    /// <c>null</c> se o Revit lançar exceção, espelhando o comportamento
    /// dos scripts Dynamo originais.
    /// </summary>
    public static class RevitParameterReader
    {
        public static Parameter? ByBuiltIn(Element element, BuiltInParameter bip)
        {
            if (element == null) return null;
            try { return element.get_Parameter(bip); }
            catch { return null; }
        }

        public static Parameter? ByName(Element element, string name)
        {
            if (element == null || string.IsNullOrEmpty(name)) return null;
            try { return element.LookupParameter(name); }
            catch { return null; }
        }

        public static Parameter? ByGuid(Element element, Guid guid)
        {
            if (element == null || guid == Guid.Empty) return null;
            try { return element.get_Parameter(guid); }
            catch { return null; }
        }

        public static Parameter? ById(Element element, ElementId definitionId)
        {
            if (element == null || definitionId == null || definitionId == ElementId.InvalidElementId)
                return null;

            // Itera porque element.get_Parameter(ElementId) não cobre todos os
            // backends de definição em Revit recentes.
            try
            {
                foreach (Parameter p in element.Parameters)
                {
                    if (p?.Id == definitionId)
                        return p;
                }
            }
            catch
            {
                // ignora
            }
            return null;
        }
    }
}
