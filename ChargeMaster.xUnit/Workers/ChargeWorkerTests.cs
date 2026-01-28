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
    public async Task ChargeLoop_OK()
    {
        var worker = await SetUpChargeWorker();

        await worker.ChargeLoop(CancellationToken.None);
    }

     [Fact]
    public async Task LaddBehov_OK()
    {
        // Arrange
        var worker = await SetUpChargeWorker();

        // Act
        var result = await worker.LaddBehov();

        // Assert
        Assert.Equal(0, result);
    }

    private static async Task<ChargeWorker> SetUpChargeWorker()
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

        HttpClient wallboxClient = new HttpClient { BaseAddress = new Uri("http://192.168.1.205:8080/") };
        var wallboxService = new WallboxService(wallboxClient);

        services.AddSingleton(wallboxService);
        services.AddLogging();

        // Assuming VWService runs on localhost:5211 based on other tests
        var vwClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5211/") };
        var vwService = new VWService(vwClient);

        services.AddSingleton(vwService);

        var provider = services.BuildServiceProvider();

        //await using (var scope = provider.CreateAsyncScope())
        //{
        //    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        //    // Ensure DB is created
        //    await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        //}
        var mockLogger = new Mock<ILogger<ChargeWorker>>();

        var worker = new ChargeWorker(provider, mockLogger.Object);
        return worker;
    }


}
