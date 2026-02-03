using ChargeMaster.Models;

namespace ChargeMaster.xUnit.Models;

public sealed class WallboxSchemaEntryTests
{
    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse()
    {
        var a = new WallboxSchemaEntry
        {
            Start = "08:00:00",
            Stop = "10:00:00",
            Weekday = "1"
        };

        Assert.False(a.Equals(null));
    }

    [Fact]
    public void Equals_WhenSameReference_ReturnsTrue()
    {
        var a = new WallboxSchemaEntry
        {
            Start = "08:00:00",
            Stop = "10:00:00",
            Weekday = "1"
        };

        Assert.True(a.Equals(a));
    }

    [Fact]
    public void Equals_WhenSameStartStopWeekday_ReturnsTrue()
    {
        var a = new WallboxSchemaEntry
        {
            SchemaId = 1,
            Start = "19:00:00",
            Stop = "24:00:00",
            Weekday = "1",
            ChargeLimit = 6
        };

        var b = new WallboxSchemaEntry
        {
            SchemaId = 999,          // should not matter for Equals
            Start = "19:00:00",
            Stop = "00:00:00",
            Weekday = "1",
            ChargeLimit = 16         // should not matter for Equals
        };

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData("07:00:00", "10:00:00", "1", "08:00:00", "10:00:00", "1")] // Start differs
    [InlineData("08:00:00", "09:00:00", "1", "08:00:00", "10:00:00", "1")] // Stop differs
    [InlineData("08:00:00", "10:00:00", "2", "08:00:00", "10:00:00", "1")] // Weekday differs
    public void Equals_WhenAnyComparedFieldDiffers_ReturnsFalse(
        string startA, string stopA, string weekdayA,
        string startB, string stopB, string weekdayB)
    {
        var a = new WallboxSchemaEntry { Start = startA, Stop = stopA, Weekday = weekdayA };
        var b = new WallboxSchemaEntry { Start = startB, Stop = stopB, Weekday = weekdayB };

        Assert.False(a.Equals(b));
        Assert.False(a.Equals((object)b));
    }

    [Fact]
    public void Equals_IsOrdinalAndCaseSensitive()
    {
        var a = new WallboxSchemaEntry { Start = "08:00:00", Stop = "10:00:00", Weekday = "mon" };
        var b = new WallboxSchemaEntry { Start = "08:00:00", Stop = "10:00:00", Weekday = "Mon" };

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_WhenBothHaveNullFields_TreatsNullsAsEqual()
    {
        var a = new WallboxSchemaEntry { Start = null, Stop = null, Weekday = null };
        var b = new WallboxSchemaEntry { Start = null, Stop = null, Weekday = null };

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}