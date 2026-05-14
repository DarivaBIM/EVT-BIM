using System;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Family;

namespace DarivaBIM.Plugin.Features.FamiliesImporter
{
    /// <summary>
    /// Bridges WPF-side family imports to a Revit-side <see cref="ExternalEvent"/>.
    /// The caller MUST download the .rfa to local cache before raising — the
    /// handler runs on Revit's UI thread and any I/O there freezes the
    /// application until completion.
    /// </summary>
    public class ImportFamilyExternalEvent
    {
        private readonly ImportFamilyHandler _handler;
        private readonly ExternalEvent _externalEvent;

        // Disparado no UI thread do Revit ao fim do handler — qualquer
        // caminho de saída (sucesso, validação, exceção). A UI usa isso
        // pra limpar o flag de "importando" e re-habilitar cliques. Sem
        // o sinal, a janela ficaria bloqueando cliques até que o usuário
        // forçasse algum reset.
        public event Action? Completed;

        public ImportFamilyExternalEvent()
        {
            _handler = new ImportFamilyHandler();
            _handler.Completed = () => Completed?.Invoke();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(ImportFamilyRequest request, string localFilePath)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(localFilePath))
                throw new ArgumentException(
                    "O arquivo local da família precisa estar baixado antes de disparar o evento.",
                    nameof(localFilePath));

            _handler.PendingRequest = request;
            _handler.PendingLocalFilePath = localFilePath;
            _externalEvent.Raise();
        }
    }
}
