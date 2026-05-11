using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.FloorDrainExtension
{
    /// <summary>
    /// Varre o projeto (ou um conjunto de caixas) e agrupa as instâncias
    /// pelo tipo (Família + Symbol), inferindo o diâmetro do conector
    /// vertical (o "prolongador" alvo) a partir de uma instância amostra.
    /// O resultado alimenta a UI: cada grupo vira uma linha com o seu
    /// próprio dropdown de PipeType compatível.
    /// </summary>
    public static class FloorDrainExtensionBoxScanner
    {
        /// <summary>
        /// Coleta todas as <see cref="FamilyInstance"/> candidatas a caixa
        /// sifonada/seca no documento (mesmas categorias que o filtro de
        /// seleção aceita), excluindo família corrompida ou sem MEPModel.
        /// </summary>
        public static List<FamilyInstance> CollectAllBoxes(Document doc)
        {
            return CollectByCategories(doc, view: null);
        }

        /// <summary>
        /// Mesma coisa que <see cref="CollectAllBoxes"/>, mas restrito ao
        /// que está visível na vista informada.
        /// </summary>
        public static List<FamilyInstance> CollectBoxesInView(Document doc, View view)
        {
            return CollectByCategories(doc, view);
        }

        private static List<FamilyInstance> CollectByCategories(Document doc, View? view)
        {
            BuiltInCategory[] cats =
            {
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_MechanicalEquipment,
            };

            HashSet<long> seen = new();
            List<FamilyInstance> result = new();

            foreach (BuiltInCategory cat in cats)
            {
                FilteredElementCollector col = view != null
                    ? new FilteredElementCollector(doc, view.Id)
                    : new FilteredElementCollector(doc);

                IEnumerable<FamilyInstance> instances = col
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .OfType<FamilyInstance>();

                foreach (FamilyInstance fi in instances)
                {
                    long id = fi.Id.Value;
                    if (seen.Add(id) && HasMepConnectors(fi))
                        result.Add(fi);
                }
            }

            return result;
        }

        private static bool HasMepConnectors(FamilyInstance fi)
        {
            try
            {
                MEPModel? mep = fi.MEPModel;
                if (mep == null)
                    return false;

                ConnectorManager? cm = mep.ConnectorManager;
                if (cm == null)
                    return false;

                foreach (Connector _ in cm.Connectors)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resultado de um agrupamento: chave do tipo de caixa (Família +
        /// Symbol), o <see cref="FamilySymbol"/> de referência, o diâmetro
        /// do conector vertical (em mm) inferido da primeira instância e
        /// a lista de instâncias do projeto desse tipo. <c>DiameterMm</c>
        /// fica em zero quando nenhuma instância amostra teve conector
        /// vertical legível.
        /// </summary>
        public sealed class BoxGroup
        {
            public BoxGroup(
                FamilySymbol symbol,
                string familyName,
                string symbolName,
                double diameterMm,
                string materialKind,
                IReadOnlyList<FamilyInstance> instances)
            {
                Symbol = symbol;
                FamilyName = familyName;
                SymbolName = symbolName;
                DiameterMm = diameterMm;
                MaterialKind = materialKind;
                Instances = instances;
            }

            public FamilySymbol Symbol { get; }
            public string FamilyName { get; }
            public string SymbolName { get; }
            public double DiameterMm { get; }
            public string MaterialKind { get; }
            public IReadOnlyList<FamilyInstance> Instances { get; }
        }

        public static List<BoxGroup> GroupBySymbol(IReadOnlyList<FamilyInstance> boxes)
        {
            Dictionary<long, List<FamilyInstance>> bucket = new();
            Dictionary<long, FamilySymbol> sampleSymbols = new();

            foreach (FamilyInstance fi in boxes)
            {
                FamilySymbol? sym;
                try { sym = fi.Symbol; }
                catch { continue; }
                if (sym == null) continue;

                long key = sym.Id.Value;
                if (!bucket.TryGetValue(key, out List<FamilyInstance>? list))
                {
                    list = new List<FamilyInstance>();
                    bucket[key] = list;
                    sampleSymbols[key] = sym;
                }
                list.Add(fi);
            }

            List<BoxGroup> groups = new();
            foreach (KeyValuePair<long, List<FamilyInstance>> kv in bucket)
            {
                FamilySymbol sym = sampleSymbols[kv.Key];
                string familyName = SafeGet(() => sym.Family.Name);
                string symbolName = SafeGet(() => sym.Name);

                double diameterMm = TryGetVerticalConnectorDiameterMm(kv.Value);
                string materialKind = FloorDrainExtensionPipeTypeResolver.DetermineMaterialKind(
                    new[]
                    {
                        familyName,
                        symbolName,
                        SafeReadParam(sym, BuiltInParameter.ALL_MODEL_TYPE_COMMENTS),
                    });

                groups.Add(new BoxGroup(sym, familyName, symbolName, diameterMm, materialKind, kv.Value));
            }

            return groups
                .OrderBy(g => g.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.SymbolName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static double TryGetVerticalConnectorDiameterMm(IReadOnlyList<FamilyInstance> instances)
        {
            // Procura a primeira instância que tenha conector vertical
            // legível e usa o seu raio como referência do diâmetro do
            // prolongamento. As caixas de um mesmo tipo costumam ter
            // exatamente o mesmo diâmetro de saída superior — basta uma
            // amostra para definir o grupo.
            List<string> logs = new();

            foreach (FamilyInstance fi in instances)
            {
                try
                {
                    Connector? vert = VerticalConnectorFinder.Find(fi, logs);
                    if (vert == null)
                        continue;

                    double diameterFeet = vert.Radius * 2.0;
                    double mm = UnitUtils.ConvertFromInternalUnits(diameterFeet, UnitTypeId.Millimeters);
                    return Math.Round(mm, 1);
                }
                catch
                {
                    // Tenta próxima instância.
                }
            }

            return 0;
        }

        private static string SafeGet(Func<string?> getter)
        {
            try { return getter() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeReadParam(Element element, BuiltInParameter id)
        {
            try
            {
                Parameter? p = element.get_Parameter(id);
                return p != null && p.HasValue ? (p.AsString() ?? string.Empty) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
