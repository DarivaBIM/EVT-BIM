using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ClearTigreCodes;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// <c>ExternalEvent</c> que apaga (zera) o parâmetro Tigre: Código nos
    /// elementos Tigre marcados pelo usuário no WPF.
    /// </summary>
    public sealed class PipeCodesClearExternalEvent
    {
        private readonly PipeCodesClearHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeCodesClearExternalEvent()
        {
            _handler = new PipeCodesClearHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeCodesWindow window, IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));

            _handler.Window = window ?? throw new ArgumentNullException(nameof(window));
            _handler.ElementIds = elementIds.ToList();
            _externalEvent.Raise();
        }
    }

    internal sealed class PipeCodesClearHandler : IExternalEventHandler
    {
        public PipeCodesWindow? Window { get; set; }
        public List<long> ElementIds { get; set; } = new();

        public string GetName() => "EvtBim.PipeCodesClearHandler";

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
                    win.NotifyClearCompleted(new TigreClearResult
                    {
                        Selected = ElementIds.Count,
                        ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                    });
                    return;
                }

                Document doc = uiDoc.Document;
                TigreCodeCleaner cleaner = new(doc);
                ClearTigreCodesUseCase useCase = new(cleaner);
                TigreClearResult result = useCase.Execute(ElementIds);

                win.NotifyClearCompleted(result);
            }
            catch (Exception ex)
            {
                win.NotifyClearCompleted(new TigreClearResult
                {
                    Selected = ElementIds.Count,
                    ErrorMessage = ex.Message,
                });
            }
        }
    }
}
