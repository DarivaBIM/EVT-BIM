using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Resolve o <see cref="ElementId"/> do <see cref="Level"/> a usar para
    /// criar o tubo prolongador. Tenta na ordem: <c>elem.LevelId</c>,
    /// <c>GenLevel</c> da vista ativa (se for <see cref="ViewPlan"/>) e
    /// primeiro <c>Level</c> do projeto. Logs descrevem qual fallback foi
    /// usado para diagnóstico.
    /// </summary>
    internal static class FloorDrainExtensionLevelResolver
    {
        public static ElementId Resolve(Document doc, Element elem, List<string> logs)
        {
            try
            {
                if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
                    return elem.LevelId;
            }
            catch { }

            try
            {
                View? v = doc.ActiveView;
                if (v is ViewPlan vp && vp.GenLevel != null)
                {
                    logs.Add("  -> LevelId inválido, usando GenLevel da vista ativa.");
                    return vp.GenLevel.Id;
                }
            }
            catch { }

            Element? lvl = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement();
            if (lvl != null)
            {
                logs.Add("  -> LevelId inválido, usando primeiro Level do projeto.");
                return lvl.Id;
            }

            return ElementId.InvalidElementId;
        }
    }
}
