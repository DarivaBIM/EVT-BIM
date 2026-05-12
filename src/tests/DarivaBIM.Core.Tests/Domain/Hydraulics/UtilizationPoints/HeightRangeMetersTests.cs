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
        public void Contains_tolerates_floating_point_drift_at_boundaries()
        {
            // Cenário típico: conector modelado exatamente no limite, mas a
            // conversão pés→metros do Revit produz uma fração sub-milimétrica
            // abaixo/acima. A faixa precisa absorver esse ruído.
            HeightRangeMeters range = new(0.10, 0.30);
            Assert.True(range.Contains(0.0995));
            Assert.True(range.Contains(0.3005));
            // Folga maior que 1 mm já é "fora" — adjacentes não se sobrepõem.
            Assert.False(range.Contains(0.098));
            Assert.False(range.Contains(0.302));
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
