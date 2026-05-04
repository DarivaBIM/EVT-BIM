using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Coleta todos os tubos do projeto (categoria
    /// <c>OST_PipeCurves</c>, instâncias). Retorna uma lista de
    /// <see cref="Pipe"/> já tipados — o coletor descarta silenciosamente os
    /// elementos que, por algum motivo, não são <see cref="Pipe"/>.
    /// </summary>
    internal static class TigrePipeCollector
    {
        public static IList<Pipe> CollectPipes(Document doc)
        {
            IList<Element> elements = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .ToElements();

            List<Pipe> pipes = new(elements.Count);
            foreach (Element el in elements)
            {
                if (el is Pipe pipe)
                    pipes.Add(pipe);
            }
            return pipes;
        }
    }
}
