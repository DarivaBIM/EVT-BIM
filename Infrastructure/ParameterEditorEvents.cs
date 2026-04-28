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

        public void Raise(ParameterEditorWindow window)
        {
            _handler.Window = window;
            _externalEvent.Raise();
        }
    }

    internal class ParameterEditorSelectionHandler : IExternalEventHandler
    {
        public ParameterEditorWindow? Window { get; set; }

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

                IList<Reference> refs;
                try
                {
                    refs = uiDoc.Selection.PickObjects(
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
                IReadOnlyList<CommonParameterOption> common = ParameterEditorService.ComputeCommonParameters(elements);

                win.SetSelection(ids, common);
            }
            catch (Exception ex)
            {
                win.SetStatus($"Erro inesperado na seleção: {ex.Message}");
                win.SetSelectionActive(false);
            }
        }
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

                int updated = 0;
                int skipped = 0;
                int failed = 0;

                using Transaction tx = new(doc, $"TigreBIM — Atribuir '{Parameter.Name}'");
                tx.Start();

                foreach (ElementId id in Ids)
                {
                    Element? elem = doc.GetElement(id);
                    if (elem == null)
                    {
                        skipped++;
                        continue;
                    }

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
                    $"Aplicado em {updated} elemento(s). " +
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
