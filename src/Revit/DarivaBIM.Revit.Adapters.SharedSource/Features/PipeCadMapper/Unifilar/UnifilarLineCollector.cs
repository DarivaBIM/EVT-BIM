using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.Common.Cad;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Unifilar
{
    /// <summary>
    /// Sequência ordenada de vértices de uma PolyLine (≥3 vértices) ou Line
    /// (2 vértices) coletada do CAD. Mantém a estrutura de polyline para que
    /// o snap de bend-angles tenha acesso à cadeia completa antes da geração
    /// dos marcadores.
    /// </summary>
    public sealed class UnifilarPolyline
    {
        public UnifilarPolyline(IReadOnlyList<XYZ> vertices)
        {
            Vertices = vertices;
        }

        public IReadOnlyList<XYZ> Vertices { get; }

        /// <summary>
        /// Itera segmentos consecutivos (Vi, Vi+1) sem alocar a lista
        /// completa.
        /// </summary>
        public IEnumerable<(XYZ Start, XYZ End)> EnumerateSegments()
        {
            for (int i = 0; i < Vertices.Count - 1; i++)
                yield return (Vertices[i], Vertices[i + 1]);
        }
    }

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
            int skippedNonLinear,
            IReadOnlyList<UnifilarPolyline> polylines)
        {
            Segments = segments;
            SkippedNonLinear = skippedNonLinear;
            Polylines = polylines;
        }

        /// <summary>
        /// Lista flat de segmentos individuais. Mantida para callers que
        /// não precisam da estrutura de polyline. Equivale ao SUM dos
        /// segmentos das <see cref="Polylines"/>.
        /// </summary>
        public IReadOnlyList<(XYZ Start, XYZ End)> Segments { get; }

        public int SkippedNonLinear { get; }

        /// <summary>
        /// Polylines agrupadas (cada Line vira polyline de 2 vértices,
        /// PolyLines viram polyline com N vértices originais). Necessário
        /// para que o pipeline de bend-angle snap trabalhe em cima de
        /// cadeias completas, não de segmentos isolados.
        /// </summary>
        public IReadOnlyList<UnifilarPolyline> Polylines { get; }
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
            List<UnifilarPolyline> polylines = new();
            int skipped = 0;

            Options opts = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement? geomElem = importInstance.get_Geometry(opts);
            if (geomElem != null)
                WalkGeometry(doc, geomElem, Transform.Identity, targetLayer, polylines, ref skipped);

            return BuildBatch(polylines, skipped);
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

            List<UnifilarPolyline> polylines = new();
            int skipped = 0;

            CollectLinearGeometry(geom, transform, polylines, ref skipped);

            return BuildBatch(polylines, skipped);
        }

        private static void WalkGeometry(
            Document doc,
            GeometryElement geomElem,
            Transform transform,
            string targetLayer,
            List<UnifilarPolyline> polylines,
            ref int skipped)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is GeometryInstance gi)
                {
                    GeometryElement inst = gi.GetInstanceGeometry();
                    if (inst != null)
                        WalkGeometry(doc, inst, transform, targetLayer, polylines, ref skipped);
                    continue;
                }

                string? layerName = CadLayerScanner.TryReadLayerName(doc, obj);
                if (!string.Equals(layerName, targetLayer, StringComparison.OrdinalIgnoreCase))
                    continue;

                CollectLinearGeometry(obj, transform, polylines, ref skipped);
            }
        }

        private static void CollectLinearGeometry(
            GeometryObject obj,
            Transform transform,
            List<UnifilarPolyline> polylines,
            ref int skipped)
        {
            switch (obj)
            {
                case Line line:
                    polylines.Add(new UnifilarPolyline(new[]
                    {
                        transform.OfPoint(line.GetEndPoint(0)),
                        transform.OfPoint(line.GetEndPoint(1)),
                    }));
                    break;

                case PolyLine polyLine:
                    IList<XYZ> coords = polyLine.GetCoordinates();
                    if (coords.Count >= 2)
                    {
                        XYZ[] vertices = new XYZ[coords.Count];
                        for (int i = 0; i < coords.Count; i++)
                            vertices[i] = transform.OfPoint(coords[i]);
                        polylines.Add(new UnifilarPolyline(vertices));
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

        private static UnifilarSegmentBatch BuildBatch(IReadOnlyList<UnifilarPolyline> polylines, int skipped)
        {
            // Achata segmentos para callers que ainda usam a lista flat.
            List<(XYZ, XYZ)> segments = new();
            foreach (UnifilarPolyline polyline in polylines)
            foreach ((XYZ start, XYZ end) in polyline.EnumerateSegments())
                segments.Add((start, end));

            return new UnifilarSegmentBatch(segments, skipped, polylines);
        }
    }
}
