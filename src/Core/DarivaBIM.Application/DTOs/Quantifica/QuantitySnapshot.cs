using System;
using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Snapshot completo lido do projeto pela feature "Tigre Quantifica":
    /// cabeçalho do projeto, lista de grupos quantitativos e findings de
    /// auditoria. É o output do <see cref="DarivaBIM.Application.Contracts.IQuantityScanService"/>
    /// e o input do <c>QuantityCsvWriter</c> e dos ViewModels da janela.
    /// </summary>
    public sealed class QuantitySnapshot
    {
        /// <summary>Cabeçalho do projeto (Empreendimento/Cliente/Autor/Data/Versão).</summary>
        public ProjectInfoDto ProjectInfo { get; init; } = new ProjectInfoDto();

        /// <summary>Linhas agrupadas — uma por chave única (Categoria, Família, Tipo, Diâmetro, ...).</summary>
        public IReadOnlyList<QuantityGroup> Groups { get; init; } = Array.Empty<QuantityGroup>();

        /// <summary>Observações de auditoria geradas durante a varredura.</summary>
        public IReadOnlyList<QuantityAuditFinding> AuditFindings { get; init; } = Array.Empty<QuantityAuditFinding>();

        /// <summary>
        /// Mensagem de erro fatal (ex.: documento de família aberto, modelo
        /// vazio). Quando preenchida, a UI exibe o erro e ignora as listas.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }
}
