using System;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="BifilarMarkerLinePickHandler"/>. Mesmo padrão de "re-arm"
    /// usado pelo unifilar: ao final de cada pick, o handler chama o
    /// callback e o evento é raise novamente, mantendo o loop até ESC.
    /// </summary>
    public class BifilarMarkerLinePickExternalEvent
    {
        private readonly BifilarMarkerLinePickHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public BifilarMarkerLinePickExternalEvent()
        {
            _handler = new BifilarMarkerLinePickHandler();
            _externalEvent = ExternalEvent.Create(_handler);
            _handler.RearmRequested = () => _externalEvent.Raise();
        }

        public void Raise(PipeConverterViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }

        public void RaiseIfActive(PipeConverterViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (!viewModel.IsActive) return;
            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }

        public void MarkNextCancelAsInternal()
        {
            _handler.RequestInternalCancel();
        }
    }
}
