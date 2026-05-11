using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.Pipes;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Detecta o material da caixa selecionada (redux/reforçada/série
    /// normal) e escolhe o <see cref="PipeType"/> mais compatível
    /// disponível no projeto. Reproduz as preferências do script Dynamo:
    /// para "redux" prioriza nomes que contenham "Redux" + "Prolongamento".
    /// Também expõe a listagem de tipos compatíveis com um diâmetro
    /// específico, usada pela UI para popular o dropdown por tipo de caixa.
    /// </summary>
    public static class FloorDrainExtensionPipeTypeResolver
    {
        public static string DetermineMaterialKind(FamilyInstance fi)
        {
            List<string> fields = CollectMaterialFields(fi);

            foreach (string f in fields)
                if (Contains(f, "redux"))
                    return "redux";

            foreach (string f in fields)
                if (Contains(f, "reforcada") || Contains(f, "reforçada"))
                    return "reforcada";

            return "serie normal";
        }

        /// <summary>
        /// Variação que aceita os strings de identificação diretamente
        /// (Família, Symbol, instance Name, Description, Type Comments).
        /// Útil quando estamos analisando um <see cref="FamilySymbol"/> sem
        /// instância concreta — caso típico do scan que monta os grupos de
        /// tipos de caixa para a UI.
        /// </summary>
        public static string DetermineMaterialKind(IEnumerable<string?> identifyingFields)
        {
            List<string> fields = identifyingFields
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f!)
                .ToList();

            foreach (string f in fields)
                if (Contains(f, "redux"))
                    return "redux";

            foreach (string f in fields)
                if (Contains(f, "reforcada") || Contains(f, "reforçada"))
                    return "reforcada";

            return "serie normal";
        }

        private static List<string> CollectMaterialFields(FamilyInstance fi)
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

            return fields;
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
            List<PipeType> types = LoadAllPipeTypes(doc);

            if (types.Count == 0)
                return null;

            PipeType? preferred = FindPreferred(types, materialKind);
            return preferred ?? types[0];
        }

        /// <summary>
        /// Retorna todos os <see cref="PipeType"/> do projeto cuja
        /// <c>RoutingPreferenceManager</c> consegue rotear o diâmetro
        /// informado (em mm). Lista ordenada com o tipo preferido (pelo
        /// <paramref name="materialKind"/>) à frente, seguido dos demais
        /// compatíveis em ordem alfabética. Devolve coleção vazia se nenhum
        /// tipo do projeto suportar o diâmetro.
        /// </summary>
        public static List<PipeType> FindPipeTypesForDiameter(
            Document doc,
            double diameterMm,
            string materialKind)
        {
            if (diameterMm <= 0)
                return new List<PipeType>();

            List<PipeType> types = LoadAllPipeTypes(doc);
            if (types.Count == 0)
                return types;

            List<PipeType> compatible = types
                .Where(t => PipeDiameterDiscoveryService.SupportsDiameterMm(doc, t, diameterMm))
                .ToList();

            if (compatible.Count == 0)
                return compatible;

            PipeType? preferred = FindPreferred(compatible, materialKind);

            List<PipeType> ordered = new();
            if (preferred != null)
                ordered.Add(preferred);

            foreach (PipeType pt in compatible.OrderBy(p => p.Name))
            {
                if (preferred != null && pt.Id == preferred.Id)
                    continue;
                ordered.Add(pt);
            }

            return ordered;
        }

        private static List<PipeType> LoadAllPipeTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsElementType()
                .OfType<PipeType>()
                .ToList();
        }

        private static PipeType? FindPreferred(IReadOnlyList<PipeType> types, string materialKind)
        {
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

            return null;
        }

        private static bool Contains(string text, string needle)
        {
            string a = TigreTextUtils.Normalize(text);
            string b = TigreTextUtils.Normalize(needle);
            return !string.IsNullOrEmpty(b) && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }
    }
}
