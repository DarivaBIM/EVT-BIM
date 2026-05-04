using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DarivaBIM.Revit.Adapters.V2025.Common.Filters
{
    /// <summary>
    /// <see cref="ISelectionFilter"/> reutilizável que aceita
    /// <see cref="ImportInstance"/> (vínculos CAD) e qualquer
    /// <see cref="CurveElement"/>. Útil para qualquer ferramenta que peça
    /// ao usuário para clicar em uma linha de CAD ou em uma curva
    /// gerenciada pelo Revit.
    /// </summary>
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
