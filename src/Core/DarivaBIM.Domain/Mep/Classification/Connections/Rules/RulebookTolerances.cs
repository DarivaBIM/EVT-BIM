namespace DarivaBIM.Domain.Mep.Classification.Connections.Rules
{
    /// <summary>Tolerancias globais do rulebook (secao 19): angulo e diametro em mm.</summary>
    public sealed record RulebookTolerances
    {
        public int AngleDeg { get; init; }

        public int DiameterMm { get; init; }
    }
}
