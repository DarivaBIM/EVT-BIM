using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ScanTigreCodes;
using DarivaBIM.Infrastructure.Persistence.TigreCatalog;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.TigreCodes;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Wrapper de <c>ExternalEvent</c> que dispara a varredura de
    /// elementos Tigre (Pipes + Conexões + Acessórios + Aparelhos) para
    /// a janela "Codificar Tigre". Sem transação — só leitura.
    ///
    /// Slice 4.3.A F1 ampliado — <see cref="Raise"/> aceita prefilter
    /// opcional de IDs, propagado pro <see cref="TigreCodeScanner"/>
    /// via ctor. Se null/vazio, comportamento original (varredura completa).
    /// </summary>
    public sealed class PipeCodesScanExternalEvent
    {
        private readonly PipeCodesScanHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeCodesScanExternalEvent()
        {
            _handler = new PipeCodesScanHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeCodesWindow window, IReadOnlyCollection<long>? prefilterIds = null)
        {
            _handler.Window = window ?? throw new ArgumentNullException(nameof(window));
            _handler.PrefilterIds = prefilterIds;
            _externalEvent.Raise();
        }
    }

    internal sealed class PipeCodesScanHandler : IExternalEventHandler
    {
        public PipeCodesWindow? Window { get; set; }
        public IReadOnlyCollection<long>? PrefilterIds { get; set; }

        public string GetName() => "EvtBim.PipeCodesScanHandler";

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
                    win.NotifyScanCompleted(new TigreScanResult
                    {
                        ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                    });
                    return;
                }

                Document doc = uiDoc.Document;
                TigreCodeScanner scanner = new(doc, new TigreCatalogJsonLoader(), PrefilterIds);
                ScanTigreCodesUseCase useCase = new(scanner);
                TigreScanResult result = useCase.Execute();

                win.NotifyScanCompleted(result);
            }
            catch (Exception ex)
            {
                win.NotifyScanCompleted(new TigreScanResult
                {
                    ErrorMessage = $"Falha ao varrer os elementos Tigre: {ex.Message}",
                });
            }
        }
    }
}
