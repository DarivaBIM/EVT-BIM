using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Wrapper de external event para o ciclo de seleção do
    /// <see cref="ParameterEditorWindow"/>: <c>PickObjects</c> + cálculo dos
    /// parâmetros comuns aos elementos selecionados.
    /// </summary>
    public class ParameterEditorSelectionExternalEvent
    {
        private readonly ParameterEditorSelectionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ParameterEditorSelectionExternalEvent()
        {
            _handler = new ParameterEditorSelectionHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(ParameterEditorWindow window, IReadOnlyList<Discipline> disciplines)
        {
            _handler.Window = window;
            _handler.Disciplines = disciplines.ToList();
            _externalEvent.Raise();
        }
    }

    internal class ParameterEditorSelectionHandler : IExternalEventHandler
    {
        public ParameterEditorWindow? Window { get; set; }
        public List<Discipline> Disciplines { get; set; } = new();

        public string GetName() => "TigreBIM.ParameterEditorSelectionHandler";

        public void Execute(UIApplication app)
        {
            ParameterEditorWindow? win = Window;
            if (win == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    win.SetStatus("Abra um projeto Revit para selecionar elementos.");
                    win.SetSelectionActive(false);
                    return;
                }

                Document doc = uiDoc.Document;

                // Constroi o filtro de seleção a partir das disciplinas
                // marcadas. Se nenhuma disciplina estiver marcada, não há
                // filtro de categoria — o Revit aceitará qualquer elemento.
                ISelectionFilter? filter = null;
                if (Disciplines.Count > 0)
                {
                    HashSet<long> allowed = DisciplineCategoryMap.UnionCategoryIds(Disciplines);
                    if (allowed.Count > 0)
                        filter = new DisciplineSelectionFilter(allowed);
                }

                IList<Reference> refs;
                try
                {
                    refs = filter != null
                        ? uiDoc.Selection.PickObjects(
                            ObjectType.Element,
                            filter,
                            "Selecione os elementos. ENTER ou ESC para finalizar.")
                        : uiDoc.Selection.PickObjects(
                            ObjectType.Element,
                            "Selecione os elementos. ENTER ou ESC para finalizar.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    win.SetStatus("Seleção cancelada.");
                    win.SetSelectionActive(false);
                    return;
                }

                if (refs == null || refs.Count == 0)
                {
                    win.SetSelection(Array.Empty<ElementId>(), Array.Empty<CommonParameterOption>());
                    return;
                }

                List<Element> elements = refs
                    .Select(r => doc.GetElement(r))
                    .Where(e => e != null)
                    .ToList()!;

                List<ElementId> ids = elements.Select(e => e.Id).ToList();

                // Os parâmetros em comum consideram apenas os elementos
                // selecionados pelo usuário. As famílias aninhadas só são
                // expandidas no momento do Apply, então valores que
                // existirem nelas são preenchidos por consequência (e
                // ignorados quando o parâmetro não está presente).
                IReadOnlyList<CommonParameterOption> common =
                    ParameterEditorService.ComputeCommonParameters(elements);

                win.SetSelection(ids, common);
            }
            catch (Exception ex)
            {
                win.SetStatus($"Erro inesperado na seleção: {ex.Message}");
                win.SetSelectionActive(false);
            }
        }
    }

    /// <summary>
    /// Aceita apenas elementos cuja categoria pertença a uma das disciplinas
    /// marcadas pelo usuário no editor de parâmetros.
    /// </summary>
    internal class DisciplineSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<long> _allowedCategoryIds;

        public DisciplineSelectionFilter(HashSet<long> allowedCategoryIds)
        {
            _allowedCategoryIds = allowedCategoryIds;
        }

        public bool AllowElement(Element elem)
        {
            Category? cat = elem?.Category;
            if (cat == null)
                return false;

            return _allowedCategoryIds.Contains(cat.Id.Value);
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    public class ParameterEditorApplyExternalEvent
    {
        private readonly ParameterEditorApplyHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ParameterEditorApplyExternalEvent()
        {
            _handler = new ParameterEditorApplyHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(
            ParameterEditorWindow window,
            IReadOnlyList<ElementId> ids,
            CommonParameterOption parameter,
            string value)
        {
            _handler.Window = window;
            // ToList() cria um snapshot independente da lista da janela.
            _handler.Ids = ids.ToList();
            _handler.Parameter = parameter;
            _handler.Value = value;
            _externalEvent.Raise();
        }
    }

    internal class ParameterEditorApplyHandler : IExternalEventHandler
    {
        public ParameterEditorWindow? Window { get; set; }
        public List<ElementId> Ids { get; set; } = new();
        public CommonParameterOption? Parameter { get; set; }
        public string Value { get; set; } = string.Empty;

        public string GetName() => "TigreBIM.ParameterEditorApplyHandler";

        public void Execute(UIApplication app)
        {
            ParameterEditorWindow? win = Window;
            if (win == null || Parameter == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    win.SetStatus("Abra um projeto Revit antes de aplicar.");
                    return;
                }

                Document doc = uiDoc.Document;

                // Resolve os elementos e adiciona seus subcomponents (famílias
                // aninhadas) recursivamente. Também inclui o supercomponent
                // raiz quando o usuário seleciona algo aninhado, replicando o
                // comportamento do script Dynamo original.
                List<Element> roots = new();
                HashSet<long> rootIds = new();
                foreach (ElementId id in Ids)
                {
                    Element? el = doc.GetElement(id);
                    if (el == null)
                        continue;

                    Element top = ParameterEditorService.GetTopSuperComponent(el);
                    long tid = top.Id.Value;
                    if (rootIds.Add(tid))
                        roots.Add(top);
                }

                List<Element> targets = ParameterEditorService
                    .ExpandWithNested(doc, roots)
                    .ToList();

                int updated = 0;
                int skipped = 0;
                int failed = 0;

                using Transaction tx = new(doc, $"TigreBIM — Atribuir '{Parameter.Name}'");
                tx.Start();

                foreach (Element elem in targets)
                {
                    Parameter? param = ParameterEditorService.FindParameter(elem, Parameter);
                    if (param == null || param.IsReadOnly)
                    {
                        skipped++;
                        continue;
                    }

                    if (TrySetValue(param, Value))
                        updated++;
                    else
                        failed++;
                }

                tx.Commit();

                string status =
                    $"Aplicado em {updated} elemento(s) (incluindo aninhados). " +
                    $"Pulado(s): {skipped}. Falhou: {failed}.";

                win.SetStatus(status);
            }
            catch (Exception ex)
            {
                win.SetStatus($"Erro ao aplicar: {ex.Message}");
                TaskDialog.Show("TigreBIM", $"Erro ao aplicar parâmetro:\n{ex.Message}");
            }
        }

        private static bool TrySetValue(Parameter param, string value)
        {
            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.Set(value ?? string.Empty);

                    case StorageType.Integer:
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                            return param.Set(i);
                        // Fallback: tenta SetValueString (suporta valores como "Yes/No").
                        return param.SetValueString(value ?? string.Empty);

                    case StorageType.Double:
                        // SetValueString permite ao Revit interpretar a unidade
                        // de exibição do parâmetro (mm, m, ft, °, etc.).
                        // Set(double) usaria unidades internas (feet/radianos),
                        // o que confundiria o usuário; por isso só tentamos
                        // SetValueString aqui.
                        return param.SetValueString(value ?? string.Empty);

                    case StorageType.ElementId:
                        return param.SetValueString(value ?? string.Empty);

                    default:
                        return param.SetValueString(value ?? string.Empty);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class ParameterEditorService
    {
        /// <summary>
        /// Sobe pela hierarquia de <c>FamilyInstance.SuperComponent</c> até
        /// chegar à raiz (família que não está aninhada em outra). Para
        /// elementos que não são <c>FamilyInstance</c> ou que já são raiz,
        /// retorna o próprio elemento.
        /// </summary>
        public static Element GetTopSuperComponent(Element element)
        {
            Element current = element;
            HashSet<long> visited = new();

            while (true)
            {
                if (current is not FamilyInstance fi)
                    return current;

                Element? sc;
                try
                {
                    sc = fi.SuperComponent;
                }
                catch
                {
                    return current;
                }

                if (sc == null)
                    return current;

                long sid = sc.Id.Value;
                if (!visited.Add(sid))
                    return current; // proteção contra loop

                current = sc;
            }
        }

        /// <summary>
        /// Expande uma lista de elementos para incluir todas as famílias
        /// aninhadas (subcomponents) recursivamente. A ordem preserva o
        /// elemento raiz primeiro, seguido pelos seus aninhados.
        /// </summary>
        public static IEnumerable<Element> ExpandWithNested(Document doc, IEnumerable<Element> roots)
        {
            HashSet<long> seen = new();
            Stack<Element> stack = new();

            foreach (Element r in roots)
            {
                if (r != null)
                    stack.Push(r);
            }

            while (stack.Count > 0)
            {
                Element cur = stack.Pop();
                long cid = cur.Id.Value;
                if (!seen.Add(cid))
                    continue;

                yield return cur;

                if (cur is FamilyInstance fi)
                {
                    ICollection<ElementId>? subIds = null;
                    try
                    {
                        subIds = fi.GetSubComponentIds();
                    }
                    catch
                    {
                        subIds = null;
                    }

                    if (subIds == null)
                        continue;

                    foreach (ElementId sid in subIds)
                    {
                        Element? child = doc.GetElement(sid);
                        if (child != null)
                            stack.Push(child);
                    }
                }
            }
        }

        /// <summary>
        /// Calcula a interseção dos parâmetros editáveis (não-ReadOnly) entre os
        /// elementos selecionados, considerando parâmetros de instância e de
        /// tipo. A chave de comparação é (Nome, IsInstance) para permitir
        /// distinguir um parâmetro homônimo presente nos dois escopos.
        /// </summary>
        public static IReadOnlyList<CommonParameterOption> ComputeCommonParameters(IReadOnlyList<Element> elements)
        {
            if (elements.Count == 0)
                return Array.Empty<CommonParameterOption>();

            Dictionary<(string Name, bool IsInstance), CommonParameterOption>? intersection = null;

            foreach (Element el in elements)
            {
                Dictionary<(string, bool), CommonParameterOption> current = CollectEditableParameters(el);

                if (intersection == null)
                {
                    intersection = current;
                    continue;
                }

                Dictionary<(string, bool), CommonParameterOption> next = new();
                foreach (var kv in intersection)
                {
                    if (current.TryGetValue(kv.Key, out CommonParameterOption? other) &&
                        other.StorageType == kv.Value.StorageType)
                    {
                        next[kv.Key] = kv.Value;
                    }
                }
                intersection = next;
            }

            if (intersection == null)
                return Array.Empty<CommonParameterOption>();

            return intersection.Values
                .OrderBy(o => o.IsInstance ? 0 : 1)
                .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static Parameter? FindParameter(Element element, CommonParameterOption option)
        {
            if (option.IsInstance)
            {
                Parameter? p = element.LookupParameter(option.Name);
                if (p != null && !p.IsReadOnly && p.StorageType == option.StorageType)
                    return p;
            }
            else
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    Element? type = element.Document.GetElement(typeId);
                    if (type != null)
                    {
                        Parameter? tp = type.LookupParameter(option.Name);
                        if (tp != null && !tp.IsReadOnly && tp.StorageType == option.StorageType)
                            return tp;
                    }
                }
            }

            return null;
        }

        private static Dictionary<(string Name, bool IsInstance), CommonParameterOption> CollectEditableParameters(Element element)
        {
            Dictionary<(string, bool), CommonParameterOption> map = new();

            foreach (Parameter p in element.Parameters)
                AddIfEditable(map, p, isInstance: true);

            ElementId typeId = element.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
            {
                Element? type = element.Document.GetElement(typeId);
                if (type != null)
                {
                    foreach (Parameter p in type.Parameters)
                        AddIfEditable(map, p, isInstance: false);
                }
            }

            return map;
        }

        private static void AddIfEditable(
            Dictionary<(string Name, bool IsInstance), CommonParameterOption> map,
            Parameter p,
            bool isInstance)
        {
            if (p == null || p.IsReadOnly)
                return;

            Definition? def = p.Definition;
            if (def == null || string.IsNullOrEmpty(def.Name))
                return;

            (string, bool) key = (def.Name, isInstance);
            if (map.ContainsKey(key))
                return;

            map[key] = new CommonParameterOption(def.Name, p.StorageType, isInstance);
        }
    }
}
