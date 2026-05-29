using Autodesk.Revit.DB;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Revit.Adapters.Common.Parameters;

namespace DarivaBIM.Revit.Adapters.Common.Mep
{
    /// <summary>
    /// Le os tres textos de um <see cref="Element"/> (FamilyName, TypeName,
    /// Description) para o <see cref="ElementTexts"/> (Domain) consumido pelo
    /// Classify da fase 2.B. Description reusa o <see cref="ElementDescriptionReader"/>
    /// (fallback instance->type, pt-BR/en-US/BuiltIn, ja validado). Valor ausente
    /// vira "" (nunca null). Os pesos 3/2/1 do score sao aplicados no Classify.
    /// </summary>
    public static class ElementTextsReader
    {
        public static ElementTexts Read(Element element)
        {
            if (element is null)
            {
                return new ElementTexts();
            }

            string typeName = ReadTypeName(element);

            return new ElementTexts
            {
                FamilyName = ReadFamilyName(element, typeName),
                TypeName = typeName,
                Description = ElementDescriptionReader.Read(element) ?? string.Empty,
            };
        }

        private static string ReadTypeName(Element element)
        {
            try
            {
                ElementId typeId = element.GetTypeId();
                if (typeId == ElementId.InvalidElementId)
                {
                    return string.Empty;
                }

                Element? type = element.Document?.GetElement(typeId);
                string? name = type?.Name;
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name!.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadFamilyName(Element element, string typeNameFallback)
        {
            try
            {
                // FamilyInstance (fittings/accessories/fixtures): Symbol.Family.Name.
                if (element is FamilyInstance fi)
                {
                    string? raw = fi.Symbol?.Family?.Name;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        return raw!.Trim();
                    }
                }
            }
            catch
            {
                // cai no fallback do nome do tipo
            }

            // System families (Pipe) nao tem Family -> usa o nome do tipo.
            return typeNameFallback;
        }
    }
}
