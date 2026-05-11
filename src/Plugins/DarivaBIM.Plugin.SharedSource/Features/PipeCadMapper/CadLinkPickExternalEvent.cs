using System;
using Autodesk.Revit.UI;
using DarivaBIM.Presentation.Wpf.PipeConverter;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="CadLinkPickHandler"/>. Disparado ao clicar em
    /// "Selecionar vínculo CAD" na janela.
    /// </summary>
    public class CadLinkPickExternalEvent
    {
        private readonly CadLinkPickHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public CadLinkPickExternalEvent()
        {
            _handler = new CadLinkPickHandler();
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
