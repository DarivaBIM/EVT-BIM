using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;
using DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Handler "one-shot" que recalcula
    /// <see cref="PipeConverterViewModel.ActiveViewMarkerCount"/> a partir
    /// da vista ativa. Usado ao abrir a janela e ao trocar de vista para
    /// que o botão "Converter marcadores em tubos" habilite/desabilite
    /// corretamente.
    /// </summary>
    public class MarkerCountRefreshHandler : IExternalEventHandler
    {
        public PipeConverterViewModel? ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            PipeConverterViewModel? vm = ViewModel;
            if (vm == null) return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    vm.ActiveViewMarkerCount = 0;
                    return;
                }

                Document doc = uiDoc.Document;
                View activeView = doc.ActiveView;
                vm.ActiveViewMarkerCount = PipeMarkerCollector.CountInView(doc, activeView);
            }
            catch
            {
                vm.ActiveViewMarkerCount = 0;
            }
        }

        public string GetName() => "EvtBim.MarkerCountRefreshHandler";
    }

    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> para
    /// <see cref="MarkerCountRefreshHandler"/>.
    /// </summary>
    public class MarkerCountRefreshExternalEvent
    {
        private readonly MarkerCountRefreshHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public MarkerCountRefreshExternalEvent()
        {
            _handler = new MarkerCountRefreshHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeConverterViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }
    }
}
