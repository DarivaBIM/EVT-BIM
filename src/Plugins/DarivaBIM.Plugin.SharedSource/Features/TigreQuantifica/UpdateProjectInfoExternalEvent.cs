using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Revit.Adapters.Common.Transactions;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// Slice 4.3.B F4 — escreve <see cref="ProjectInfoDto.Client"/> e
    /// <see cref="ProjectInfoDto.Author"/> de volta em
    /// <see cref="Document.ProjectInformation"/> (campos built-in
    /// <c>ClientName</c> e <c>Author</c>). NUNCA cria parâmetro novo;
    /// NÃO escreve em campos que a UI não controla (Name, IssueDate,
    /// Version), mesmo que o DTO carregue valores — preservar
    /// <c>ProjectInformation</c> não-corrompida é mais importante que
    /// implementar tudo de uma vez.
    ///
    /// <para>
    /// Padrão de orquestração espelha
    /// <see cref="QuantityScanExternalEvent"/>: wrapper + handler
    /// internal sealed no mesmo arquivo. Pós-save, dispara um re-scan
    /// pra a UI receber o snapshot fresco (que vai esvaziar o finding
    /// amarelo "Cliente/Autor não preenchido" automaticamente).
    /// </para>
    /// </summary>
    public sealed class UpdateProjectInfoExternalEvent
    {
        private readonly UpdateProjectInfoHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public UpdateProjectInfoExternalEvent(Action<string>? onError = null, Action? onSuccess = null)
        {
            _handler = new UpdateProjectInfoHandler
            {
                OnError = onError,
                OnSuccess = onSuccess,
            };
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(ProjectInfoDto pendingInfo)
        {
            if (pendingInfo == null) throw new ArgumentNullException(nameof(pendingInfo));
            _handler.PendingInfo = pendingInfo;
            _externalEvent.Raise();
        }
    }

    internal sealed class UpdateProjectInfoHandler : IExternalEventHandler
    {
        public ProjectInfoDto? PendingInfo { get; set; }
        public Action<string>? OnError { get; set; }
        public Action? OnSuccess { get; set; }

        public string GetName() => "EvtBim.UpdateProjectInfoHandler";

        public void Execute(UIApplication app)
        {
            ProjectInfoDto? dto = PendingInfo;
            if (dto == null) return;

            try
            {
                UIDocument? uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
                {
                    OnError?.Invoke("Abra um projeto Revit (.rvt) para salvar as informações.");
                    return;
                }

                Document doc = uiDoc.Document;
                ProjectInfo info = doc.ProjectInformation;
                if (info == null)
                {
                    OnError?.Invoke("Este projeto não expõe ProjectInformation editável.");
                    return;
                }

                // Normaliza placeholder "(não preenchido)" pra string vazia
                // (escrever o placeholder no Revit causaria duplo gap no
                // próximo scan — placeholder vira valor real e o
                // ProjectInfoReader não detectaria mais o campo como
                // faltante).
                string client = NormalizePersisted(dto.Client);
                string author = NormalizePersisted(dto.Author);

                RevitTransactionRunner.Run(doc, "EVT-BIM: atualizar Cliente/Autor", () =>
                {
                    if (!string.Equals(info.ClientName ?? string.Empty, client, StringComparison.Ordinal))
                        info.ClientName = client;
                    if (!string.Equals(info.Author ?? string.Empty, author, StringComparison.Ordinal))
                        info.Author = author;
                });

                OnSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Falha ao salvar Cliente/Autor: {ex.Message}");
            }
        }

        private static string NormalizePersisted(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;
            string trimmed = raw.Trim();
            if (string.Equals(trimmed, "(não preenchido)", StringComparison.Ordinal))
                return string.Empty;
            return trimmed;
        }
    }
}
