using ChargeMaster.Services.ElectricityPrice;

namespace ChargeMaster.xUnit.Services.ElectricityPrice;

public class ElectricityPriceTests
{
    [Theory]
    // Winter Months (Nov-Mar) - Blocked between 07:00 and 19:00
    [InlineData(11, 7, false)]  // Start of block
    [InlineData(11, 18, false)] // End of block (inclusive/exclusive boundary check)
    [InlineData(12, 12, false)]
    [InlineData(1, 10, false)]
    [InlineData(3, 15, false)]
    
    // Winter Months (Nov-Mar) - Allowed outside 07:00 - 19:00
    [InlineData(11, 6, true)]   // Just before block
    [InlineData(11, 19, true)]  // Just after block
    [InlineData(1, 0, true)]
    [InlineData(3, 23, true)]

    // Summer Months (Apr-Oct) - Always allowed
    [InlineData(4, 7, true)]
    [InlineData(4, 12, true)]
    [InlineData(10, 18, true)]
    [InlineData(6, 12, true)]
    public void ChargingAllowed_ReturnsExpectedValue_BasedOnMonthAndHour(int month, int hour, bool expected)
    {
        // Arrange
        // Year and Day do not impact the logic, but must be valid
        var price = new ChargeMaster.Services.ElectricityPrice.ElectricityPrice
        {
            TimeStart = new DateTime(2023, month, 15, hour, 0, 0)
        };

        // Act
        var result = price.ChargingAllowed;

        // Assert
        Assert.Equal(expected, result);
    }
}