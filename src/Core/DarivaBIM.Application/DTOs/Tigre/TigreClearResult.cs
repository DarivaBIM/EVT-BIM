namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Relatório da operação "Deletar Códigos" sobre os tubos selecionados.
    /// Limpar significa zerar o valor do parâmetro Tigre: Código (Integer →
    /// 0; String → vazio).
    /// </summary>
    public sealed class TigreClearResult
    {
        public int Selected { get; init; }

        /// <summary>Tubos cujo valor foi efetivamente zerado.</summary>
        public int Cleared { get; init; }

        /// <summary>Tubos que já estavam vazios — nenhuma alteração feita.</summary>
        public int AlreadyEmpty { get; init; }

        /// <summary>
        /// Tubos sem parâmetro acessível (não há binding ou está read-only).
        /// </summary>
        public int ParameterIssue { get; init; }

        public string? ErrorMessage { get; init; }
    }
}
