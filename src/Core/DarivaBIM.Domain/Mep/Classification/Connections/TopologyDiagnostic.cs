namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Item individual produzido pela leitura de topologia. Concentra codigo,
    /// detalhe humano e severidade num so registro imutavel para que o caller
    /// agregue/filtre sem perder contexto e para que o teste compare estrutura
    /// e nao texto. Severidade default e Info: o codigo ja carrega gravidade
    /// implicita, o nivel e refinado pelo emissor quando relevante.
    /// </summary>
    public sealed record TopologyDiagnostic
    {
        public required TopologyDiagnosticCode Code { get; init; }
        public string Detail { get; init; } = "";
        public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;
    }
}
