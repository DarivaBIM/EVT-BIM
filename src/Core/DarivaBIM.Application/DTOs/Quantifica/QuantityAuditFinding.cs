using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Uma observação de auditoria gerada pelo Scanner durante a leitura do
    /// projeto. Vermelha = bloqueante pro relatório de compras (ex.: tubo
    /// sem código Tigre), amarela = apenas sinaliza (ex.: descrição vazia).
    ///
    /// Slice 4.3.A F1 ampliado — <see cref="ElementId"/> singular foi
    /// substituído por <see cref="ElementIds"/> (lista). Findings agregados
    /// por categoria (Tigre: Código ausente, Fabricante ausente, Sistema
    /// ausente, Tigre: Descrição ausente) carregam a lista de IDs dos
    /// elementos com o gap — alimenta o "Corrigir agora" e o
    /// SelectInRevitCommand. Findings ProjectInfo (Cliente/Autor/Data/Versão)
    /// permanecem com lista vazia. O singular <c>ElementId</c> antigo foi
    /// removido — nada no codebase consumia, apenas o
    /// AuditFindingViewModel.ElementIdText, que agora deriva de ElementIds.
    /// </summary>
    public sealed class QuantityAuditFinding
    {
        /// <summary>
        /// IDs de elementos relacionados ao finding. <see cref="Array.Empty{T}"/>
        /// para findings ProjectInfo (não há elemento Revit pra selecionar).
        /// Lista populada quando o finding é agregado de N elementos da
        /// categoria — Slice 4.3.A F1.
        /// </summary>
        public IReadOnlyList<long> ElementIds { get; init; } = Array.Empty<long>();

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

        /// <summary>
        /// <c>true</c> quando o finding representa especificamente o gap
        /// "Tigre: Código ausente em N elemento(s)". Usado pelo "Corrigir
        /// agora" pra habilitar apenas nesses findings (Slice 4.3.A F1
        /// ampliado). Defaults a <c>false</c> — qualquer finding que não
        /// seja desse tipo permanece somente seleção.
        /// </summary>
        public bool IsTigreCodigoMissing { get; init; }
    }
}
