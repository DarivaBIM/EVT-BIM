using System;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Family;

namespace DarivaBIM.Plugin.V2025.Features.FamiliesImporter
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