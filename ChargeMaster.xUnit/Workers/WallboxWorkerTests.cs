using ChargeMaster.Models;
using ChargeMaster.Services;
using ChargeMaster.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChargeMaster.xUnit.Workers;

public class WallboxWorkerTests
{
    [Fact]
    public async Task InitializeWallboxStatus_OK()
    {
        // Arrange
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/")
        };

        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(services, wallbox, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var result = await worker.InitializeWallboxStatus(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
    }

    [Fact]
    public async Task CheckWallboxTime_OK()
    {
        // Arrange
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/")
        };

        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();

        var worker = new WallboxWorker(services, wallbox, logger);

        var now = DateTime.Now;

        var status = new WallboxStatus(
            Serial: 1,
            OcppState: null,
            ConnectedToInternet: true,
            FreeCharging: false,
            OcppConnectionState: null,
            Connector: "CONNECTED",
            Mode: "ALWAYS_ON",
            CurrentLimit: 0,
            FactoryCurrentLimit: 0,
            SwitchCurrentLimit: 0,
            PowerMode: "",
            CurrentChargingCurrent: 0,
            CurrentChargingPower: 0,
            AccSessionEnergy: 0,
            SessionStartTime: null,
            ChargeboxTime: now.ToString("HH:mm"),
            AccSessionMillis: 0,
            LatestReading: 0,
            ChargeStatus: 0,
            UpdateStatus: null,
            CurrentTemperature: 0,
            SessionStartValue: 0,
            NrOfPhases: 0,
            SlaveControlWarning: false,
            SupportConnectionEnabled: false,
            DatetimeConfigured: false,
            PilotLevel: 0,
            MainCharger: null,
            TwinCharger: null);

        // Act
        await worker.CheckWallboxTime(status);
    }

    [Fact]
    public async Task CheckWallboxSchedule_OK()
    {
        // Arrange
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/")
        };

        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(services, wallbox, logger);

        // Act
        await worker.CheckWallboxSchedule();

        // Assert (success-path only): ensure we can still read schema after applying rules.
        var schema = await wallbox.GetSchemaAsync();
        Assert.NotNull(schema);
    }
}
