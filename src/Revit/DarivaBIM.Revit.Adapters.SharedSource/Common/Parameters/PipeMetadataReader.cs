using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Parameters
{
    /// <summary>
    /// Leitura de metadados Tigre específicos de famílias de catálogo (que
    /// não são shared parameters declarados pelo plugin — vêm prontos na
    /// família fornecida pelo fabricante). Hoje cobre apenas
    /// "Tigre: Descrição", lido por nome via <c>LookupParameter</c> com
    /// fallback instance → type. NÃO criamos GUID novo de shared param
    /// porque a família catálogo já traz o param embutido e o plugin não
    /// é responsável por declará-lo.
    ///
    /// Padrão isolado deste reader (separado de
    /// <see cref="ElementDescriptionReader"/>) porque "Tigre: Descrição"
    /// é específico do catálogo Tigre — não tem fallback en-US, não é
    /// confundido com BuiltInParameter.ALL_MODEL_DESCRIPTION e não
    /// deveria ser consumido por features que apenas precisam de uma
    /// descrição genérica.
    /// </summary>
    public static class PipeMetadataReader
    {
        // Nome exato do shared param embutido nas famílias Tigre do
        // catálogo. Conferido com Matheus em 2026-05-26 — convenção do
        // fabricante, não muda entre linhas de produto.
        private const string TigreDescriptionParameterName = "Tigre: Descrição";

        /// <summary>
        /// Lê "Tigre: Descrição" primeiro do instance e, se ausente/vazio,
        /// do type. Retorna <c>null</c> quando o param não existe em
        /// nenhum dos dois ou o valor é nulo/whitespace. Trim aplicado ao
        /// resultado.
        /// </summary>
        public static string? GetTigreDescriptionOrNull(Document doc, Element element)
        {
            if (doc == null || element == null)
                return null;

            string? fromInstance = SafeLookupString(element, TigreDescriptionParameterName);
            if (!string.IsNullOrWhiteSpace(fromInstance))
                return fromInstance;

            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return null;

            Element? type = doc.GetElement(typeId);
            if (type == null)
                return null;

            return SafeLookupString(type, TigreDescriptionParameterName);
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
    }
}
