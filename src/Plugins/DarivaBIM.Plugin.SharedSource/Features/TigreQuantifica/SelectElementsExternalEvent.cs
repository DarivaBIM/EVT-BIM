using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// ExternalEvent que recebe uma lista de IDs (longs) de elementos e
    /// executa <c>UIDocument.Selection.SetElementIds</c> + <c>ShowElements</c>
    /// pra que a view ativa do Revit deixe os IDs selecionados e zoom-fit.
    /// Sem transação — operação UI puro, mas precisa de contexto Revit
    /// válido (UIApplication.ActiveUIDocument), por isso ExternalEvent.
    ///
    /// Slice 4.3.A F2 — alimentado pelo SelectInRevitCommand do
    /// AuditFindingViewModel via callback registrado no code-behind da
    /// janela. UM SÓ ExternalEvent reutilizado entre clicks (não fica
    /// criando handler novo por click).
    /// </summary>
    public sealed class SelectElementsExternalEvent
    {
        private readonly SelectElementsHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public SelectElementsExternalEvent()
        {
            _handler = new SelectElementsHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(IReadOnlyCollection<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            // Snapshot defensivo — chamador pode mutar a lista entre Raise
            // e Execute (Execute roda no idle do Revit, não inline).
            _handler.PendingIds = elementIds.ToArray();
            _externalEvent.Raise();
        }
    }

    internal sealed class SelectElementsHandler : IExternalEventHandler
    {
        public IReadOnlyList<long>? PendingIds { get; set; }

        public string GetName() => "EvtBim.SelectElementsHandler";

        public void Execute(UIApplication app)
        {
            IReadOnlyList<long>? ids = PendingIds;
            if (ids == null || ids.Count == 0)
                return;

            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null)
                return;

            // Filtra IDs ainda válidos no documento ativo. Se o usuário
            // trocou de projeto entre o scan e o click, IDs antigos não
            // resolvem — silenciosamente ignora (não joga TaskDialog).
            Document doc = uiDoc.Document;
            List<ElementId> resolved = new(ids.Count);
            foreach (long idLong in ids)
            {
                ElementId id = new ElementId(idLong);
                if (doc.GetElement(id) != null)
                    resolved.Add(id);
            }

            if (resolved.Count == 0)
                return;

            try
            {
                uiDoc.Selection.SetElementIds(resolved);
                // ShowElements espelha o que o "Selecionar pelo ID" do
                // Revit faz: zoom-fit na view ativa enquadrando os
                // elementos. Não joga exception em IDs inválidos
                // (já filtramos).
                uiDoc.ShowElements(resolved);
            }
            catch
            {
                // Operação UI — qualquer falha rara (view ativa fechada
                // entre Raise e Execute) é ignorada. Usuário continua
                // com janela WPF + Revit funcional.
            }
        }
    }
}
