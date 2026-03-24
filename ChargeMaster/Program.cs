using ChargeMaster.Components;
using ChargeMaster.Data;
using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.VolksWagen;
using ChargeMaster.Services.Daikin;
using ChargeMaster.Services.Wallbox;
using ChargeMaster.Services.Shelly;
using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Services.TibberPulse;
using ChargeMaster.Workers;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using Serilog;

using System.Reflection;
using ChargeMaster.Services.SMHI;

namespace ChargeMaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var assembly = typeof(Program).Assembly;
            var versionInfo = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "Unknown";

            // Ta bort commit-hash om den finns (allt efter "+")
            versionInfo = versionInfo.Split('+')[0];

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File($"logs/log-.txt",
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("==============================================");
                Log.Information(" Starting ChargeMaster v{Version}", versionInfo);
                Log.Information("==============================================");

                var builder = WebApplication.CreateBuilder(args);

                // ----- Data Protection (för att persistera krypteringsnycklar) -----
                // Läs sökväg från environment-variabel, eller använd default
                var keyRingPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");

                if (string.IsNullOrEmpty(keyRingPath))
                {
                    keyRingPath = Path.Combine(builder.Environment.ContentRootPath,
                        "data-protection-keys");
                }
                if (!Directory.Exists(keyRingPath))
                {
                    Directory.CreateDirectory(keyRingPath);
                }

                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName("ChargeMaster");

                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();

                builder.Host.UseSerilog((context, services, configuration) =>
                {
                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext();

                    // Error och Fatal skrivs även till stderr (utöver stdout från appsettings)
                    if (!context.HostingEnvironment.IsDevelopment())
                    {
                        configuration.WriteTo.Logger(lc => lc
                            .Filter.ByIncludingOnly(e => e.Level >= Serilog.Events.LogEventLevel.Error)
                            .WriteTo.Console(
                                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}",
                                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose));
                    }
                });

                var connectionString
                    = builder.Configuration.GetConnectionString("DefaultConnection") ??
                      throw new InvalidOperationException(
                          "Connection string 'DefaultConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(connectionString));

                // ----- Application Services -----
                builder.Services.AddScoped<IElectricityPriceRepository, ElectricityPriceRepository>();
                builder.Services.AddHttpClient<ElectricityPriceService, ElectricityPriceService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                });

                var vwServiceBaseAddress = builder.Configuration["Services:VWService"]
                                           ?? throw new InvalidOperationException(
                                               "VWService base address 'Services:VWService' not configured.");
                builder.Services.AddHttpClient<VWService, VWService>(client =>
                {
                    client.BaseAddress = new Uri(vwServiceBaseAddress);
                    client.Timeout = TimeSpan.FromSeconds(30);
                });

                builder.Services.AddHttpClient<WallboxService, WallboxService>(client =>
                {
                    client.BaseAddress = new Uri("http://192.168.1.205:8080/");
                    client.Timeout = TimeSpan.FromSeconds(20);
                });

                builder.Services.AddHttpClient<DaikinService>(client =>
                {
                    client.BaseAddress = new Uri("http://192.168.1.156/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                });
                builder.Services.AddSingleton<IDaikinService>(sp => sp.GetRequiredService<DaikinService>());

                builder.Services.AddHttpClient<SmhiWeatherService, SmhiWeatherService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                });
                builder.Services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>();

                builder.Services.AddSingleton<DaikinFacade>();

                builder.Services.AddSingleton<ShellyMqttService>();

                // ----- Logging -----
                builder.Services.AddLogging();

                // ----- InfluxDB -----
                builder.Services.Configure<InfluxDBOptions>(
                    builder.Configuration.GetSection("InfluxDB"));
                builder.Services.AddSingleton<InfluxDbService>();

                // ----- Tibber Pulse -----
                builder.Services.Configure<TibberPulseOptions>(
                    builder.Configuration.GetSection("Tibber"));
                builder.Services.AddSingleton<TibberPulseService>();

                // ----- Workers -----
                builder.Services.AddSingleton<PriceFetchingWorker>();
                builder.Services.AddSingleton<WallboxWorker>();
                builder.Services.AddSingleton<ChargeWorker>();
                builder.Services.AddSingleton<DaikinWorker>();
                builder.Services.AddSingleton<ShellyWorker>();
                builder.Services.AddSingleton<SmhiWorker>();
                builder.Services.AddSingleton<TibberWorker>();
                builder.Services.AddSingleton<LinuxWorker>();

                builder.Services.AddHostedService(sp =>
                    sp.GetRequiredService<PriceFetchingWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<WallboxWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<ChargeWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<DaikinWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<ShellyWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<SmhiWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<TibberWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<LinuxWorker>());

                var app = builder.Build();

                if (!app.Environment.IsDevelopment())
                {
                    app.UseForwardedHeaders(new ForwardedHeadersOptions
                    {
                        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                                           | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                    });
                    // Säkerhetsheaders
                    app.Use(async (context, next) =>
                    {
                        var headers = context.Response.Headers;
                        headers["X-Content-Type-Options"] = "nosniff";
                        headers["X-Frame-Options"] = "DENY";
                        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                        await next();
                    });
                    app.UsePathBase("/ChargeMaster");
                }

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    // Development error page
                }
                else
                {
                    app.UseExceptionHandler("/Error");
                }

                app.UseStatusCodePagesWithReExecute("/not-found",
                    createScopeForStatusCodePages: true);

                app.UseAntiforgery();

                app.MapStaticAssets();
                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
