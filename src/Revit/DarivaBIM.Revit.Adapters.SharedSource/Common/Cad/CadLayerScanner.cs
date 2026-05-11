using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Cad
{
    /// <summary>
    /// Lê os nomes de layer presentes na geometria de um <see cref="ImportInstance"/>.
    /// O nome do layer vem da <see cref="GraphicsStyleCategory"/> ligada ao
    /// <c>GraphicsStyleId</c> de cada <see cref="GeometryObject"/>. Layers
    /// completamente vazios (sem nenhuma linha/polilinha/arco/etc.) não
    /// aparecem porque não há onde ler o estilo.
    /// </summary>
    public static class CadLayerScanner
    {
        public static IReadOnlyList<string> GetLayers(Document doc, ImportInstance importInstance)
        {
            HashSet<string> layers = new(StringComparer.Ordinal);

            Options opts = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement? geomElem = importInstance.get_Geometry(opts);
            if (geomElem == null)
                return Array.Empty<string>();

            CollectLayers(doc, geomElem, layers);

            List<string> ordered = new(layers);
            ordered.Sort(StringComparer.OrdinalIgnoreCase);
            return ordered;
        }

        private static void CollectLayers(Document doc, GeometryElement geomElem, HashSet<string> layers)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is GeometryInstance gi)
                {
                    // GetInstanceGeometry já devolve coordenadas posicionadas;
                    // não reaplicar Transform.
                    GeometryElement inst = gi.GetInstanceGeometry();
                    if (inst != null)
                        CollectLayers(doc, inst, layers);
                    continue;
                }

                string? name = TryReadLayerName(doc, obj);
                if (!string.IsNullOrEmpty(name))
                {
                    layers.Add(name!);
                }
            }
        }

        public static string? TryReadLayerName(Document doc, GeometryObject obj)
        {
            try
            {
                ElementId styleId = obj.GraphicsStyleId;
                if (styleId == ElementId.InvalidElementId)
                    return null;

                Element? styleElem = doc.GetElement(styleId);
                if (styleElem is not GraphicsStyle style)
                    return null;

                return style.GraphicsStyleCategory?.Name;
            }
            catch
            {
                return null;
            }
        }
    }
}
