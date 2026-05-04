using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Revit.Adapters.V2025.Features.Prolongador
{
    /// <summary>
    /// Escolhe o <see cref="PipingSystemType"/> a usar no prolongador,
    /// priorizando sistemas de esgoto/sanitário/ventilação/dreno; em último
    /// caso devolve o primeiro tipo disponível para manter compatibilidade
    /// com projetos cujos nomes não casam o dicionário.
    /// </summary>
    internal static class ProlongadorSystemTypeResolver
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
                    if (Contains(n, pref))
                        return (t, $"PipingSystemType escolhido: '{n}' (match: {pref})");
                }
            }

            return (types[0], $"PipingSystemType fallback: '{types[0].Name}'");
        }

        private static bool Contains(string text, string needle)
        {
            string a = TigreTextUtils.Normalize(text);
            string b = TigreTextUtils.Normalize(needle);
            return !string.IsNullOrEmpty(b) && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }
    }
}
