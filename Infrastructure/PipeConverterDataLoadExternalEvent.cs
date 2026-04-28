using System;
using Autodesk.Revit.UI;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    public class PipeConverterDataLoadExternalEvent
    {
        private readonly PipeConverterDataLoadHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeConverterDataLoadExternalEvent()
        {
            _handler = new PipeConverterDataLoadHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeConverterViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }

        public void RaiseWithSettings(PipeConverterViewModel viewModel, PipeCadMapperSettings settings)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _handler.ViewModel = viewModel;
            _handler.PendingSettings = settings;
            _externalEvent.Raise();
        }
    }
}
