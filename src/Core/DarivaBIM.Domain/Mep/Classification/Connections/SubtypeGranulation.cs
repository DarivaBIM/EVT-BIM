namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Granulacoes da secao 14 mapeadas por id de regra. O mapa vive no CODIGO (nao no
    /// JSON): nao toca o catalogo nem o POCO de regra, e as 6 valve/instrument rules sao
    /// fixas no MVP 1. O guardrail de cobertura (testes) barra renomeacao/esquecimento —
    /// se uma rule BaseKind=Valve ficar sem granulacao, a distincao registro/retencao/
    /// hidrometro se perderia no <see cref="ConnectionIdentity"/>.
    /// </summary>
    public static class SubtypeGranulation
    {
        /// <summary>ValveKind do id da regra, ou null se a regra nao e uma valvula granulada.</summary>
        public static ValveKind? ValveKindFor(string? ruleId)
            => ruleId switch
            {
                "valve-shutoff" => ValveKind.Shutoff,
                "valve-check" => ValveKind.Check,
                "valve-prv" => ValveKind.PressureReducing,
                "valve-flush" => ValveKind.Flush,
                _ => (ValveKind?)null,
            };

        /// <summary>InstrumentKind do id da regra, ou null se a regra nao e um instrumento.</summary>
        public static InstrumentKind? InstrumentKindFor(string? ruleId)
            => ruleId switch
            {
                "meter" => InstrumentKind.FlowMeter,            // hidrometro
                "instrument-pressure" => InstrumentKind.PressureMeter, // manometro
                _ => (InstrumentKind?)null,
            };
    }
}
