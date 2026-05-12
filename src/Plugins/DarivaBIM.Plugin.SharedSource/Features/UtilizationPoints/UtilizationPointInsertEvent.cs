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
    /// ExternalEvent que executa o fluxo de inserção: dispara um
    /// <c>PickObjects</c> mostrando o prompt da ribbon do Revit ("Concluir" /
    /// ENTER finalizam, ESC cancela), filtra elementos hidráulicos e invoca o
    /// adapter Revit-side dentro de uma transação.
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
            long? referenceLevelId)
        {
            _handler.Window = window;
            _handler.Group = group;
            _handler.ReferenceLevelId = referenceLevelId;
            _externalEvent.Raise();
        }
    }

    internal class UtilizationPointInsertHandler : IExternalEventHandler
    {
        public UtilizationPointInsertionWindow? Window { get; set; }
        public UtilizationPointGroup? Group { get; set; }
        public long? ReferenceLevelId { get; set; }

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

                IReadOnlyList<long>? elementIds = PickElements(uiDoc, window);
                if (elementIds == null)
                {
                    // Pick cancelado pelo usuário (NotifyCancelled já chamado).
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

        private static IReadOnlyList<long>? PickElements(UIDocument uiDoc, UtilizationPointInsertionWindow window)
        {
            HydraulicSelectionFilter filter = new();
            IList<Reference> refs;
            try
            {
                refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    filter,
                    "Selecione tubos/conexões/acessórios com conectores livres. ENTER para confirmar, ESC para cancelar.");
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
