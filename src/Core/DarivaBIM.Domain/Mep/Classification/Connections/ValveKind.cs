namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Granulacao de <see cref="BaseKind.Valve"/>. Captura o tipo de
    /// dispositivo de fluxo (corte, retencao, reducao de pressao, etc.)
    /// para alimentar perda de carga, validacoes NBR e match contra catalogo
    /// Tigre/Rinnai. Vide secao 14 do rulebook canonico.
    /// </summary>
    public enum ValveKind
    {
        Unknown = 0,
        Shutoff = 1,
        Check = 2,
        PressureReducing = 3,
        Flush = 4,
        Relief = 5,
        Butterfly = 6,
        Ball = 7,
        Gate = 8,
    }
}
