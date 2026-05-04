using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Cad
{
    /// <summary>
    /// Resolves the local-to-world transform that must be applied to geometry
    /// returned from a CAD link reference. <c>ImportInstance</c> wraps the CAD
    /// content in a single <see cref="GeometryInstance"/>; everything else is
    /// authored in world coordinates and the identity transform is enough.
    /// </summary>
    public static class CadGeometryExtractor
    {
        public static Transform GetTransformForElement(Element element)
        {
            if (element is ImportInstance imp)
            {
                Options opts = new() { ComputeReferences = true };
                GeometryElement? geomElem = imp.get_Geometry(opts);
                if (geomElem != null)
                {
                    foreach (GeometryObject g in geomElem)
                    {
                        if (g is GeometryInstance gi)
                            return gi.Transform;
                    }
                }
            }

            return Transform.Identity;
        }
    }
}
