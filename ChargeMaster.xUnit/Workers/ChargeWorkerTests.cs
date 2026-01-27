using ChargeMaster.Data;
using ChargeMaster.Services;
using ChargeMaster.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;
using System.Net;

namespace ChargeMaster.xUnit.Workers;

public class ChargeWorkerTests
{
    [Fact]
    public async Task Debug_ChargeLoop_Executes()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(new WallboxService(new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/")
        }));
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Ensure DB is created
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }
        var mockLogger = new Mock<ILogger<ChargeWorker>>();

        // Use real services
        using HttpClient wallboxClient = new HttpClient { BaseAddress = new Uri("http://192.168.1.205:8080/") };
        var wallboxService = new WallboxService(wallboxClient);

        // Assuming VWService runs on localhost:5211 based on other tests
        using var vwClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5211/") };
        var vwService = new VWService(vwClient);

        var worker = new ChargeWorker(
            provider,
            mockLogger.Object,
            wallboxService,
            vwService);

        await worker.ChargeLoop(CancellationToken.None);
    }

    [Fact]
    public async Task LaddBehov_ReturnsZero_WhenVWServiceReturnsNull()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var vwService = new VWService(httpClient);

        var mockLogger = new Mock<ILogger<ChargeWorker>>();
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        using var wallboxClient = new HttpClient();
        var wallboxService = new WallboxService(wallboxClient);

        var worker = new ChargeWorker(provider, mockLogger.Object, wallboxService, vwService);

        // Act
        var result = await worker.LaddBehov();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task LaddBehov_ReturnsZero_WhenVWServiceReturnsValidData()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var jsonResponse = """
                           {
                             "status": {
                               "battery_level": "80",
                               "charging_settings_target_level": 100
                             }
                           }
                           """;

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var vwService = new VWService(httpClient);

        var mockLogger = new Mock<ILogger<ChargeWorker>>();
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        using var wallboxClient = new HttpClient();
        var wallboxService = new WallboxService(wallboxClient);

        var worker = new ChargeWorker(provider, mockLogger.Object, wallboxService, vwService);

        // Act
        var result = await worker.LaddBehov();

        // Assert
        Assert.Equal(0, result);

        // Verify that the service was actually called
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/status")),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}
