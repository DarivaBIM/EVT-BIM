using DarivaBIM.Domain.Hydraulics.UtilizationPoints;
using Xunit;

namespace DarivaBIM.Core.Tests.Domain.Hydraulics.UtilizationPoints
{
    public class HeightRangeMetersTests
    {
        [Fact]
        public void Contains_returns_true_for_height_inside_range()
        {
            HeightRangeMeters range = new(0.10, 0.30);
            Assert.True(range.Contains(0.10));
            Assert.True(range.Contains(0.20));
            Assert.True(range.Contains(0.30));
        }

        [Fact]
        public void Contains_returns_false_outside_range()
        {
            HeightRangeMeters range = new(0.10, 0.30);
            Assert.False(range.Contains(0.05));
            Assert.False(range.Contains(0.35));
        }

        [Fact]
        public void Contains_tolerates_drift_within_two_centimeters()
        {
            // A UI promete "Tolerância ±0.02 m". Esse teste fixa esse contrato:
            // pontos até 2 cm fora do limite ainda são aceitos. Maior que 2 cm
            // está fora — gaps típicos entre regras do tool (≥ 10 cm) preservam
            // exclusividade.
            HeightRangeMeters range = new(0.10, 0.30);
            Assert.True(range.Contains(0.085));
            Assert.True(range.Contains(0.319));
            Assert.False(range.Contains(0.07));
            Assert.False(range.Contains(0.33));
        }

        [Fact]
        public void IsValid_requires_min_less_or_equal_max()
        {
            Assert.True(new HeightRangeMeters(0.0, 0.0).IsValid);
            Assert.True(new HeightRangeMeters(0.10, 0.30).IsValid);
            Assert.False(new HeightRangeMeters(0.5, 0.4).IsValid);
        }
    }
}
