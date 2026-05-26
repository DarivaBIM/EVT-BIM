using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Application.UseCases.GenerateQuantitySnapshot;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Infrastructure.Persistence.TigreCatalog;
using DarivaBIM.Plugin.Ui;
using DarivaBIM.Revit.Adapters.Features.TigreQuantifica;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// <c>ExternalEvent</c> que dispara a varredura para a janela
    /// "Tigre Quantifica". Sem transação — é leitura pura.
    /// Espelha o padrão de <c>PipeCodesScanExternalEvent</c>: wrapper +
    /// handler internal sealed no mesmo arquivo, scanner instanciado direto
    /// (sem DI), tratamento friendly de IsFamilyDocument e exceções.
    /// </summary>
    public sealed class QuantityScanExternalEvent
    {
        private readonly QuantityScanHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public QuantityScanExternalEvent()
        {
            _handler = new QuantityScanHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(TigreQuantificaWindow window)
        {
            _handler.Window = window ?? throw new ArgumentNullException(nameof(window));
            _externalEvent.Raise();
        }
    }

    internal sealed class QuantityScanHandler : IExternalEventHandler
    {
        public TigreQuantificaWindow? Window { get; set; }

        public string GetName() => "EvtBim.QuantityScanHandler";

        public void Execute(UIApplication app)
        {
            TigreQuantificaWindow? win = Window;
            if (win == null)
                return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    win.NotifyScanCompleted(new QuantitySnapshot
                    {
                        ErrorMessage = "Abra um projeto Revit (.rvt) para usar esta ferramenta.",
                    });
                    return;
                }

                Document doc = uiDoc.Document;
                // Slice 2D — Scanner agora consome o detector "é Tigre?"
                // (TigreManufacturerDetector via QuantityCategoryMap.
                // ShouldExpectTigreCode) pra audit POR ELEMENTO. Loader
                // do catálogo é instanciado por scan (espelhando padrão
                // do PipeCodesScanExternalEvent), sem cache cross-scan
                // — o JSON é leitura única + cheap.
                TigreCatalog catalog = new TigreCatalogJsonLoader().Load();
                QuantityScanner scanner = new(doc, catalog);
                GenerateQuantitySnapshotUseCase useCase = new(scanner);
                QuantitySnapshot snapshot = useCase.Execute();

                win.NotifyScanCompleted(snapshot);
            }
            catch (Exception ex)
            {
                win.NotifyScanCompleted(new QuantitySnapshot
                {
                    ErrorMessage = $"Falha ao varrer o projeto: {ex.Message}",
                });
            }
        }
    }
}
