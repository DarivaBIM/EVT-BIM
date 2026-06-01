namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Granulacao quando <c>Category=Instrument</c>: manometros, hidrometros,
    /// sensores. Mantido separado de <see cref="ValveKind"/> pq instrumentos
    /// nao tem comportamento de valvula (perda de carga, abertura). Vide
    /// secao 14 do rulebook canonico.
    /// </summary>
    public enum InstrumentKind
    {
        Unknown = 0,
        PressureMeter = 1,
        FlowMeter = 2,
        PressureSensor = 3,
        TemperatureSensor = 4,
        FlowSensor = 5,
    }
}
