using ChargeMaster.Services.TibberPulse;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChargeMaster.xUnit.Services.TibberPulse;

/// <summary>
/// Interaktiva tester för TibberPulseService.
/// Designade för manuell testning och utforskning av Tibber API-kommunikation.
/// Kräver giltig Tibber API-token och Home ID i ChargeMaster/appsettings.json.
/// </summary>
public class TibberPulseInteractiveTests(ITestOutputHelper output)
{
    /// <summary>
    /// Ansluter till Tibber API och skriver ut alla mottagna mätningar i upp till 2 minuter.
    /// Kör testet manuellt via Test Explorer för att se resultat i realtid.
    /// </summary>
    [Fact(Skip="Only for interactive testing")]
    public async Task SubscribeAndPrintMeasurements()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(FindAppSettings(), optional: false)
            .Build();

        var tibberOptions = config.GetSection("Tibber").Get<TibberPulseOptions>()
            ?? throw new InvalidOperationException(
                "Tibber-konfiguration saknas i appsettings.json. Lägg till ApiToken och HomeId under 'Tibber'.");

        var loggerFactory = LoggerFactory.Create(b =>
            b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<TibberPulseService>();

        var service = new TibberPulseService(Options.Create(tibberOptions), logger);

        service.MeasurementReceived += (_, e) =>
        {
            var m = e.Measurement;
            output.WriteLine(
                $"[{m.Timestamp.ToLocalTime():HH:mm:ss}] " +
                $"Effekt: {m.Power,8} W | " +
                
                //$"Timme: {m.AccumulatedConsumptionLastHour:F4} kWh | " +
                //$"Dag: {m.AccumulatedConsumption:F4} kWh | " +
                $"P1/P2/P3: {m.VoltagePhase1* m.CurrentPhase1}/{m.VoltagePhase2* m.CurrentPhase2}/{m.VoltagePhase3* m.CurrentPhase3} W | " +
                $"P: {m.VoltagePhase1* m.CurrentPhase1  + m.VoltagePhase2* m.CurrentPhase2 + m.VoltagePhase3* m.CurrentPhase3} W | " +
                $"Ppf: {m.PowerFactor * (m.VoltagePhase1* m.CurrentPhase1  + m.VoltagePhase2* m.CurrentPhase2 + m.VoltagePhase3* m.CurrentPhase3)} W | " +

                //$"U1/U2/U3: {m.VoltagePhase1}/{m.VoltagePhase2}/{m.VoltagePhase3} V | " +
                //$"I1/I2/I3: {m.CurrentPhase1}/{m.CurrentPhase2}/{m.CurrentPhase3} A | " +
                $"PF: {m.PowerFactor} | " +
                $"Signal: {m.SignalStrength} dBm");
        };

        output.WriteLine("Prenumererar på Tibber Pulse-data (max 2 minuter)...");
        output.WriteLine(new string('-', 80));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));

        try
        {
            await service.SubscribeAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            output.WriteLine(new string('-', 80));
            output.WriteLine("Testprenumeration avslutad efter tidsgräns.");
        }
    }

    /// <summary>
    /// Söker upp huvudprojektets appsettings.json relativt testbinärens katalog.
    /// </summary>
    private static string FindAppSettings()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../ChargeMaster/appsettings.Development.json"));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Kunde inte hitta appsettings.json. Sökte på: {path}");
        }

        return path;
    }
}
