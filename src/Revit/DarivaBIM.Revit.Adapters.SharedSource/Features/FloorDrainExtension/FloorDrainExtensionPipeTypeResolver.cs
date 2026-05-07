using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Domain.Tigre;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Detecta o material da caixa selecionada (redux/reforçada/série
    /// normal) e escolhe o <see cref="PipeType"/> mais compatível
    /// disponível no projeto. Reproduz as preferências do script Dynamo:
    /// para "redux" prioriza nomes que contenham "Redux" + "Prolongamento".
    /// </summary>
    internal static class FloorDrainExtensionPipeTypeResolver
    {
        public static string DetermineMaterialKind(FamilyInstance fi)
        {
            List<string> fields = new();

            // A RevitAPI joga exceções variadas em getters quando o elemento
            // está parcialmente inválido (família corrompida, refresh em
            // andamento, etc.). Cada origem de texto é independente, então
            // engolimos a falha e seguimos com o que conseguimos coletar.
            SafeAdd(fields, () => fi.Symbol.Family.Name);
            SafeAdd(fields, () => fi.Symbol.Name);
            SafeAdd(fields, () => fi.Name);
            SafeAdd(fields, () => ReadStringParameter(fi, BuiltInParameter.ALL_MODEL_DESCRIPTION));
            SafeAdd(fields, () => ReadStringParameter(fi.Symbol, BuiltInParameter.ALL_MODEL_TYPE_COMMENTS));

            foreach (string f in fields)
                if (Contains(f, "redux"))
                    return "redux";

            foreach (string f in fields)
                if (Contains(f, "reforcada") || Contains(f, "reforçada"))
                    return "reforcada";

            return "serie normal";
        }

        private static void SafeAdd(List<string> fields, Func<string?> getter)
        {
            try
            {
                fields.Add(getter() ?? string.Empty);
            }
            catch
            {
            }
        }

        private static string? ReadStringParameter(Element element, BuiltInParameter id)
        {
            Parameter? p = element.get_Parameter(id);
            return p != null && p.HasValue ? p.AsString() : null;
        }

        public static PipeType? FindPipeType(Document doc, string materialKind)
        {
            List<PipeType> types = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsElementType()
                .OfType<PipeType>()
                .ToList();

            if (types.Count == 0)
                return null;

            if (materialKind == "redux")
            {
                // Prioridade: "Redux" + "Prolongamento/Prolongador".
                foreach (PipeType pt in types)
                {
                    string n = pt.Name ?? string.Empty;
                    if (Contains(n, "redux") &&
                        (Contains(n, "prolongamento") || Contains(n, "prolongador")))
                        return pt;
                }

                foreach (PipeType pt in types)
                {
                    string n = pt.Name ?? string.Empty;
                    if (Contains(n, "redux"))
                        return pt;
                }
            }

            foreach (PipeType pt in types)
            {
                string n = pt.Name ?? string.Empty;

                if (materialKind == "reforcada" &&
                    (Contains(n, "reforcada") || Contains(n, "reforçada")))
                    return pt;

                if (materialKind == "serie normal" && Contains(n, "serie normal"))
                    return pt;
            }

            return types[0];
        }

        private static bool Contains(string text, string needle)
        {
            string a = TigreTextUtils.Normalize(text);
            string b = TigreTextUtils.Normalize(needle);
            return !string.IsNullOrEmpty(b) && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }
    }
}
