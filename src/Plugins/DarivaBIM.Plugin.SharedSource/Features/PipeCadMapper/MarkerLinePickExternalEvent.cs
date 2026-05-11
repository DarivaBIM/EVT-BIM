using System;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="MarkerLinePickHandler"/>. Expõe um callback
    /// <c>RearmRequested</c> para que o handler se re-agende ao final de
    /// cada pick (mantendo o ciclo de seleção vivo enquanto a ferramenta
    /// está ativa).
    /// </summary>
    public class MarkerLinePickExternalEvent
    {
        private readonly MarkerLinePickHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public MarkerLinePickExternalEvent()
        {
            _handler = new MarkerLinePickHandler();
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
