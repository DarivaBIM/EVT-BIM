namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Severidade de um <see cref="QuantityAuditFinding"/>. A escolha entre
    /// vermelho e amarelo segue a regra: vermelho bloqueia (informação
    /// indispensável pro relatório de compras), amarelo só sinaliza.
    /// </summary>
    public enum AuditSeverity
    {
        /// <summary>Atenção — informação opcional faltando, relatório segue utilizável.</summary>
        Yellow,

        /// <summary>Crítico — informação indispensável faltando, relatório fica incompleto.</summary>
        Red,
    }
}
