using System;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Faixa fechada de altura (em metros, relativa ao nível de referência) que
    /// determina quando uma <see cref="UtilizationPointRule"/> é aplicável a um
    /// conector hidráulico livre. Os limites são inclusivos: a regra casa
    /// quando <c>Min ≤ altura ≤ Max</c>.
    /// </summary>
    public readonly struct HeightRangeMeters : IEquatable<HeightRangeMeters>
    {
        public HeightRangeMeters(double minMeters, double maxMeters)
        {
            MinMeters = minMeters;
            MaxMeters = maxMeters;
        }

        public double MinMeters { get; }
        public double MaxMeters { get; }

        public bool IsValid => MinMeters <= MaxMeters;

        public bool Contains(double heightMeters)
            => heightMeters >= MinMeters && heightMeters <= MaxMeters;

        public bool Equals(HeightRangeMeters other)
            => MinMeters.Equals(other.MinMeters) && MaxMeters.Equals(other.MaxMeters);

        public override bool Equals(object? obj) => obj is HeightRangeMeters r && Equals(r);
        public override int GetHashCode() => MinMeters.GetHashCode() ^ MaxMeters.GetHashCode();
        public override string ToString() => $"[{MinMeters:0.###} m, {MaxMeters:0.###} m]";

        public static bool operator ==(HeightRangeMeters a, HeightRangeMeters b) => a.Equals(b);
        public static bool operator !=(HeightRangeMeters a, HeightRangeMeters b) => !a.Equals(b);
    }
}
