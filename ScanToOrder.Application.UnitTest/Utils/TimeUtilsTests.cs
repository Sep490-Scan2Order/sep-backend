using FluentAssertions;
using ScanToOrder.Application.Utils;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Utils
{
    public class TimeUtilsTests
    {
        [Fact]
        public void GetVietnamTimeNow_ShouldReturnTimeWithSevenHourOffsetFromUtc()
        {
            // Arrange
            var before = DateTime.UtcNow.AddHours(7);

            // Act
            var result = TimeUtils.GetVietnamTimeNow();

            // Assert
            var after = DateTime.UtcNow.AddHours(7);
            result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        [Fact]
        public void GetVietnamDayRangeUtc_ShouldReturnValidRange()
        {
            // Act
            var (startUtc, endUtc, dateInt) = TimeUtils.GetVietnamDayRangeUtc();

            // Assert
            endUtc.Should().Be(startUtc.AddDays(1));
            dateInt.ToString().Length.Should().Be(8); // YYYYMMDD
            
            // Check if StartUtc is 00:00:00 VN time (which is 17:00:00 UTC previous day or 00:00:00 UTC)
            // Depending on timezone availability in environment, we check consistency
            startUtc.Kind.Should().Be(DateTimeKind.Utc);
            endUtc.Kind.Should().Be(DateTimeKind.Utc);
        }
    }
}
