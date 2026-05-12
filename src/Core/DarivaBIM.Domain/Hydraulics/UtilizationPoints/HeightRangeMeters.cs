using System;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Faixa fechada de altura (em metros, relativa ao nível de referência) que
    /// determina quando uma <see cref="UtilizationPointRule"/> é aplicável a um
    /// conector hidráulico livre. Os limites são inclusivos: a regra casa
    /// quando <c>Min ≤ altura ≤ Max</c>.
    ///
    /// A comparação aplica uma tolerância simétrica de
    /// <see cref="BoundaryToleranceMeters"/> em torno de cada limite — sem ela
    /// a altura calculada em pés → metros podia escapar do limite por
    /// fração de mícron quando o conector estava modelado exatamente na cota
    /// (ex.: 0,60 m configurado, 0,5999999998 m vindo de Revit).
    /// </summary>
    public readonly struct HeightRangeMeters : IEquatable<HeightRangeMeters>
    {
        /// <summary>
        /// Tolerância aplicada nos limites para neutralizar ruído de
        /// arredondamento na conversão pés↔metros do Revit e dar uma folga
        /// prática de modelagem. 0.02 m (2 cm) é o valor exibido na UI
        /// ("Tolerância ±0.02 m") — gaps típicos entre regras do tool são
        /// de pelo menos 10 cm, então não há risco de overlap acidental.
        /// </summary>
        public const double BoundaryToleranceMeters = 0.02;

        public HeightRangeMeters(double minMeters, double maxMeters)
        {
            MinMeters = minMeters;
            MaxMeters = maxMeters;
        }

        public double MinMeters { get; }
        public double MaxMeters { get; }

        public bool IsValid => MinMeters <= MaxMeters;

        public bool Contains(double heightMeters)
            => heightMeters >= MinMeters - BoundaryToleranceMeters
            && heightMeters <= MaxMeters + BoundaryToleranceMeters;

        public bool Equals(HeightRangeMeters other)
            => MinMeters.Equals(other.MinMeters) && MaxMeters.Equals(other.MaxMeters);

        public override bool Equals(object? obj) => obj is HeightRangeMeters r && Equals(r);
        public override int GetHashCode() => MinMeters.GetHashCode() ^ MaxMeters.GetHashCode();
        public override string ToString() => $"[{MinMeters:0.###} m, {MaxMeters:0.###} m]";

        public static bool operator ==(HeightRangeMeters a, HeightRangeMeters b) => a.Equals(b);
        public static bool operator !=(HeightRangeMeters a, HeightRangeMeters b) => !a.Equals(b);
    }
}
