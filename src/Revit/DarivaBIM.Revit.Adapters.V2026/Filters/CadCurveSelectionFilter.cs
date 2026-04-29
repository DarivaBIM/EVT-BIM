using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Revit.Adapters.V2026.Filters
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
