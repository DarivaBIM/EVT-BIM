using System;

namespace DarivaBIM.Domain.ValueObjects
{
    public readonly struct Length : IEquatable<Length>
    {
        public Length(double meters)
        {
            if (meters < 0) throw new ArgumentOutOfRangeException(nameof(meters));
            Meters = meters;
        }

        public double Meters { get; }

        public double Millimeters => Meters * 1000.0;

        public static Length FromMeters(double m) => new Length(m);
        public static Length FromMillimeters(double mm) => new Length(mm / 1000.0);

        public bool Equals(Length other) => Meters.Equals(other.Meters);
        public override bool Equals(object? obj) => obj is Length v && Equals(v);
        public override int GetHashCode() => Meters.GetHashCode();
        public override string ToString() => $"{Meters:0.###} m";

        public static bool operator ==(Length a, Length b) => a.Equals(b);
        public static bool operator !=(Length a, Length b) => !a.Equals(b);
    }
}
