using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using DarivaBIM.Infrastructure.Persistence.TigreCatalog;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// <c>ExternalEvent</c> que executa a operação "Inserir/Atualizar Códigos"
    /// nos tubos marcados pelo usuário no WPF.
    /// </summary>
    public sealed class PipeCodesApplyExternalEvent
    {
        private readonly PipeCodesApplyHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeCodesApplyExternalEvent()
        {
            _handler = new PipeCodesApplyHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeCodesWindow window, IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));

            _handler.Window = window ?? throw new ArgumentNullException(nameof(window));
            // Snapshot independente — o WPF pode modificar a seleção
            // depois, mas o handler precisa do conjunto exato do clique.
            _handler.ElementIds = elementIds.ToList();
            _externalEvent.Raise();
        }
    }

    internal sealed class PipeCodesApplyHandler : IExternalEventHandler
    {
        public PipeCodesWindow? Window { get; set; }
        public List<long> ElementIds { get; set; } = new();

        public string GetName() => "EvtBim.PipeCodesApplyHandler";

        public void Execute(UIApplication app)
        {
            PipeCodesWindow? win = Window;
            if (win == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    win.NotifyApplyCompleted(new TigreSelectiveApplyResult
                    {
                        Selected = ElementIds.Count,
                        ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                    });
                    return;
                }

                Document doc = uiDoc.Document;
                TigreCodeApplier applier = new(doc, new TigreCatalogJsonLoader());
                ApplyTigreCodesUseCase useCase = new(applier);
                TigreSelectiveApplyResult result = useCase.Execute(ElementIds);

                win.NotifyApplyCompleted(result);
            }
            catch (Exception ex)
            {
                win.NotifyApplyCompleted(new TigreSelectiveApplyResult
                {
                    Selected = ElementIds.Count,
                    ErrorMessage = ex.Message,
                });
            }
        }
    }
}
