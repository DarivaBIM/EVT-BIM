using System;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="MarkerBatchHandler"/>. Disparado pelo botão de criação em
    /// lote (unifilar do layer inteiro ou detecção bifilar automática).
    /// </summary>
    public class MarkerBatchExternalEvent
    {
        private readonly MarkerBatchHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public MarkerBatchExternalEvent()
        {
            _handler = new MarkerBatchHandler();
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
