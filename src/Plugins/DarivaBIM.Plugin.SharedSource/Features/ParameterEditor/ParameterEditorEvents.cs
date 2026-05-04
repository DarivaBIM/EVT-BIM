using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Plugin.Ui;
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.ParameterEditor;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.ParameterEditor;
#endif

namespace DarivaBIM.Plugin.Features.ParameterEditor
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

        public void Raise(
            ParameterEditorWindow window,
            IReadOnlyList<Discipline> disciplines,
            IReadOnlyList<ElementId> previousSelection)
        {
            _handler.Window = window;
            _handler.Disciplines = disciplines.ToList();
            _handler.PreviousSelection = previousSelection.ToList();
            _externalEvent.Raise();
        }
    }

    internal class ParameterEditorSelectionHandler : IExternalEventHandler
    {
        public ParameterEditorWindow? Window { get; set; }
        public List<Discipline> Disciplines { get; set; } = new();
        public List<ElementId> PreviousSelection { get; set; } = new();

        public string GetName() => "EvtBim.ParameterEditorSelectionHandler";

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

                // Reconstrói References dos elementos previamente selecionados
                // que ainda existem e satisfazem o filtro. Isso permite ao
                // usuário continuar adicionando/removendo elementos via
                // Ctrl/Shift+clique sem perder o que já estava selecionado.
                List<Reference> preselected = new();
                foreach (ElementId id in PreviousSelection)
                {
                    Element? el = doc.GetElement(id);
                    if (el == null)
                        continue;

                    if (filter != null && !filter.AllowElement(el))
                        continue;

                    try
                    {
                        preselected.Add(new Reference(el));
                    }
                    catch
                    {
                        // Se o elemento não suporta criação de Reference (raro
                        // para FamilyInstance/Pipe/etc.), ignora silenciosamente.
                    }
                }

                const string prompt =
                    "Selecione os elementos. Ctrl+clique adiciona, Shift+clique remove. " +
                    "Clique em Concluir na ribbon para finalizar.";

                // Garante que a janela do Revit fique em primeiro plano antes
                // do PickObjects. Sem isso, a janela WPF Topmost do editor
                // pode reter o foco do teclado e capturar ENTER/ESC, fazendo
                // com que o usuário precise clicar no Concluir manualmente.
                EnsureRevitForeground();

                IList<Reference> refs;
                try
                {
                    refs = PickObjectsCompat(uiDoc, filter, prompt, preselected);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // ESC cancela todo o pick. Mantemos a seleção anterior
                    // intacta para não punir o usuário por um cancelamento
                    // acidental.
                    win.SetSelectionActive(false);
                    if (PreviousSelection.Count > 0)
                        win.SetStatus(
                            $"Seleção cancelada. {PreviousSelection.Count} elemento(s) ainda selecionado(s).");
                    else
                        win.SetStatus("Seleção cancelada.");
                    return;
                }

                if (refs == null || refs.Count == 0)
                {
                    win.SetSelection(
                        Array.Empty<ElementId>(),
                        Array.Empty<CommonParameterOption>(),
                        string.Empty);
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

                string categoriesSummary = BuildCategoriesSummary(elements);

                // Persiste a seleção como seleção corrente do Revit, dando
                // feedback visual ao usuário (elementos ficam destacados na
                // viewport).
                try
                {
                    uiDoc.Selection.SetElementIds(ids);
                }
                catch
                {
                    // Falha ao alterar a seleção do Revit não é fatal.
                }

                win.SetSelection(ids, common, categoriesSummary);
            }
            catch (Exception ex)
            {
                win.SetStatus($"Erro inesperado na seleção: {ex.Message}");
                win.SetSelectionActive(false);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void EnsureRevitForeground()
        {
            try
            {
                IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
                if (handle != IntPtr.Zero)
                    SetForegroundWindow(handle);
            }
            catch
            {
                // Falha em mover o foco não é fatal — o usuário ainda pode
                // clicar em Concluir.
            }
        }

        // Encapsula os overloads de PickObjects. Quando não há filtro específico
        // (todas as disciplinas marcadas), usamos um filtro pass-through para
        // poder utilizar o overload com pPreSelected — esse overload só existe
        // na API com ISelectionFilter, então um filtro nulo não cobriria o
        // caminho de seleção incremental.
        private static IList<Reference> PickObjectsCompat(
            UIDocument uiDoc,
            ISelectionFilter? filter,
            string prompt,
            IList<Reference> preselected)
        {
            ISelectionFilter effectiveFilter = filter ?? AcceptAllSelectionFilter.Instance;

            if (preselected.Count == 0)
                return uiDoc.Selection.PickObjects(ObjectType.Element, effectiveFilter, prompt);

            return uiDoc.Selection.PickObjects(ObjectType.Element, effectiveFilter, prompt, preselected);
        }

        private static string BuildCategoriesSummary(IReadOnlyList<Element> elements)
        {
            if (elements.Count == 0)
                return string.Empty;

            Dictionary<string, int> counts = new();
            foreach (Element el in elements)
            {
                string name = el.Category?.Name ?? "Sem categoria";
                counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
            }

            // Top 4 categorias para evitar mensagens longas demais; o resto
            // entra como "+N outras".
            const int maxShown = 4;
            var ordered = counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IEnumerable<string> shown = ordered
                .Take(maxShown)
                .Select(kv => $"{kv.Key} ({kv.Value})");

            string result = string.Join(", ", shown);

            int remaining = ordered.Count - maxShown;
            if (remaining > 0)
                result += $" +{remaining} outra(s)";

            return result;
        }
    }

    /// <summary>
    /// Filtro de seleção que aceita qualquer elemento. Usado quando o usuário
    /// não quer restringir por disciplina, mas o overload de
    /// <c>PickObjects</c> com pré-seleção exige um <see cref="ISelectionFilter"/>.
    /// </summary>
    internal class AcceptAllSelectionFilter : ISelectionFilter
    {
        public static readonly AcceptAllSelectionFilter Instance = new();

        public bool AllowElement(Element elem) => elem != null;

        public bool AllowReference(Reference reference, XYZ position) => false;
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

    /// <summary>
    /// Resultado consolidado da aplicação do parâmetro, usado para montar o
    /// TaskDialog final mostrado ao usuário.
    /// </summary>
    public class ParameterApplyResult
    {
        public bool Success { get; init; }
        public string ParameterName { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public int TopLevelCount { get; init; }
        public int Updated { get; init; }
        public int UpdatedNested { get; init; }
        public int SkippedReadOnly { get; init; }
        public int SkippedMissing { get; init; }
        public int Failed { get; init; }
        public string? ErrorMessage { get; init; }
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

        public string GetName() => "EvtBim.ParameterEditorApplyHandler";

        public void Execute(UIApplication app)
        {
            ParameterEditorWindow? win = Window;
            if (win == null || Parameter == null)
                return;

            string parameterName = Parameter.Name;
            string value = Value;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    ParameterApplyResult missingDoc = new()
                    {
                        Success = false,
                        ParameterName = parameterName,
                        Value = value,
                        ErrorMessage = "Nenhum projeto Revit ativo.",
                    };
                    win.NotifyApplyCompleted(missingDoc);
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

                int topLevelCount = roots.Count;

                List<Element> targets = ParameterEditorService
                    .ExpandWithNested(doc, roots)
                    .ToList();

                int updatedTopLevel = 0;
                int updatedNested = 0;
                int skippedReadOnly = 0;
                int skippedMissing = 0;
                int failed = 0;

                using Transaction tx = new(doc, $"EVT-BIM — Atribuir '{Parameter.Name}'");
                tx.Start();

                foreach (Element elem in targets)
                {
                    bool isTopLevel = rootIds.Contains(elem.Id.Value);

                    Parameter? param = ParameterEditorService.FindParameter(elem, Parameter);
                    if (param == null)
                    {
                        // Para os elementos top-level isso seria estranho (já
                        // que o parâmetro foi escolhido entre os comuns), mas
                        // é esperado em famílias aninhadas que não têm o
                        // parâmetro.
                        skippedMissing++;
                        continue;
                    }

                    if (param.IsReadOnly)
                    {
                        skippedReadOnly++;
                        continue;
                    }

                    if (TrySetValue(param, value))
                    {
                        if (isTopLevel)
                            updatedTopLevel++;
                        else
                            updatedNested++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                tx.Commit();

                ParameterApplyResult result = new()
                {
                    Success = failed == 0,
                    ParameterName = parameterName,
                    Value = value,
                    TopLevelCount = topLevelCount,
                    Updated = updatedTopLevel,
                    UpdatedNested = updatedNested,
                    SkippedReadOnly = skippedReadOnly,
                    SkippedMissing = skippedMissing,
                    Failed = failed,
                };

                win.NotifyApplyCompleted(result);
            }
            catch (Exception ex)
            {
                ParameterApplyResult errorResult = new()
                {
                    Success = false,
                    ParameterName = parameterName,
                    Value = value,
                    ErrorMessage = ex.Message,
                };
                win.NotifyApplyCompleted(errorResult);
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
                if (p != null && p.StorageType == option.StorageType)
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
                        if (tp != null && tp.StorageType == option.StorageType)
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
