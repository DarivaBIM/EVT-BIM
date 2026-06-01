using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.SharedParameters
{
    /// <summary>
    /// Busca um shared parameter em um <see cref="Element"/> primeiro pelo
    /// nome e depois pelo GUID. Replica o comportamento do script Dynamo, que
    /// preferia o nome para reaproveitar parâmetros pré-existentes de
    /// projetos legados em que o GUID não bate exatamente.
    /// </summary>
    public static class SharedParameterAccessor
    {
        /// <summary>
        /// Procura o parâmetro EXCLUSIVAMENTE no próprio elemento. Use para
        /// leitura/escrita de instance parameters. PipeCodes usa esta
        /// variante para garantir que escrita atinja só o instance.
        /// </summary>
        public static Parameter? GetParameter(Element element, SharedParameterDefinition definition)
        {
            return TryReadFromElement(element, definition);
        }

        /// <summary>
        /// Procura o parâmetro no instance e, se não encontrar, cai no type
        /// via <c>element.Document.GetElement(element.GetTypeId())</c>.
        /// Codes de catálogo (Tigre: Código, Tigre: Descrição) vivem
        /// tipicamente no type das families fornecidas pelo fabricante —
        /// todas instâncias do mesmo type compartilham o mesmo SKU. Use
        /// apenas para LEITURA; escrever no type via fallback alteraria
        /// silenciosamente todas as instâncias do tipo, o que não é o
        /// comportamento esperado de nenhuma feature do plugin hoje.
        /// </summary>
        public static Parameter? GetParameterIncludingType(Element element, SharedParameterDefinition definition)
        {
            Parameter? fromInstance = TryReadFromElement(element, definition);
            if (fromInstance != null)
                return fromInstance;

            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return null;

            Element? type = element.Document.GetElement(typeId);
            if (type == null)
                return null;

            return TryReadFromElement(type, definition);
        }

        private static Parameter? TryReadFromElement(Element element, SharedParameterDefinition definition)
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
