using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Cad
{
    /// <summary>
    /// Turns a CAD <see cref="GeometryObject"/> (line, polyline or arc) into
    /// straight pipe segments. Arcs are downgraded to a single chord because
    /// <c>Pipe.CreatePlaceholder</c> only accepts straight lines today;
    /// callers receive <paramref name="arcChordCount"/> so they can warn the
    /// user.
    /// </summary>
    public static class CadSegmentExtractor
    {
        public static List<(XYZ Start, XYZ End)> ExtractSegments(
            GeometryObject geom,
            Transform transform,
            out int arcChordCount)
        {
            arcChordCount = 0;
            List<(XYZ, XYZ)> segments = new();

            switch (geom)
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

                case Arc arc:
                    arcChordCount = 1;
                    segments.Add((
                        transform.OfPoint(arc.GetEndPoint(0)),
                        transform.OfPoint(arc.GetEndPoint(1))));
                    break;
            }

            return segments;
        }
    }
}
