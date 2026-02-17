using System.Reflection;
using ChargeMaster.Components;
using ChargeMaster.Components.Account;
using ChargeMaster.Data;
using ChargeMaster.Services;
using ChargeMaster.Workers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ChargeMaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var assembly = typeof(Program).Assembly;
            var versionInfo = assembly
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
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

                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();

                builder.Services.AddCascadingAuthenticationState();
                builder.Services.AddScoped<IdentityRedirectManager>();
                builder.Services
                    .AddScoped<AuthenticationStateProvider,
                        IdentityRevalidatingAuthenticationStateProvider>();

                builder.Services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = IdentityConstants.ApplicationScheme;
                        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                    })
                    .AddIdentityCookies();

                var connectionString
                    = builder.Configuration.GetConnectionString("DefaultConnection") ??
                      throw new InvalidOperationException(
                          "Connection string 'DefaultConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(connectionString));

                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                builder.Services.AddIdentityCore<ApplicationUser>(options =>
                    {
                        options.SignIn.RequireConfirmedAccount = true;
                        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                    })
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddSignInManager()
                    .AddDefaultTokenProviders();

                builder.Services
                    .AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

                // ----- Application Services -----
                builder.Services.AddHttpClient<ElectricityPriceService, ElectricityPriceService>();

                var vwServiceBaseAddress = builder.Configuration["Services:VWService"]
                                           ?? throw new InvalidOperationException(
                                               "VWService base address 'Services:VWService' not configured.");
                builder.Services.AddHttpClient<VWService, VWService>(client =>
                {
                    client.BaseAddress = new Uri(vwServiceBaseAddress);
                });

                builder.Services.AddHttpClient<WallboxService, WallboxService>(client =>
                {
                    client.BaseAddress = new Uri("http://192.168.1.205:8080/");
                });

                // ----- Workers -----
                builder.Services.AddSingleton<PriceFetchingWorker>();
                builder.Services.AddSingleton<WallboxWorker>();
                builder.Services.AddSingleton<ChargeWorker>();

                builder.Services.AddHostedService(sp =>
                    sp.GetRequiredService<PriceFetchingWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<WallboxWorker>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<ChargeWorker>());

                var app = builder.Build();
                app.UsePathBase("/ChargeMaster");

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseMigrationsEndPoint();
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

                // Add additional endpoints required by the Identity /Account Razor components.
                app.MapAdditionalIdentityEndpoints();

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
