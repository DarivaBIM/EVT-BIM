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
            Assert.False(range.Contains(0.099));
            Assert.False(range.Contains(0.31));
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
