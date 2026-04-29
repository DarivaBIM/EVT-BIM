using System;

namespace DarivaBIM.Domain.ValueObjects
{
    /// <summary>
    /// Pipe nominal diameter in millimeters. Value object; immutable.
    /// </summary>
    public readonly struct Diameter : IEquatable<Diameter>
    {
        public Diameter(double millimeters)
        {
            if (millimeters < 0) throw new ArgumentOutOfRangeException(nameof(millimeters));
            Millimeters = millimeters;
        }

        public double Millimeters { get; }

        public static Diameter FromMillimeters(double mm) => new Diameter(mm);

        public bool Equals(Diameter other) => Millimeters.Equals(other.Millimeters);
        public override bool Equals(object? obj) => obj is Diameter d && Equals(d);
        public override int GetHashCode() => Millimeters.GetHashCode();
        public override string ToString() => $"{Millimeters:0.##} mm";

        public static bool operator ==(Diameter a, Diameter b) => a.Equals(b);
        public static bool operator !=(Diameter a, Diameter b) => !a.Equals(b);
    }
}
