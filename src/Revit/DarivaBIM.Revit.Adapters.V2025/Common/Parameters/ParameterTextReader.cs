using System.Globalization;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2025.Common.Parameters
{
    /// <summary>
    /// Lê um <see cref="Parameter"/> como texto independente do
    /// <see cref="StorageType"/> (string, integer, double, ElementId).
    /// Equivalente ao <c>ParamToText</c> usado em scripts Dynamo. Devolve
    /// <see cref="string.Empty"/> em qualquer falha.
    /// </summary>
    public static class ParameterTextReader
    {
        public static string Read(Document doc, Parameter? p)
        {
            if (p == null)
                return string.Empty;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString() ?? string.Empty;
                    case StorageType.ElementId:
                    {
                        ElementId id = p.AsElementId();
                        if (id != null && id.Value > 0)
                            return doc.GetElement(id)?.Name ?? string.Empty;
                        break;
                    }
                }

                string? vs = p.AsValueString();
                if (!string.IsNullOrEmpty(vs))
                    return vs;

                return p.StorageType switch
                {
                    StorageType.Integer => p.AsInteger().ToString(CultureInfo.InvariantCulture),
                    StorageType.Double => p.AsDouble().ToString(CultureInfo.InvariantCulture),
                    _ => string.Empty,
                };
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
