using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Escolhe o <see cref="PipingSystemType"/> a usar no prolongador,
    /// priorizando sistemas de esgoto/sanitário/ventilação/dreno; em último
    /// caso devolve o primeiro tipo disponível para manter compatibilidade
    /// com projetos cujos nomes não casam o dicionário.
    /// </summary>
    internal static class FloorDrainExtensionSystemTypeResolver
    {
        private static readonly string[] Preferences =
        {
            "esgoto", "sanitario", "sanitário", "waste", "sanitary",
            "vent", "dreno", "drain",
        };

        public static (PipingSystemType? SystemType, string Message) Resolve(Document doc)
        {
            List<PipingSystemType> types = new FilteredElementCollector(doc)
                .OfClass(typeof(PipingSystemType))
                .Cast<PipingSystemType>()
                .ToList();

            if (types.Count == 0)
                return (null, "Nenhum PipingSystemType encontrado no projeto.");

            foreach (string pref in Preferences)
            {
                foreach (PipingSystemType t in types)
                {
                    string n = t.Name ?? string.Empty;
                    if (TigreTextUtils.ContainsNormalized(n, pref))
                        return (t, $"PipingSystemType escolhido: '{n}' (match: {pref})");
                }
            }

            return (types[0], $"PipingSystemType fallback: '{types[0].Name}'");
        }
    }
}
