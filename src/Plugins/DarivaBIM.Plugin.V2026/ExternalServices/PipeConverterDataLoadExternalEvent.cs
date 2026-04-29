using System;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.V2026.Ui;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Plugin.V2026.ExternalServices
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
