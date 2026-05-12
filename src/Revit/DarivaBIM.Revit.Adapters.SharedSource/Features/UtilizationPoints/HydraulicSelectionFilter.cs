using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// <see cref="ISelectionFilter"/> que aceita elementos hidráulicos
    /// compatíveis com inserção de ponto de utilização: tubos, conexões,
    /// acessórios e equipamentos hidrossanitários (PlumbingFixtures). Outros
    /// elementos são rejeitados para evitar tentar ler conectores de coisas
    /// sem ConnectorManager.
    /// </summary>
    public sealed class HydraulicSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element == null) return false;

            Category? category = element.Category;
            if (category == null) return false;

            long id = category.Id.Value;
            return id == (long)BuiltInCategory.OST_PipeCurves
                || id == (long)BuiltInCategory.OST_PipeFitting
                || id == (long)BuiltInCategory.OST_PipeAccessory
                || id == (long)BuiltInCategory.OST_PlumbingFixtures
                || id == (long)BuiltInCategory.OST_MechanicalEquipment;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
