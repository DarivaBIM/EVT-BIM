using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2025.Common.SharedParameters
{
    /// <summary>
    /// Busca um shared parameter em um <see cref="Element"/> primeiro pelo
    /// nome e depois pelo GUID. Replica o comportamento do script Dynamo, que
    /// preferia o nome para reaproveitar parâmetros pré-existentes de
    /// projetos legados em que o GUID não bate exatamente.
    /// </summary>
    public static class SharedParameterAccessor
    {
        public static Parameter? GetParameter(Element element, SharedParameterDefinition definition)
        {
            try
            {
                Parameter? p = element.LookupParameter(definition.Name);
                if (p != null)
                    return p;
            }
            catch
            {
                // continua — tenta pelo GUID
            }

            try
            {
                return element.get_Parameter(definition.Guid);
            }
            catch
            {
                return null;
            }
        }
    }
}
