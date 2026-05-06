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

            try { fields.Add(fi.Symbol.Family.Name ?? string.Empty); } catch { }
            try { fields.Add(fi.Symbol.Name ?? string.Empty); } catch { }
            try { fields.Add(fi.Name ?? string.Empty); } catch { }

            try
            {
                Parameter? p = fi.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                if (p != null && p.HasValue)
                    fields.Add(p.AsString() ?? string.Empty);
            }
            catch { }

            try
            {
                Parameter? p = fi.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                if (p != null && p.HasValue)
                    fields.Add(p.AsString() ?? string.Empty);
            }
            catch { }

            foreach (string f in fields)
                if (Contains(f, "redux"))
                    return "redux";

            foreach (string f in fields)
                if (Contains(f, "reforcada") || Contains(f, "reforçada"))
                    return "reforcada";

            return "serie normal";
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
