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
    /// ExternalEvent que executa o ciclo de inserção: enquanto a janela
    /// mantém o flag <c>IsLoopActive</c>, dispara <c>PickObjects</c> mostrando
    /// o prompt da ribbon do Revit ("Concluir"/ENTER finaliza um lote, ESC
    /// encerra o ciclo). Cada lote inserido é refletido nas chips de resumo
    /// sem interromper o loop.
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

            string endMessage = "Inserção encerrada.";
            int batches = 0;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    window.NotifyInsertionEnded("Abra um projeto Revit para usar a ferramenta.");
                    return;
                }

                Document doc = uiDoc.Document;
                RevitUtilizationPointInsertionService service = new(doc);

                while (window.IsLoopActive)
                {
                    IReadOnlyList<long>? elementIds = PickElements(uiDoc);
                    if (elementIds == null)
                    {
                        endMessage = batches == 0
                            ? "Seleção cancelada."
                            : $"Inserção encerrada após {batches} lote(s).";
                        break;
                    }

                    if (elementIds.Count == 0)
                    {
                        // PickObjects retornou lista vazia (raro). Mantém o loop.
                        continue;
                    }

                    InsertionSummaryDto summary = service.Insert(elementIds, group, ReferenceLevelId);
                    batches++;
                    window.OnInsertionBatchCompleted(summary);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                endMessage = batches == 0
                    ? "Seleção cancelada."
                    : $"Inserção encerrada após {batches} lote(s).";
            }
            catch (Exception ex)
            {
                endMessage = $"Erro inesperado: {ex.Message}";
                TaskDialog.Show("EVT-BIM", $"Erro ao inserir pontos de utilização:\n{ex.Message}");
            }
            finally
            {
                window.NotifyInsertionEnded(endMessage);
            }
        }

        private static IReadOnlyList<long>? PickElements(UIDocument uiDoc)
        {
            HydraulicSelectionFilter filter = new();
            IList<Reference> refs;
            try
            {
                refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    filter,
                    "Selecione tubos/conexões com conectores livres. ENTER ou Concluir aplica o lote; ESC encerra.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
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
