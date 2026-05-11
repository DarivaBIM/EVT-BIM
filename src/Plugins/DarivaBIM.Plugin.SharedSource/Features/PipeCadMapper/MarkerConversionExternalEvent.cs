using System;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="MarkerConversionHandler"/>. Disparado pelo botão
    /// "Converter marcadores em tubos".
    /// </summary>
    public class MarkerConversionExternalEvent
    {
        private readonly MarkerConversionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public MarkerConversionExternalEvent()
        {
            _handler = new MarkerConversionHandler();
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
