namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>Faixa angular [MinDeg, MaxDeg] de uma regra topologica (secao 19).</summary>
    public sealed record AngleRange
    {
        public double MinDeg { get; init; }

        public double MaxDeg { get; init; }
    }
}
