using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Workers;
using Microsoft.Extensions.Logging;

namespace ChargeMaster.UnitTests.Workers;

public class WallboxWorkerTests
{
    [Fact]
    public void GenerateHourBoundaryReadings_SameHour_ReturnsSingleEntry()
    {
        // Arrange
        var previousTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var currentTime = new DateTime(2024, 1, 15, 14, 45, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Act
        var result = WallboxWorker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Single(result);
        Assert.Equal(currentTime, result[0].ReadAt);
        Assert.Equal(currentEnergy, result[0].AccEnergy);
    }

    [Fact]
    public void GenerateHourBoundaryReadings_CrossesOneHour_ReturnsTwoEntries()
    {
        // Arrange
        var previousTime = new DateTime(2024, 1, 15, 14, 45, 0);
        var currentTime = new DateTime(2024, 1, 15, 15, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Total tid: 25 min, energi: 100 Wh
        // Tid till 15:00 = 15 min -> 100 * 15/25 = 60 Wh
        // Förväntad energi vid 15:00 = 1000 + 60 = 1060

        // Act
        var result = WallboxWorker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(2, result.Count);

        // Första posten vid timgränsen
        Assert.Equal(new DateTime(2024, 1, 15, 15, 0, 0), result[0].ReadAt);
        Assert.Equal(1050, result[0].AccEnergy);

        // Andra posten med slutvärdet
        Assert.Equal(currentTime, result[1].ReadAt);
        Assert.Equal(currentEnergy, result[1].AccEnergy);
    }

    [Fact]
    public void GenerateHourBoundaryReadings_CrossesMultipleHours_ReturnsEntryPerHour()
    {
        // Arrange
        var previousTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var currentTime = new DateTime(2024, 1, 15, 17, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 2000;

        // Total tid: 2h 45min = 165 min, energi: 1000 Wh
        // Energi per minut: 1000/165 ≈ 6.06 Wh/min

        // Tid till 15:00 = 30 min -> 1000 * 30/165 ≈ 182 Wh -> AccEnergy = 1182
        // Tid till 16:00 = 90 min -> 1000 * 90/165 ≈ 545 Wh -> AccEnergy = 1545
        // Tid till 17:00 = 150 min -> 1000 * 150/165 ≈ 909 Wh -> AccEnergy = 1909

        // Act
        var result = WallboxWorker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(4, result.Count); // 3 timgränser + slutmätning

        Assert.Equal(new DateTime(2024, 1, 15, 15, 0, 0), result[0].ReadAt);
        Assert.Equal(new DateTime(2024, 1, 15, 16, 0, 0), result[1].ReadAt);
        Assert.Equal(new DateTime(2024, 1, 15, 17, 0, 0), result[2].ReadAt);
        Assert.Equal(currentTime, result[3].ReadAt);
        Assert.Equal(currentEnergy, result[3].AccEnergy);

        // Verifiera att energin ökar monotont
        Assert.True(result[0].AccEnergy > previousEnergy);
        Assert.True(result[1].AccEnergy > result[0].AccEnergy);
        Assert.True(result[2].AccEnergy > result[1].AccEnergy);
        Assert.True(result[3].AccEnergy > result[2].AccEnergy);
    }

    [Fact]
    public void GenerateHourBoundaryReadings_CrossesMidnight_HandlesDateChange()
    {
        // Arrange
        var previousTime = new DateTime(2024, 1, 15, 23, 45, 0);
        var currentTime = new DateTime(2024, 1, 16, 0, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Act
        var result = WallboxWorker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2024, 1, 16, 0, 0, 0), result[0].ReadAt);
        Assert.Equal(currentTime, result[1].ReadAt);
    }

}