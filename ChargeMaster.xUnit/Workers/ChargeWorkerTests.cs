using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ChargeMaster.Models;
using ChargeMaster.Services;
using ChargeMaster.Workers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChargeMaster.xUnit.Workers;

public class ChargeWorkerTests
{
    [Fact]
    public async Task Debug_ChargeLoop_Executes()
    {
        // Questo test är till för att debugga ChargeLoop manuellt
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<ChargeWorker>>();

        // Use real services
        using var wallboxClient = new HttpClient { BaseAddress = new Uri("http://192.168.1.205:8080/") };
        var wallboxService = new WallboxService(wallboxClient);

        // Assuming VWService runs on localhost:5211 based on other tests
        using var vwClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5211/") };
        var vwService = new VWService(vwClient);

        var worker = new ChargeWorker(
            mockServiceProvider.Object, 
            mockLogger.Object, 
            wallboxService, 
            vwService);

        // Act
        // Run loop for a short period to verify logic flow
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await worker.ChargeLoop(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected when token is cancelled
        }
        catch (HttpRequestException)
        {
             // Ignore network errors for this debug test if running offline
        }
    }
}
