using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace FamiliesImporterHub.Infrastructure
{
    public class CadCurveSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is ImportInstance)
            {
                return true;
            }

            if (elem is CurveElement)
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
