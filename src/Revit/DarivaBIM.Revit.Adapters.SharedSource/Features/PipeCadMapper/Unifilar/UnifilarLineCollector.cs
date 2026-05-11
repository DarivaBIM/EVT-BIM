using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.Common.Cad;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Unifilar
{
    /// <summary>
    /// Resultado da coleta unifilar: segmentos retos pertencentes ao layer
    /// alvo, prontos para virar marcadores. Geometrias não-retas (arcos,
    /// elipses, NURBS, etc.) presentes no mesmo layer são ignoradas — em
    /// unifilar essas formas não representam tubos.
    /// </summary>
    public sealed class UnifilarSegmentBatch
    {
        public UnifilarSegmentBatch(
            IReadOnlyList<(XYZ Start, XYZ End)> segments,
            int skippedNonLinear)
        {
            Segments = segments;
            SkippedNonLinear = skippedNonLinear;
        }

        public IReadOnlyList<(XYZ Start, XYZ End)> Segments { get; }
        public int SkippedNonLinear { get; }
    }

    /// <summary>
    /// Coleta segmentos retos (Line e segmentos de PolyLine) de um
    /// <see cref="ImportInstance"/>, restritos a um layer alvo. Usado pelo
    /// botão "marcar todas as linhas do layer (unifilar)" e pela validação
    /// de uma única <see cref="Reference"/> selecionada pelo usuário.
    /// </summary>
    public static class UnifilarLineCollector
    {
        public static UnifilarSegmentBatch CollectFromLayer(
            Document doc,
            ImportInstance importInstance,
            string targetLayer)
        {
            List<(XYZ, XYZ)> segments = new();
            int skipped = 0;

            Options opts = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement? geomElem = importInstance.get_Geometry(opts);
            if (geomElem == null)
                return new UnifilarSegmentBatch(segments, skipped);

            WalkGeometry(doc, geomElem, Transform.Identity, targetLayer, segments, ref skipped);

            return new UnifilarSegmentBatch(segments, skipped);
        }

        /// <summary>
        /// Valida que o objeto referenciado pelo usuário (via pick) é uma
        /// linha/polilinha do layer alvo e devolve seus segmentos. Retorna
        /// <c>null</c> se o objeto está em outro layer ou se a geometria não
        /// é reta (caso em que o caller exibe mensagem ao usuário).
        /// </summary>
        public static UnifilarSegmentBatch? CollectFromReference(
            Document doc,
            GeometryObject geom,
            Transform transform,
            string targetLayer)
        {
            string? layerName = CadLayerScanner.TryReadLayerName(doc, geom);
            if (!string.Equals(layerName, targetLayer, StringComparison.OrdinalIgnoreCase))
                return null;

            List<(XYZ, XYZ)> segments = new();
            int skipped = 0;

            AddLinearSegments(geom, transform, segments, ref skipped);

            return new UnifilarSegmentBatch(segments, skipped);
        }

        private static void WalkGeometry(
            Document doc,
            GeometryElement geomElem,
            Transform transform,
            string targetLayer,
            List<(XYZ, XYZ)> segments,
            ref int skipped)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is GeometryInstance gi)
                {
                    GeometryElement inst = gi.GetInstanceGeometry();
                    if (inst != null)
                        WalkGeometry(doc, inst, transform, targetLayer, segments, ref skipped);
                    continue;
                }

                string? layerName = CadLayerScanner.TryReadLayerName(doc, obj);
                if (!string.Equals(layerName, targetLayer, StringComparison.OrdinalIgnoreCase))
                    continue;

                AddLinearSegments(obj, transform, segments, ref skipped);
            }
        }

        private static void AddLinearSegments(
            GeometryObject obj,
            Transform transform,
            List<(XYZ, XYZ)> segments,
            ref int skipped)
        {
            switch (obj)
            {
                case Line line:
                    segments.Add((
                        transform.OfPoint(line.GetEndPoint(0)),
                        transform.OfPoint(line.GetEndPoint(1))));
                    break;

                case PolyLine polyLine:
                    IList<XYZ> coords = polyLine.GetCoordinates();
                    for (int i = 0; i < coords.Count - 1; i++)
                    {
                        segments.Add((
                            transform.OfPoint(coords[i]),
                            transform.OfPoint(coords[i + 1])));
                    }
                    break;

                case Arc:
                case Ellipse:
                case NurbSpline:
                case HermiteSpline:
                case CylindricalHelix:
                    // Geometrias curvas no layer alvo são descartadas em
                    // unifilar — não representam tubo.
                    skipped++;
                    break;
            }
        }
    }
}
