using System;
using Autodesk.Revit.UI;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    public class PipeInsertionExternalEvent
    {
        private readonly PipeInsertionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeInsertionExternalEvent()
        {
            _handler = new PipeInsertionHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(PipeConverterViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }
    }
}
