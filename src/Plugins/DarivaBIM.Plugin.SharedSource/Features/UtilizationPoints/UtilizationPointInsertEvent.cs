using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.UtilizationPoints;

namespace DarivaBIM.Plugin.Features.UtilizationPoints
{
    /// <summary>
    /// ExternalEvent que executa o fluxo de inserção: lê a seleção atual ou
    /// dispara um <c>PickObjects</c> (modo "Selecionar elementos e inserir"),
    /// filtra elementos hidráulicos e invoca o adapter Revit-side dentro de
    /// uma transação. Se a janela estiver em modo contínuo, reagenda-se ao
    /// terminar.
    /// </summary>
    public class UtilizationPointInsertEvent
    {
        private readonly UtilizationPointInsertHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public UtilizationPointInsertEvent()
        {
            _handler = new UtilizationPointInsertHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(
            UtilizationPointInsertionWindow window,
            UtilizationPointGroup group,
            long? referenceLevelId,
            bool useCurrentSelection)
        {
            _handler.Window = window;
            _handler.Group = group;
            _handler.ReferenceLevelId = referenceLevelId;
            _handler.UseCurrentSelection = useCurrentSelection;
            _externalEvent.Raise();
        }
    }

    internal class UtilizationPointInsertHandler : IExternalEventHandler
    {
        public UtilizationPointInsertionWindow? Window { get; set; }
        public UtilizationPointGroup? Group { get; set; }
        public long? ReferenceLevelId { get; set; }
        public bool UseCurrentSelection { get; set; }

        public string GetName() => "EvtBim.UtilizationPointInsertHandler";

        public void Execute(UIApplication app)
        {
            UtilizationPointInsertionWindow? window = Window;
            UtilizationPointGroup? group = Group;
            if (window == null || group == null) return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    window.NotifyCancelled("Abra um projeto Revit para usar a ferramenta.");
                    return;
                }

                Document doc = uiDoc.Document;

                IReadOnlyList<long> elementIds = UseCurrentSelection
                    ? GetCurrentSelection(uiDoc)
                    : PickElements(uiDoc, window);

                if (elementIds == null)
                {
                    // Pick cancelled by the user (already notified).
                    return;
                }

                if (elementIds.Count == 0)
                {
                    window.NotifyCancelled("Nenhum elemento hidráulico válido selecionado.");
                    return;
                }

                RevitUtilizationPointInsertionService service = new(doc);
                InsertionSummaryDto summary = service.Insert(elementIds, group, ReferenceLevelId);

                window.ApplyInsertionSummary(summary);

                // Modo contínuo: imediatamente reagenda o pick para um novo
                // ciclo, mantendo a janela aberta — só faz sentido para o
                // "Selecionar elementos e inserir", que é o caminho que aceita
                // múltiplas execuções consecutivas.
                if (!UseCurrentSelection && window.IsContinuousMode)
                {
                    Group = group;
                    UseCurrentSelection = false;
                    new UtilizationPointInsertEvent().Raise(window, group, ReferenceLevelId, false);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                window.NotifyCancelled("Seleção cancelada.");
            }
            catch (Exception ex)
            {
                window.NotifyCancelled($"Erro inesperado: {ex.Message}");
                TaskDialog.Show("EVT-BIM", $"Erro ao inserir pontos de utilização:\n{ex.Message}");
            }
        }

        private static IReadOnlyList<long> GetCurrentSelection(UIDocument uiDoc)
        {
            ICollection<ElementId> selected = uiDoc.Selection.GetElementIds();
            if (selected == null || selected.Count == 0) return Array.Empty<long>();

            Document doc = uiDoc.Document;
            HydraulicSelectionFilter filter = new();
            List<long> result = new();
            foreach (ElementId id in selected)
            {
                Element? element = doc.GetElement(id);
                if (element != null && filter.AllowElement(element))
                {
                    result.Add(id.Value);
                }
            }
            return result;
        }

        private static IReadOnlyList<long>? PickElements(UIDocument uiDoc, UtilizationPointInsertionWindow window)
        {
            HydraulicSelectionFilter filter = new();
            IList<Reference> refs;
            try
            {
                refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    filter,
                    "Selecione tubos/conexões/acessórios. ENTER para confirmar, ESC para cancelar.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                window.NotifyCancelled("Seleção cancelada.");
                return null;
            }

            if (refs == null || refs.Count == 0) return Array.Empty<long>();

            Document doc = uiDoc.Document;
            return refs
                .Select(r => doc.GetElement(r))
                .Where(e => e != null)
                .Select(e => e!.Id.Value)
                .ToList();
        }
    }
}
