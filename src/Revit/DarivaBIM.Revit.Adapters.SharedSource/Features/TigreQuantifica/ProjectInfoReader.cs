using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Revit.Adapters.Features.TigreQuantifica
{
    /// <summary>
    /// Lê o cabeçalho do projeto a partir de <see cref="Document.ProjectInformation"/>.
    /// Campos não preenchidos pelo usuário NÃO são substituídos por
    /// <c>Environment.UserName</c> ou <c>DateTime.Now</c> — isso mascararia
    /// o gap pro reviewer e quebraria a auditoria. Em vez disso, devolvemos
    /// o literal "(não preenchido)" + uma <see cref="QuantityAuditFinding"/>
    /// amarela por campo faltante.
    /// </summary>
    internal static class ProjectInfoReader
    {
        public const string MissingPlaceholder = "(não preenchido)";

        public static ProjectInfoReadResult Read(Document doc)
        {
            ProjectInfo info = doc.ProjectInformation;

            string name = NormalizeOrPlaceholder(info?.Name);
            string client = NormalizeOrPlaceholder(info?.ClientName);
            string author = NormalizeOrPlaceholder(info?.Author);
            string issueDate = NormalizeOrPlaceholder(info?.IssueDate);

            ProjectInfoDto dto = new ProjectInfoDto
            {
                Name = name,
                Client = client,
                Author = author,
                IssueDate = issueDate,
                Version = "1.0",
            };

            List<QuantityAuditFinding> findings = new();
            AddMissingFinding(findings, name, "Empreendimento");
            AddMissingFinding(findings, client, "Cliente");
            AddMissingFinding(findings, author, "Autor");
            AddMissingFinding(findings, issueDate, "Data de emissão");

            return new ProjectInfoReadResult(dto, findings);
        }

        private static string NormalizeOrPlaceholder(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return MissingPlaceholder;
            return raw.Trim();
        }

        private static void AddMissingFinding(List<QuantityAuditFinding> sink, string value, string fieldLabel)
        {
            if (value != MissingPlaceholder)
                return;

            sink.Add(new QuantityAuditFinding
            {
                FamilyType = "Informações do projeto",
                MissingFields = new[] { fieldLabel },
                Severity = AuditSeverity.Yellow,
            });
        }
    }

    /// <summary>
    /// Resultado da leitura: o DTO já com placeholders aplicados + as
    /// findings amarelas dos campos faltantes (uma por campo).
    /// </summary>
    internal sealed class ProjectInfoReadResult
    {
        public ProjectInfoReadResult(ProjectInfoDto info, IReadOnlyList<QuantityAuditFinding> findings)
        {
            Info = info;
            Findings = findings;
        }

        public ProjectInfoDto Info { get; }
        public IReadOnlyList<QuantityAuditFinding> Findings { get; }
    }
}
