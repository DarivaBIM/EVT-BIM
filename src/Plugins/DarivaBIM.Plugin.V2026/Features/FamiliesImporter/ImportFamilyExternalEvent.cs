using System;
using Autodesk.Revit.UI;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Plugin.V2026.Features.FamiliesImporter
{
    public class ImportFamilyExternalEvent
    {
        private readonly ImportFamilyHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ImportFamilyExternalEvent()
        {
            _handler = new ImportFamilyHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(ImportFamilyRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _handler.PendingRequest = request;
            _externalEvent.Raise();
        }
    }
}