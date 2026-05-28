namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Nivel de severidade de um <see cref="TopologyDiagnostic"/>. Separa ruido
    /// informativo de problemas que invalidam a leitura topologica, permitindo
    /// que o pipeline decida abortar, degradar ou seguir.
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }
}
