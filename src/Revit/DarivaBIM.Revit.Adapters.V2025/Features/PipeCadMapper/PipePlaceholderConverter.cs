using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.V2025.Features.PipeCadMapper
{
    /// <summary>
    /// Encapsula a chamada a <see cref="PlumbingUtils.ConvertPipePlaceholders"/>
    /// e a coleta dos tubos resultantes, isolando o ponto onde o Revit pode
    /// lançar exceções por preferências de roteamento ausentes (joelhos/tês).
    /// </summary>
    internal static class PipePlaceholderConverter
    {
        public static List<Pipe> Convert(
            Document doc,
            IReadOnlyList<(Pipe Pipe, XYZ Start, XYZ End)> placeholders)
        {
            List<ElementId> placeholderIds = new(placeholders.Count);
            foreach (var (pipe, _, _) in placeholders)
                placeholderIds.Add(pipe.Id);

            ICollection<ElementId> convertedIds = PlumbingUtils.ConvertPipePlaceholders(doc, placeholderIds);

            List<Pipe> convertedPipes = new(convertedIds.Count);
            foreach (ElementId id in convertedIds)
            {
                if (doc.GetElement(id) is Pipe p)
                    convertedPipes.Add(p);
            }
            return convertedPipes;
        }
    }
}
