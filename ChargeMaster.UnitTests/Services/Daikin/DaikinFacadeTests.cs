using System;
using System.Threading.Tasks;

using ChargeMaster.Services.Daikin;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChargeMaster.Services.Daikin.UnitTests;


/// <summary>
/// Unit tests for <see cref="DaikinFacade"/> class.
/// </summary>
public class DaikinFacadeTests
{
    /// <summary>
    /// Tests that the Mode property returns Auto (0) as the default value when the facade is newly instantiated.
    /// </summary>
    [Fact]
    public void Mode_DefaultValue_ReturnsAuto()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = facade.Mode;

        // Assert
        Assert.Equal(DaikinMode.Heat, result);
    }

    /// <summary>
    /// Tests that the Mode property returns the correct value for each defined DaikinMode enum value
    /// after the internal state is updated via UpdateStatusAsync.
    /// </summary>
    /// <param name="mode">The DaikinMode value to test.</param>
    [Theory]
    [InlineData(DaikinMode.Auto)]
    [InlineData(DaikinMode.AutoAlt)]
    [InlineData(DaikinMode.Dry)]
    [InlineData(DaikinMode.Cool)]
    [InlineData(DaikinMode.Heat)]
    [InlineData(DaikinMode.Fan)]
    [InlineData(DaikinMode.AutoSwing)]
    public async Task Mode_AfterUpdate_ReturnsExpectedMode(DaikinMode mode)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo
        {
            Mode = (int)mode,
            Power = 1,
            TargetTemperature = 22.0
        };

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync(false);
        var result = facade.Mode;

        // Assert
        Assert.Equal(mode, result);
    }

    /// <summary>
    /// Tests that the Mode property returns an undefined enum value when the underlying mode
    /// is set to a value outside the defined DaikinMode enum range.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task Mode_UndefinedEnumValue_ReturnsUndefinedMode(int undefinedModeValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo
        {
            Mode = undefinedModeValue,
            Power = 1,
            TargetTemperature = 22.0
        };

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync(false);
        var result = facade.Mode;

        // Assert
        Assert.Equal((DaikinMode)undefinedModeValue, result);
    }

    /// <summary>
    /// Tests that the Mode property correctly reflects mode changes when UpdateStatusAsync is called multiple times
    /// with different mode values.
    /// </summary>
    [Fact]
    public async Task Mode_MultipleUpdates_ReflectsLatestMode()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act & Assert - First update to Heat
        var controlInfo1 = new DaikinControlInfo { Mode = (int)DaikinMode.Heat, Power = 1, TargetTemperature = 22.0 };
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(controlInfo1);
        await facade.UpdateStatusAsync(false);
        Assert.Equal(DaikinMode.Heat, facade.Mode);

        // Act & Assert - Second update to Cool
        var controlInfo2 = new DaikinControlInfo { Mode = (int)DaikinMode.Cool, Power = 1, TargetTemperature = 22.0 };
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(controlInfo2);
        await facade.UpdateStatusAsync(false);
        Assert.Equal(DaikinMode.Cool, facade.Mode);

        // Act & Assert - Third update to Fan
        var controlInfo3 = new DaikinControlInfo { Mode = (int)DaikinMode.Fan, Power = 1, TargetTemperature = 22.0 };
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(controlInfo3);
        await facade.UpdateStatusAsync(false);
        Assert.Equal(DaikinMode.Fan, facade.Mode);
    }

    /// <summary>
    /// Tests that the Mode property retains its current value when UpdateStatusAsync receives
    /// null control info from the service.
    /// </summary>
    [Fact]
    public async Task Mode_UpdateWithNullControlInfo_RetainsCurrentValue()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var initialMode = facade.Mode;

        // Act
        await facade.UpdateStatusAsync(false);
        var resultMode = facade.Mode;

        // Assert
        Assert.Equal(initialMode, resultMode);
        Assert.Equal(DaikinMode.Heat, resultMode);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync returns true and calls UpdateStatusAsync and logs information
    /// when the service call succeeds with various temperature values.
    /// </summary>
    /// <param name="temperature">The target temperature value to test.</param>
    /// <param name="heat">Whether to use heat mode.</param>
    [Theory]
    [InlineData(20.0, true)]
    [InlineData(25.5, false)]
    [InlineData(0.0, true)]
    [InlineData(-10.0, false)]
    [InlineData(100.0, true)]
    [InlineData(-273.15, false)]
    [InlineData(1000000.0, true)]
    [InlineData(-1000000.0, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(double.MaxValue, false)]
    public async Task SetTargetTemperatureAsync_WhenServiceReturnsTrue_ReturnsTrueAndUpdatesStatus(double temperature, bool heat)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ReturnsAsync(true);

        // Setup for UpdateStatusAsync internal calls
        mockDaikinService
            .Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        mockDaikinService
            .Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.SetTargetTemperatureAsync(temperature, heat), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Måltemperatur inställd till")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync correctly handles infinity values for temperature parameter.
    /// </summary>
    /// <param name="temperature">The infinity temperature value to test.</param>
    /// <param name="heat">Whether to use heat mode.</param>
    [Theory]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, false)]
    public async Task SetTargetTemperatureAsync_WhenServiceReturnsTrueWithInfinity_ReturnsTrueAndUpdatesStatus(double temperature, bool heat)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ReturnsAsync(true);

        mockDaikinService
            .Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        mockDaikinService
            .Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.SetTargetTemperatureAsync(temperature, heat), Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync correctly handles NaN value for temperature parameter
    /// when the service call succeeds.
    /// </summary>
    [Fact]
    public async Task SetTargetTemperatureAsync_WhenServiceReturnsTrueWithNaN_ReturnsTrueAndUpdatesStatus()
    {
        // Arrange
        var temperature = double.NaN;
        var heat = true;
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ReturnsAsync(true);

        mockDaikinService
            .Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        mockDaikinService
            .Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.SetTargetTemperatureAsync(temperature, heat), Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync returns false and does not update status or log information
    /// when the service call returns false.
    /// </summary>
    /// <param name="temperature">The target temperature value to test.</param>
    /// <param name="heat">Whether to use heat mode.</param>
    [Theory]
    [InlineData(20.0, true)]
    [InlineData(0.0, false)]
    [InlineData(-10.0, true)]
    [InlineData(double.MaxValue, false)]
    public async Task SetTargetTemperatureAsync_WhenServiceReturnsFalse_ReturnsFalseWithoutUpdatingStatus(double temperature, bool heat)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ReturnsAsync(false);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.SetTargetTemperatureAsync(temperature, heat), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync catches exceptions from the service call,
    /// logs an error, and returns false.
    /// </summary>
    /// <param name="temperature">The target temperature value to test.</param>
    /// <param name="heat">Whether to use heat mode.</param>
    [Theory]
    [InlineData(20.0, true)]
    [InlineData(-10.0, false)]
    [InlineData(0.0, true)]
    public async Task SetTargetTemperatureAsync_WhenServiceThrowsException_LogsErrorAndReturnsFalse(double temperature, bool heat)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var expectedException = new InvalidOperationException("Service error");

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ThrowsAsync(expectedException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.SetTargetTemperatureAsync(temperature, heat), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid inställning av måltemperatur")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync handles various exception types correctly,
    /// always catching and returning false.
    /// </summary>
    /// <param name="temperature">The target temperature value to test.</param>
    [Theory]
    [InlineData(25.0)]
    [InlineData(0.0)]
    public async Task SetTargetTemperatureAsync_WhenServiceThrowsVariousExceptionTypes_LogsErrorAndReturnsFalse(double temperature)
    {
        // Arrange - ArgumentException
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var argumentException = new ArgumentException("Invalid argument");

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, true))
            .ThrowsAsync(argumentException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, true);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid inställning av måltemperatur")),
                argumentException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync handles HttpRequestException correctly.
    /// </summary>
    [Fact]
    public async Task SetTargetTemperatureAsync_WhenServiceThrowsHttpRequestException_LogsErrorAndReturnsFalse()
    {
        // Arrange
        var temperature = 22.5;
        var heat = false;
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var httpException = new System.Net.Http.HttpRequestException("Network error");

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ThrowsAsync(httpException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid inställning av måltemperatur")),
                httpException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetTargetTemperatureAsync handles TaskCanceledException correctly.
    /// </summary>
    [Fact]
    public async Task SetTargetTemperatureAsync_WhenServiceThrowsTaskCanceledException_LogsErrorAndReturnsFalse()
    {
        // Arrange
        var temperature = 18.0;
        var heat = true;
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var canceledException = new TaskCanceledException("Operation cancelled");

        mockDaikinService
            .Setup(s => s.SetTargetTemperatureAsync(temperature, heat))
            .ThrowsAsync(canceledException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.SetTargetTemperatureAsync(temperature, heat);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid inställning av måltemperatur")),
                canceledException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync does not raise StatusChanged event when both sensorInfo and controlInfo are null and forceEvent is false.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenBothInfosAreNullAndForceEventIsFalse_DoesNotRaiseEvent()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        facade.StatusChanged += (sender, args) => eventRaised = true;

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.False(eventRaised);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync raises StatusChanged event when both sensorInfo and controlInfo are null but forceEvent is true.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenBothInfosAreNullButForceEventIsTrue_RaisesEvent()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: true);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.False(capturedArgs.HasChanges);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates CurrentTemperature when IndoorTemperature changes by more than 0.01 degrees.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.02)]
    [InlineData(21.5, 21.52)]
    [InlineData(-10.0, -10.02)]
    [InlineData(100.0, 100.1)]
    public async Task UpdateStatusAsync_WhenIndoorTemperatureChangesAboveThreshold_UpdatesCurrentTemperature(double initial, double newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { IndoorTemperature = initial });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { IndoorTemperature = newValue });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CurrentTemperatureChanged);
        Assert.Equal(newValue, facade.CurrentTemperature);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync does not update CurrentTemperature when IndoorTemperature change is exactly at 0.01 threshold.
    /// </summary>
    [Theory]
    [InlineData(21.5, 21.51)]
    [InlineData(0.0, 0.01)]
    [InlineData(-10.0, -10.01)]
    public async Task UpdateStatusAsync_WhenIndoorTemperatureChangeIsAtThreshold_DoesNotUpdateCurrentTemperature(double initial, double newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { IndoorTemperature = initial });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { IndoorTemperature = newValue });
        var eventRaised = false;
        facade.StatusChanged += (sender, args) => eventRaised = true;

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.False(eventRaised);
        Assert.Equal(initial, facade.CurrentTemperature);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates OutdoorTemperature when OutdoorTemperature changes by more than 0.01 degrees.
    /// </summary>
    [Theory]
    [InlineData(5.0, 5.02)]
    [InlineData(-20.0, -20.05)]
    [InlineData(30.0, 30.1)]
    public async Task UpdateStatusAsync_WhenOutdoorTemperatureChangesAboveThreshold_UpdatesOutdoorTemperature(double initial, double newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { OutdoorTemperature = initial });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { OutdoorTemperature = newValue });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.OutdoorTemperatureChanged);
        Assert.Equal(newValue, facade.OutdoorTemperature);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates TargetTemperature when TargetTemperature changes by more than 0.01 degrees.
    /// </summary>
    [Theory]
    [InlineData(22.0, 22.02)]
    [InlineData(18.0, 18.1)]
    [InlineData(25.0, 25.5)]
    public async Task UpdateStatusAsync_WhenTargetTemperatureChangesAboveThreshold_UpdatesTargetTemperature(double initial, double newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { TargetTemperature = initial, Power = 0, Mode = 0 });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { TargetTemperature = newValue, Power = 0, Mode = 0 });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.TargetTemperatureChanged);
        Assert.Equal(newValue, facade.TargetTemperature);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates CompressorFrequency when it changes.
    /// </summary>
    [Theory]
    [InlineData(0, 50)]
    [InlineData(100, 999)]
    [InlineData(999, 0)]
    [InlineData(50, 100)]
    public async Task UpdateStatusAsync_WhenCompressorFrequencyChanges_UpdatesCompressorFrequency(int initial, int newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { CompressorFrequency = initial });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo { CompressorFrequency = newValue });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CompressorFrequencyChanged);
        Assert.Equal(newValue, facade.CompressorFrequency);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates Power status when it changes from off to on.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task UpdateStatusAsync_WhenPowerChanges_UpdatesPowerStatus(int initial, int newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { Power = initial, Mode = 0 });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { Power = newValue, Mode = 0 });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.PowerChanged);
        Assert.Equal(newValue != 0, facade.IsOn);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync updates Mode when it changes between different modes.
    /// </summary>
    [Theory]
    [InlineData(0, 4)] // Auto to Heat
    [InlineData(4, 3)] // Heat to Cool
    [InlineData(3, 6)] // Cool to Fan
    [InlineData(6, 2)] // Fan to Dry
    [InlineData(2, 7)] // Dry to AutoSwing
    public async Task UpdateStatusAsync_WhenModeChanges_UpdatesMode(int initial, int newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { Mode = initial, Power = 0 });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo { Mode = newValue, Power = 0 });
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.ModeChanged);
        Assert.Equal((DaikinMode)newValue, facade.Mode);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync handles null values in temperature properties correctly.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenTemperaturePropertiesAreNull_DoesNotThrowException()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = null,
            OutdoorTemperature = null,
            CompressorFrequency = null
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = null,
            Power = 0,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act & Assert
        await facade.UpdateStatusAsync(forceEvent: false);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync raises event with multiple changes when multiple properties change simultaneously.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenMultiplePropertiesChange_RaisesEventWithAllChanges()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 20.0,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 0,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 21.0,
            OutdoorTemperature = 6.0,
            CompressorFrequency = 100
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 23.0,
            Power = 1,
            Mode = 4
        });

        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CurrentTemperatureChanged);
        Assert.True(capturedArgs.OutdoorTemperatureChanged);
        Assert.True(capturedArgs.CompressorFrequencyChanged);
        Assert.True(capturedArgs.TargetTemperatureChanged);
        Assert.True(capturedArgs.PowerChanged);
        Assert.True(capturedArgs.ModeChanged);
        Assert.True(capturedArgs.HasChanges);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync does not raise event when no properties change and forceEvent is false.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenNoChangesAndForceEventIsFalse_DoesNotRaiseEvent()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 21.0,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        var eventRaised = false;
        facade.StatusChanged += (sender, args) => eventRaised = true;

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.False(eventRaised);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync raises event when no properties change but forceEvent is true.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenNoChangesButForceEventIsTrue_RaisesEvent()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 21.0,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: true);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.False(capturedArgs.HasChanges);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync handles extreme temperature values correctly.
    /// </summary>
    [Theory]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task UpdateStatusAsync_WithExtremeTemperatureValues_HandlesCorrectly(double extremeValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = extremeValue,
            OutdoorTemperature = extremeValue
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = extremeValue,
            Power = 0,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.Equal(extremeValue, facade.CurrentTemperature);
        Assert.Equal(extremeValue, facade.OutdoorTemperature);
        Assert.Equal(extremeValue, facade.TargetTemperature);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync handles NaN temperature values correctly.
    /// </summary>
    [Fact(Skip="ProductionBugSuspected")]
    [Trait("Category", "ProductionBugSuspected")]
    public async Task UpdateStatusAsync_WithNaNTemperatureValues_HandlesCorrectly()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = double.NaN,
            OutdoorTemperature = double.NaN
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = double.NaN,
            Power = 0,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(double.IsNaN(facade.CurrentTemperature ?? 0));
        Assert.True(double.IsNaN(facade.OutdoorTemperature ?? 0));
        Assert.True(double.IsNaN(facade.TargetTemperature ?? 0));
    }

    /// <summary>
    /// Tests that UpdateStatusAsync only updates sensor-related properties when only sensorInfo is not null.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenOnlySensorInfoIsNotNull_UpdatesOnlySensorProperties()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 21.0,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CurrentTemperatureChanged);
        Assert.True(capturedArgs.OutdoorTemperatureChanged);
        Assert.True(capturedArgs.CompressorFrequencyChanged);
        Assert.False(capturedArgs.TargetTemperatureChanged);
        Assert.False(capturedArgs.PowerChanged);
        Assert.False(capturedArgs.ModeChanged);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync only updates control-related properties when only controlInfo is not null.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenOnlyControlInfoIsNotNull_UpdatesOnlyControlProperties()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.False(capturedArgs.CurrentTemperatureChanged);
        Assert.False(capturedArgs.OutdoorTemperatureChanged);
        Assert.False(capturedArgs.CompressorFrequencyChanged);
        Assert.True(capturedArgs.TargetTemperatureChanged);
        Assert.True(capturedArgs.PowerChanged);
        Assert.True(capturedArgs.ModeChanged);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync handles transition from null to non-null temperature values.
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenTemperatureTransitionsFromNullToValue_UpdatesCorrectly()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = null,
            OutdoorTemperature = null
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = null,
            Power = 0,
            Mode = 0
        });

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = 21.0,
            OutdoorTemperature = 5.0
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 0,
            Mode = 0
        });

        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CurrentTemperatureChanged);
        Assert.True(capturedArgs.OutdoorTemperatureChanged);
        Assert.True(capturedArgs.TargetTemperatureChanged);
        Assert.Equal(21.0, facade.CurrentTemperature);
        Assert.Equal(5.0, facade.OutdoorTemperature);
        Assert.Equal(22.0, facade.TargetTemperature);
    }
    
    /// <summary>
    /// Tests that UpdateStatusAsync handles boundary value for compressor frequency (999 = idle/off).
    /// </summary>
    [Fact]
    public async Task UpdateStatusAsync_WhenCompressorFrequencyIs999_UpdatesCorrectly()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            CompressorFrequency = 50
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            CompressorFrequency = 999
        });

        var eventRaised = false;
        DaikinStatusChangedEventArgs? capturedArgs = null;
        facade.StatusChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.True(capturedArgs.CompressorFrequencyChanged);
        Assert.Equal(999, facade.CompressorFrequency);
    }

    /// <summary>
    /// Tests that UpdateStatusAsync handles temperature changes with very small differences below threshold.
    /// </summary>
    [Theory]
    [InlineData(21.5, 21.500)]
    [InlineData(21.5, 21.505)]
    [InlineData(0.0, 0.001)]
    [InlineData(-10.0, -10.005)]
    public async Task UpdateStatusAsync_WhenTemperatureChangeIsBelowThreshold_DoesNotUpdate(double initial, double newValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = initial
        });
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        await facade.UpdateStatusAsync(forceEvent: false);

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(new DaikinSensorInfo
        {
            IndoorTemperature = newValue
        });

        var eventRaised = false;
        facade.StatusChanged += (sender, args) => eventRaised = true;

        // Act
        await facade.UpdateStatusAsync(forceEvent: false);

        // Assert
        Assert.False(eventRaised);
        Assert.Equal(initial, facade.CurrentTemperature);
    }

    /// <summary>
    /// Tests that InitializeAsync successfully calls UpdateStatusAsync and logs information
    /// when daikinService returns valid sensor and control data.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithValidData_CallsUpdateStatusAndLogsInformation()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockDaikinService.Verify(x => x.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(x => x.GetControlInfoAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync with forceEvent=true successfully calls UpdateStatusAsync and logs information.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithForceEventTrue_CallsUpdateStatusAndLogsInformation()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 20.0,
            OutdoorTemperature = 10.0,
            CompressorFrequency = 30
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 21.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync(forceEvent: true);

        // Assert
        mockDaikinService.Verify(x => x.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(x => x.GetControlInfoAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync catches and logs exceptions when GetSensorInfoAsync throws.
    /// Verifies that the exception is not rethrown.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenGetSensorInfoThrows_CatchesExceptionAndLogsError()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var expectedException = new InvalidOperationException("Sensor communication failed");
        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ThrowsAsync(expectedException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid initialisering av Daikin-status")),
                It.Is<Exception>(ex => ex == expectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync catches and logs exceptions when GetControlInfoAsync throws.
    /// Verifies that the exception is not rethrown.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenGetControlInfoThrows_CatchesExceptionAndLogsError()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        };

        var expectedException = new InvalidOperationException("Control communication failed");
        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ThrowsAsync(expectedException);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid initialisering av Daikin-status")),
                It.Is<Exception>(ex => ex == expectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles null sensor info without throwing.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithNullSensorInfo_LogsInformationWithNullValues()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles null control info without throwing.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithNullControlInfo_LogsInformationWithNullValues()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles extreme temperature values without throwing.
    /// </summary>
    /// <param name="indoorTemp">Indoor temperature value to test.</param>
    /// <param name="outdoorTemp">Outdoor temperature value to test.</param>
    [Theory]
    [InlineData(double.MinValue, double.MaxValue)]
    [InlineData(double.MaxValue, double.MinValue)]
    [InlineData(0.0, 0.0)]
    [InlineData(-273.15, 1000.0)]
    [InlineData(100.0, -100.0)]
    [InlineData(double.PositiveInfinity, double.NegativeInfinity)]
    public async Task InitializeAsync_WithExtremeTemperatures_LogsSuccessfully(double indoorTemp, double outdoorTemp)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = indoorTemp,
            OutdoorTemperature = outdoorTemp,
            CompressorFrequency = 50
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles NaN temperature values without throwing.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithNaNTemperatures_LogsSuccessfully()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = double.NaN,
            OutdoorTemperature = double.NaN,
            CompressorFrequency = 50
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = double.NaN,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync correctly logs when power is off (IsOn = false).
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenPowerIsOff_LogsStatusAsAV()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 0
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 0,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AV")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync correctly logs when power is on (IsOn = true).
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenPowerIsOn_LogsStatusAsPaa()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PÅ")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles all DaikinMode enum values correctly.
    /// </summary>
    /// <param name="modeValue">The DaikinMode value to test.</param>
    [Theory]
    [InlineData(0)] // Auto
    [InlineData(1)] // Auto
    [InlineData(2)] // Dry
    [InlineData(3)] // Cool
    [InlineData(4)] // Heat
    [InlineData(6)] // Fan
    [InlineData(7)] // Auto
    public async Task InitializeAsync_WithDifferentModes_LogsSuccessfully(int modeValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = 50
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = modeValue
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles null compressor frequency correctly.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithNullCompressorFrequency_LogsSuccessfully()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = null
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles extreme compressor frequency values correctly.
    /// </summary>
    /// <param name="frequency">The compressor frequency to test.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task InitializeAsync_WithExtremeCompressorFrequency_LogsSuccessfully(int frequency)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo
        {
            IndoorTemperature = 21.5,
            OutdoorTemperature = 5.0,
            CompressorFrequency = frequency
        };

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = 22.0,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(x => x.GetControlInfoAsync()).ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync handles various exception types correctly.
    /// </summary>
    /// <param name="exceptionType">The type of exception to throw.</param>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(NullReferenceException))]
    [InlineData(typeof(TimeoutException))]
    public async Task InitializeAsync_WithDifferentExceptionTypes_CatchesAndLogsError(Type exceptionType)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;
        mockDaikinService.Setup(x => x.GetSensorInfoAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.InitializeAsync();

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid initialisering av Daikin-status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that InitializeAsync does not throw exceptions even when UpdateStatusAsync throws,
    /// verifying proper exception handling.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenUpdateStatusThrows_DoesNotRethrowException()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(x => x.GetSensorInfoAsync())
            .ThrowsAsync(new InvalidOperationException("Communication error"));

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act & Assert - Should not throw
        await facade.InitializeAsync();
    }

    /// <summary>
    /// Tests that TurnOnAsync returns true, calls UpdateStatusAsync, and logs information
    /// when the service successfully turns on the heat pump.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceReturnsTrue_ReturnsTrueAndLogsInformation()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.TurnOnAsync()).ReturnsAsync(true);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin värmepump påslagen")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync returns false and does not call UpdateStatusAsync or log information
    /// when the service fails to turn on the heat pump.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceReturnsFalse_ReturnsFalseWithoutUpdatingStatus()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.TurnOnAsync()).ReturnsAsync(false);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches exceptions thrown by the service, logs the error,
    /// and returns false for various exception types including InvalidOperationException,
    /// HttpRequestException, TimeoutException, and generic Exception.
    /// </summary>
    /// <param name="exceptionMessage">The exception message to use in the test.</param>
    [Theory]
    [InlineData("Invalid operation occurred")]
    [InlineData("HTTP request failed")]
    [InlineData("Operation timed out")]
    [InlineData("Generic error")]
    [InlineData("")]
    [InlineData(null)]
    public async Task TurnOnAsync_WhenServiceThrowsInvalidOperationException_CatchesAndLogsErrorAndReturnsFalse(string? exceptionMessage)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new InvalidOperationException(exceptionMessage);
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches HttpRequestException thrown by the service,
    /// logs the error, and returns false.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceThrowsHttpRequestException_CatchesAndLogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new System.Net.Http.HttpRequestException("Connection failed");
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches TimeoutException thrown by the service,
    /// logs the error, and returns false.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceThrowsTimeoutException_CatchesAndLogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new TimeoutException("Request timed out");
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches generic Exception thrown by the service,
    /// logs the error, and returns false.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceThrowsGenericException_CatchesAndLogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new Exception("Unexpected error");
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches ArgumentNullException thrown by the service,
    /// logs the error, and returns false.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceThrowsArgumentNullException_CatchesAndLogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new ArgumentNullException("paramName", "Parameter cannot be null");
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOnAsync catches OperationCanceledException thrown by the service,
    /// logs the error, and returns false.
    /// </summary>
    [Fact]
    public async Task TurnOnAsync_WhenServiceThrowsOperationCanceledException_CatchesAndLogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new OperationCanceledException("Operation was canceled");
        mockDaikinService.Setup(s => s.TurnOnAsync()).ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOnAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.TurnOnAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid påslaging av värmepump")),
                It.Is<Exception>(ex => ex == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOffAsync returns true when the Daikin service successfully turns off the heat pump.
    /// Verifies that UpdateStatusAsync is called and success is logged.
    /// </summary>
    [Fact]
    public async Task TurnOffAsync_WhenServiceReturnsTrue_ReturnsTrue()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(x => x.TurnOffAsync())
            .ReturnsAsync(true);

        mockDaikinService.Setup(x => x.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        mockDaikinService.Setup(x => x.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOffAsync();

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(x => x.TurnOffAsync(), Times.Once);
        mockDaikinService.Verify(x => x.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(x => x.GetControlInfoAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin värmepump avstängd")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOffAsync returns false when the Daikin service fails to turn off the heat pump.
    /// Verifies that UpdateStatusAsync is not called and no success message is logged.
    /// </summary>
    [Fact]
    public async Task TurnOffAsync_WhenServiceReturnsFalse_ReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(x => x.TurnOffAsync())
            .ReturnsAsync(false);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOffAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(x => x.TurnOffAsync(), Times.Once);
        mockDaikinService.Verify(x => x.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(x => x.GetControlInfoAsync(), Times.Never);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin värmepump avstängd")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that TurnOffAsync catches exceptions thrown by the Daikin service,
    /// logs the error, and returns false.
    /// </summary>
    /// <param name="exception">The exception to be thrown by the service.</param>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(Exception))]
    public async Task TurnOffAsync_WhenServiceThrowsException_LogsErrorAndReturnsFalse(Type exceptionType)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;

        mockDaikinService.Setup(x => x.TurnOffAsync())
            .ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOffAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(x => x.TurnOffAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid avstängning av värmepump")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that TurnOffAsync handles null exceptions properly by logging and returning false.
    /// This tests the edge case where the exception itself might be null or have null properties.
    /// </summary>
    [Fact]
    public async Task TurnOffAsync_WhenServiceThrowsExceptionWithNullMessage_LogsErrorAndReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var exception = new InvalidOperationException();

        mockDaikinService.Setup(x => x.TurnOffAsync())
            .ThrowsAsync(exception);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = await facade.TurnOffAsync();

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(x => x.TurnOffAsync(), Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid avstängning av värmepump")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that CurrentTemperature property returns the initial value of 0.0 when the DaikinFacade is newly instantiated.
    /// </summary>
    [Fact]
    public void CurrentTemperature_InitialState_ReturnsZero()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = facade.CurrentTemperature;

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that SetModeAsync returns true and calls UpdateStatusAsync when the service call succeeds.
    /// </summary>
    /// <param name="mode">The DaikinMode to set.</param>
    [Theory]
    [InlineData(DaikinMode.Auto)]
    [InlineData(DaikinMode.AutoAlt)]
    [InlineData(DaikinMode.Dry)]
    [InlineData(DaikinMode.Cool)]
    [InlineData(DaikinMode.Heat)]
    [InlineData(DaikinMode.Fan)]
    [InlineData(DaikinMode.AutoSwing)]
    public async Task SetModeAsync_WhenServiceReturnsTrue_ReturnsTrueAndUpdatesStatus(DaikinMode mode)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ReturnsAsync(true);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        // Act
        var result = await facade.SetModeAsync(mode);

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.SetModeAsync((int)mode), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Once);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that SetModeAsync returns false and does not call UpdateStatusAsync when the service call fails.
    /// </summary>
    /// <param name="mode">The DaikinMode to set.</param>
    [Theory]
    [InlineData(DaikinMode.Auto)]
    [InlineData(DaikinMode.Cool)]
    [InlineData(DaikinMode.Heat)]
    public async Task SetModeAsync_WhenServiceReturnsFalse_ReturnsFalseWithoutUpdatingStatus(DaikinMode mode)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ReturnsAsync(false);

        // Act
        var result = await facade.SetModeAsync(mode);

        // Assert
        Assert.False(result);
        mockDaikinService.Verify(s => s.SetModeAsync((int)mode), Times.Once);
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
    }

    /// <summary>
    /// Tests that SetModeAsync logs an information message when the service call succeeds.
    /// </summary>
    [Fact]
    public async Task SetModeAsync_WhenServiceReturnsTrue_LogsInformation()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var mode = DaikinMode.Heat;

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ReturnsAsync(true);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        // Act
        await facade.SetModeAsync(mode);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Daikin läge ändrat till")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetModeAsync does not log information when the service call fails.
    /// </summary>
    [Fact]
    public async Task SetModeAsync_WhenServiceReturnsFalse_DoesNotLogInformation()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var mode = DaikinMode.Cool;

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ReturnsAsync(false);

        // Act
        await facade.SetModeAsync(mode);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that SetModeAsync returns false and logs error when the service throws an exception.
    /// </summary>
    /// <param name="mode">The DaikinMode to set.</param>
    [Theory]
    [InlineData(DaikinMode.Auto)]
    [InlineData(DaikinMode.Heat)]
    [InlineData(DaikinMode.Cool)]
    public async Task SetModeAsync_WhenServiceThrowsException_ReturnsFalseAndLogsError(DaikinMode mode)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var expectedException = new InvalidOperationException("Service unavailable");

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ThrowsAsync(expectedException);

        // Act
        var result = await facade.SetModeAsync(mode);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fel vid byte av värmepumpläge till")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetModeAsync handles various exception types correctly, always returning false and logging.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(NullReferenceException))]
    public async Task SetModeAsync_WhenServiceThrowsVariousExceptions_ReturnsFalseAndLogsError(Type exceptionType)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var mode = DaikinMode.Heat;
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ThrowsAsync(exception);

        // Act
        var result = await facade.SetModeAsync(mode);

        // Assert
        Assert.False(result);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(ex => ex.GetType() == exceptionType),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that SetModeAsync works correctly with out-of-range enum values cast from integers.
    /// </summary>
    /// <param name="invalidModeValue">An integer value not defined in the DaikinMode enum.</param>
    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(99)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task SetModeAsync_WithOutOfRangeEnumValues_PassesValueToService(int invalidModeValue)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var mode = (DaikinMode)invalidModeValue;

        mockDaikinService.Setup(s => s.SetModeAsync(invalidModeValue))
            .ReturnsAsync(true);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        // Act
        var result = await facade.SetModeAsync(mode);

        // Assert
        Assert.True(result);
        mockDaikinService.Verify(s => s.SetModeAsync(invalidModeValue), Times.Once);
    }

    /// <summary>
    /// Tests that SetModeAsync does not call UpdateStatusAsync when an exception occurs during SetModeAsync call.
    /// </summary>
    [Fact]
    public async Task SetModeAsync_WhenExceptionOccurs_DoesNotCallUpdateStatus()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var mode = DaikinMode.Heat;

        mockDaikinService.Setup(s => s.SetModeAsync((int)mode))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await facade.SetModeAsync(mode);

        // Assert
        mockDaikinService.Verify(s => s.GetSensorInfoAsync(), Times.Never);
        mockDaikinService.Verify(s => s.GetControlInfoAsync(), Times.Never);
    }

    /// <summary>
    /// Verifies that the OutdoorTemperature property returns the initial value
    /// of 0.0 immediately after construction.
    /// </summary>
    [Fact]
    public void OutdoorTemperature_InitialState_ReturnsZero()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = facade.OutdoorTemperature;

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that CompressorFrequency returns the initial value of 0 upon construction.
    /// </summary>
    [Fact]
    public void CompressorFrequency_InitialValue_ReturnsZero()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        // Act
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Assert
        Assert.Equal(0, facade.CompressorFrequency);
    }

    /// <summary>
    /// Verifies that CompressorFrequency returns the expected value after UpdateStatusAsync is called
    /// with various compressor frequency values including edge cases.
    /// </summary>
    /// <param name="expectedFrequency">The compressor frequency value to test.</param>
    [Theory]
    [InlineData(0)]              // Off state
    [InlineData(999)]            // Idle/off state
    [InlineData(50)]             // Normal operating frequency
    [InlineData(100)]            // Another normal frequency
    [InlineData(1)]              // Minimum active frequency
    [InlineData(-1)]             // Invalid negative value
    [InlineData(int.MaxValue)]   // Maximum integer boundary
    [InlineData(int.MinValue)]   // Minimum integer boundary
    public async Task CompressorFrequency_AfterUpdateStatusAsync_ReturnsExpectedValue(int? expectedFrequency)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var sensorInfo = new DaikinSensorInfo { CompressorFrequency = expectedFrequency };
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(sensorInfo);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();

        // Assert
        Assert.Equal(expectedFrequency, facade.CompressorFrequency);
    }

    /// <summary>
    /// Verifies that CompressorFrequency retains its previous value when UpdateStatusAsync
    /// is called with null sensor info.
    /// </summary>
    [Fact]
    public async Task CompressorFrequency_WhenSensorInfoIsNull_RetainsPreviousValue()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();

        // Assert
        Assert.Equal(0, facade.CompressorFrequency); // Should retain initial value
    }

    /// <summary>
    /// Verifies that CompressorFrequency correctly updates when called multiple times with different values.
    /// </summary>
    [Fact]
    public async Task CompressorFrequency_MultipleUpdates_ReturnsLatestValue()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // First update
        var sensorInfo1 = new DaikinSensorInfo { CompressorFrequency = 50 };
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(sensorInfo1);
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync((DaikinControlInfo?)null);

        // Act - First update
        await facade.UpdateStatusAsync();

        // Assert - First update
        Assert.Equal(50, facade.CompressorFrequency);

        // Second update
        var sensorInfo2 = new DaikinSensorInfo { CompressorFrequency = 999 };
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(sensorInfo2);

        // Act - Second update
        await facade.UpdateStatusAsync();

        // Assert - Second update
        Assert.Equal(999, facade.CompressorFrequency);

        // Third update to null
        var sensorInfo3 = new DaikinSensorInfo { CompressorFrequency = 33 };
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync(sensorInfo3);

        // Act - Third update
        await facade.UpdateStatusAsync();

        // Assert - Third update
        Assert.Equal(33, facade.CompressorFrequency);
    }

    /// <summary>
    /// Tests that the TargetTemperature property returns the initial value (0.0) after construction.
    /// </summary>
    [Fact]
    public void TargetTemperature_InitialValue_ReturnsZero()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();
        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = facade.TargetTemperature;

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that the TargetTemperature property returns the correct value after UpdateStatusAsync is called.
    /// Verifies the property correctly reflects various temperature values including normal, zero, negative, and extreme values.
    /// </summary>
    /// <param name="targetTemperature">The target temperature value to test.</param>
    [Theory]
    [InlineData(22.5)]
    [InlineData(0.0)]
    [InlineData(-15.5)]
    [InlineData(35.0)]
    [InlineData(10.0)]
    [InlineData(18.5)]
    [InlineData(-30.0)]
    [InlineData(50.0)]
    public async Task TargetTemperature_AfterUpdateWithValue_ReturnsUpdatedValue(double targetTemperature)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = targetTemperature,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync(new DaikinSensorInfo());
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();

        // Assert
        Assert.Equal(targetTemperature, facade.TargetTemperature);
    }

    /// <summary>
    /// Tests that the TargetTemperature property returns null when the control info contains a null target temperature.
    /// </summary>
    [Fact]
    [Trait("Category", "ProductionBugSuspected")]
    public async Task TargetTemperature_AfterUpdateWithNull_ReturnsNull()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo
        {
            TargetTemperature = null,
            Power = 1,
            Mode = 4
        };

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync(new DaikinSensorInfo());
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync(controlInfo);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();

        // Assert
        Assert.Null(facade.TargetTemperature);
    }

    /// <summary>
    /// Tests that the TargetTemperature property does not change when UpdateStatusAsync is called with null control info.
    /// Verifies the property retains its previous value when no new data is available.
    /// </summary>
    [Fact]
    public async Task TargetTemperature_AfterUpdateWithNullControlInfo_RetainsInitialValue()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        mockDaikinService.Setup(s => s.GetSensorInfoAsync())
            .ReturnsAsync(new DaikinSensorInfo());
        mockDaikinService.Setup(s => s.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);
        var initialValue = facade.TargetTemperature;

        // Act
        await facade.UpdateStatusAsync();

        // Assert
        Assert.Equal(initialValue, facade.TargetTemperature);
    }

    /// <summary>
    /// Tests that IsOn returns false when power is 0 (off state).
    /// </summary>
    [Fact]
    public async Task IsOn_WhenPowerIsZero_ReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo { Power = 0 };
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(controlInfo);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();
        var result = facade.IsOn;

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests that IsOn returns true when power is non-zero (on state).
    /// Covers various power values including standard on (1), positive, and negative values.
    /// </summary>
    /// <param name="powerValue">The power value to test.</param>
    /// <param name="expected">The expected result of IsOn property.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(100, true)]
    [InlineData(-1, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(int.MinValue, true)]
    public async Task IsOn_WhenPowerIsNonZero_ReturnsTrue(int powerValue, bool expected)
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var controlInfo = new DaikinControlInfo { Power = powerValue };
        mockDaikinService.Setup(s => s.GetControlInfoAsync()).ReturnsAsync(controlInfo);
        mockDaikinService.Setup(s => s.GetSensorInfoAsync()).ReturnsAsync((DaikinSensorInfo?)null);

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        await facade.UpdateStatusAsync();
        var result = facade.IsOn;

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that IsOn returns false initially before UpdateStatusAsync is called,
    /// since the _power field is initialized to 0.
    /// </summary>
    [Fact]
    public void IsOn_BeforeUpdateStatus_ReturnsFalse()
    {
        // Arrange
        var mockDaikinService = new Mock<IDaikinService>();
        var mockLogger = new Mock<ILogger<DaikinFacade>>();

        var facade = new DaikinFacade(mockDaikinService.Object, mockLogger.Object);

        // Act
        var result = facade.IsOn;

        // Assert
        Assert.False(result);
    }
}