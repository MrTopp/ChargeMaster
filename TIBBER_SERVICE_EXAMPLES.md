# Example: Using TibberVehicleService in a Worker

This file shows practical examples of how to use the `TibberVehicleService` in background workers, replacing the old `VWService`.

## Complete Worker Example

```csharp
namespace ChargeMaster.Workers;

using ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Övervakare för fordonsstatus från Tibber.
/// Uppdaterar laddningsstatus och hanterar fel vid auktorisering.
/// </summary>
public class TibberVehicleWorker(
	TibberVehicleService vehicleService,
	ILogger<TibberVehicleWorker> logger) : BackgroundService
{
	private const int UpdateIntervalSeconds = 60;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		logger.LogInformation("TibberVehicleWorker startar");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Hämta aktuell fordonsstatus
				var status = await vehicleService.GetStatusAsync();

				if (status == null)
				{
					logger.LogWarning(
						"Kunde inte hämta fordonsstatus. Användaren behöver möjligtvis omauktorisera");
					// UI kan inspektera detta och visa login-länk
				}
				else
				{
					// Loggning med strukturerad format
					logger.LogInformation(
						"Tibber-fordonsstatus hämtat: Batteri {Battery}%, Laddningseffekt {Power}kW, Målnivå {Target}%",
						status.BatteryLevel,
						status.ChargingPower,
						status.ChargingSettingsTargetLevel);

					// Din affärslogik här
					await ProcessVehicleStatusAsync(status, stoppingToken);
				}

				// Vänta innan nästa uppdatering
				await Task.Delay(TimeSpan.FromSeconds(UpdateIntervalSeconds), stoppingToken);
			}
			catch (OperationCanceledException)
			{
				logger.LogInformation("TibberVehicleWorker avbryts");
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Fel i TibberVehicleWorker");
				// Vänta innan retry
				await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
			}
		}

		logger.LogInformation("TibberVehicleWorker stoppas");
	}

	private async Task ProcessVehicleStatusAsync(TibberVehicleStatus status, CancellationToken ct)
	{
		try
		{
			// Exempel: Stoppa laddning om batterinivå nått målnivå
			if (status.BatteryLevel.HasValue && status.ChargingSettingsTargetLevel.HasValue)
			{
				if (status.BatteryLevel >= status.ChargingSettingsTargetLevel && status.IsCharging == true)
				{
					logger.LogInformation(
						"Batterinivå {Battery}% nått målnivå {Target}%, stoppar laddning",
						status.BatteryLevel,
						status.ChargingSettingsTargetLevel);

					var stopped = await vehicleService.StopChargingAsync();
					if (stopped)
					{
						logger.LogInformation("Laddning stoppad framgångsrikt");
					}
					else
					{
						logger.LogWarning("Kunde inte stoppa laddning via API");
					}
				}
			}

			// Exempel: Logg varning om högt strömförbruk
			if (status.ChargingPower.HasValue && status.ChargingPower > 10)
			{
				logger.LogWarning(
					"Högt laddningseffekt: {Power}kW",
					status.ChargingPower);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Fel vid bearbetning av fordonsstatus");
		}
	}
}
```

## Registering in Program.cs

```csharp
// I Program.cs, efter TibberVehicleService-registreringen:

builder.Services.AddSingleton<TibberVehicleWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TibberVehicleWorker>());
```

## Usage in a Blazor Component

```blazor
@page "/vehicle-status"
@rendermode InteractiveServer

@using ChargeMaster.Services.TibberVehicle

@inject TibberVehicleService VehicleService
@inject NavigationManager Navigation
@inject ILogger<VehicleStatus> Logger

<PageTitle>Fordonsstatus</PageTitle>

<div class="container mt-4">
	<h2>Fordonsstatus</h2>

	@if (IsLoading)
	{
		<div class="spinner-border" role="status">
			<span class="visually-hidden">Laddar...</span>
		</div>
	}
	else if (Status == null)
	{
		<div class="alert alert-warning">
			<p>Kunde inte hämta fordonsstatus.</p>
			<a href="/tibber-login" class="btn btn-primary">Auktorisera med Tibber</a>
		</div>
	}
	else
	{
		<div class="row">
			<div class="col-md-6">
				<div class="card mb-3">
					<div class="card-body">
						<h5 class="card-title">Batteri</h5>
						<div class="progress">
							<div class="progress-bar" role="progressbar" 
								 style="width: @(Status.BatteryLevel)%"
								 aria-valuenow="@(Status.BatteryLevel)"
								 aria-valuemin="0" aria-valuemax="100">
								@(Status.BatteryLevel)%
							</div>
						</div>
						<small class="text-muted">
							Räckvidd: @(Status.Range)km
						</small>
					</div>
				</div>
			</div>

			<div class="col-md-6">
				<div class="card mb-3">
					<div class="card-body">
						<h5 class="card-title">Laddning</h5>
						<p>
							<strong>Status:</strong> 
							@if (Status.IsCharging == true)
							{
								<span class="badge bg-success">Laddar</span>
							}
							else
							{
								<span class="badge bg-secondary">Inaktiv</span>
							}
						</p>
						<p>
							<strong>Effekt:</strong> @(Status.ChargingPower)kW
						</p>
						<p>
							<strong>Målnivå:</strong> @(Status.ChargingSettingsTargetLevel)%
						</p>
					</div>
				</div>
			</div>
		</div>

		<div class="card">
			<div class="card-body">
				<h5 class="card-title">Åtgärder</h5>
				<button class="btn btn-success" @onclick="StartCharging" disabled="@(Status.IsCharging == true)">
					<span class="oi oi-power-standby"></span> Starta laddning
				</button>
				<button class="btn btn-danger" @onclick="StopCharging" disabled="@(Status.IsCharging != true)">
					<span class="oi oi-power-standby"></span> Stoppa laddning
				</button>
				<button class="btn btn-info" @onclick="RefreshStatus">
					<span class="oi oi-reload"></span> Uppdatera
				</button>
			</div>
		</div>
	}
</div>

@code {
	private TibberVehicleStatus? Status;
	private bool IsLoading = true;

	protected override async Task OnInitializedAsync()
	{
		// Prenumerera på status-uppdateringar
		VehicleService.VehicleStatusRetrieved += OnVehicleStatusRetrieved;

		await LoadStatusAsync();
	}

	private async Task LoadStatusAsync()
	{
		IsLoading = true;
		try
		{
			Status = await VehicleService.GetStatusAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Fel vid hämtning av fordonsstatus");
		}
		finally
		{
			IsLoading = false;
		}
	}

	private void OnVehicleStatusRetrieved(object? sender, TibberVehicleStatusEventArgs e)
	{
		Status = e.Status as dynamic;
		InvokeAsync(StateHasChanged);
	}

	private async Task RefreshStatus()
	{
		await LoadStatusAsync();
	}

	private async Task StartCharging()
	{
		var success = await VehicleService.StartChargingAsync();
		if (success)
		{
			Logger.LogInformation("Laddning startad");
			await LoadStatusAsync();
		}
		else
		{
			Logger.LogError("Kunde inte starta laddning");
		}
	}

	private async Task StopCharging()
	{
		var success = await VehicleService.StopChargingAsync();
		if (success)
		{
			Logger.LogInformation("Laddning stoppad");
			await LoadStatusAsync();
		}
		else
		{
			Logger.LogError("Kunde inte stoppa laddning");
		}
	}

	async ValueTask IAsyncDisposable.DisposeAsync()
	{
		VehicleService.VehicleStatusRetrieved -= OnVehicleStatusRetrieved;
		await Task.CompletedTask;
	}
}
```

## Error Handling Strategy

```csharp
// Robust error handling pattern
private async Task SafeVehicleOperationAsync(Func<Task<bool>> operation, string operationName)
{
	try
	{
		var success = await operation();

		if (!success)
		{
			logger.LogWarning("Misslyckad åtgärd: {Operation}", operationName);

			// Försök uppdatera tokens
			var status = await vehicleService.GetStatusAsync();
			if (status == null)
			{
				logger.LogError("Auktorisering krävs efter misslyckad åtgärd");
				// Signalera användaren att omauktorisering behövs
			}
		}
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Undantag vid åtgärd: {Operation}", operationName);
	}
}

// Använda:
await SafeVehicleOperationAsync(
	() => vehicleService.StartChargingAsync(),
	"Starta laddning");
```

## State-based Logic

```csharp
// Exemplar: Intelligent laddningskontroll
private async Task ManageChargingAsync(TibberVehicleStatus status)
{
	// Hämta elpriser (från ElectricityPriceService)
	var currentPrice = await priceService.GetCurrentPriceAsync();

	if (currentPrice?.Price < 0.20) // Billigt el
	{
		if (status.BatteryLevel < status.ChargingSettingsTargetLevel && status.IsCharging != true)
		{
			logger.LogInformation("Billig el ({Price} kr/kWh), startar laddning", currentPrice.Price);
			await vehicleService.StartChargingAsync();
		}
	}
	else if (currentPrice?.Price > 0.50) // Dyr el
	{
		if (status.IsCharging == true && status.BatteryLevel > 50)
		{
			logger.LogInformation("Dyr el ({Price} kr/kWh), stoppar laddning", currentPrice.Price);
			await vehicleService.StopChargingAsync();
		}
	}
}
```

## Testing

See `ChargeMaster.xUnit` project for integration tests with real Tibber API data.
