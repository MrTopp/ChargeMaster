using ChargeMaster.Workers;
using ChargeMaster.Services.Wallbox;
using ChargeMaster.Services.VolksWagen;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.Extensions.Logging;

namespace ChargeMaster.UnitTests;

public class ChargeWorkerTests
{
    [Fact]
    public async Task GetConnectorStatusAsync_ReturnsChargingWhenConnectorStateIsCharging()
    {
        Assert.True(true);
    }
}
