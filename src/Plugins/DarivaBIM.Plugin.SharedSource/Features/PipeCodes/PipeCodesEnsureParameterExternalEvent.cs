using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.EnsureTigreParameter;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// <c>ExternalEvent</c> que cria/garante o shared parameter Tigre: Código
    /// nas tubulações do projeto. Idempotente.
    /// </summary>
    public sealed class PipeCodesEnsureParameterExternalEvent
    {
        private readonly PipeCodesEnsureParameterHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeCodesEnsureParameterExternalEvent()
        {
            _handler = new PipeCodesEnsureParameterHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeCodesWindow window)
        {
            _handler.Window = window ?? throw new ArgumentNullException(nameof(window));
            _externalEvent.Raise();
        }
    }

    internal sealed class PipeCodesEnsureParameterHandler : IExternalEventHandler
    {
        public PipeCodesWindow? Window { get; set; }

        public string GetName() => "EvtBim.PipeCodesEnsureParameterHandler";

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
                    win.NotifyEnsureParameterCompleted(new TigreEnsureParameterResult
                    {
                        ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                    });
                    return;
                }

                Document doc = uiDoc.Document;
                TigreParameterBinder binder = new(doc);
                EnsureTigreParameterUseCase useCase = new(binder);
                TigreEnsureParameterResult result = useCase.Execute();

                win.NotifyEnsureParameterCompleted(result);
            }
            catch (Exception ex)
            {
                win.NotifyEnsureParameterCompleted(new TigreEnsureParameterResult
                {
                    ErrorMessage = ex.Message,
                });
            }
        }
    }
}
