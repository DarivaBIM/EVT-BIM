using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Uma observação de auditoria gerada pelo Scanner durante a leitura do
    /// projeto. Vermelha = bloqueante pro relatório de compras (ex.: tubo
    /// sem código Tigre), amarela = apenas sinaliza (ex.: descrição vazia).
    /// </summary>
    public sealed class QuantityAuditFinding
    {
        /// <summary>
        /// Id do elemento do Revit, quando aplicável. <c>null</c> para findings
        /// agregadas por categoria (ex.: "Nenhum elemento desta categoria
        /// tem código preenchido").
        /// </summary>
        public long? ElementId { get; init; }

        /// <summary>Texto livre: categoria + família + tipo, pra identificar visualmente.</summary>
        public string FamilyType { get; init; } = string.Empty;

        /// <summary>
        /// Lista de campos ausentes/inválidos (ex.: "Tigre: Código",
        /// "Descrição"). Pode estar vazia quando a finding já é
        /// auto-explicativa via <see cref="FamilyType"/>.
        /// </summary>
        public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

        /// <summary>Severidade visual no painel de findings.</summary>
        public AuditSeverity Severity { get; init; } = AuditSeverity.Yellow;
    }
}
