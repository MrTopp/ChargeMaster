# Migration Guide: Replacing VWService with TibberVehicleService

This guide explains how to migrate from the deprecated `VWService` to the new `TibberVehicleService`.

## Quick Summary

| Aspect | VWService | TibberVehicleService |
|--------|-----------|----------------------|
| **Authentication** | HTTP client with base address | OAuth2 with token management |
| **Configuration** | `Services:VWService` URL | `Tibber:OAuth` credentials |
| **Status Model** | `VWStatus` | `TibberVehicleStatus` |
| **Event Args** | `VWStatusEventArgs` | `TibberVehicleStatusEventArgs` |
| **Token Refresh** | N/A | Automatic (on-demand) |

## Step 1: Update Service Injection

**Before (VWService):**
```csharp
var vwServiceBaseAddress = builder.Configuration["Services:VWService"];
builder.Services.AddHttpClient<VWService>(client =>
{
	client.BaseAddress = new Uri(vwServiceBaseAddress);
	client.Timeout = TimeSpan.FromSeconds(60);
});
```

**After (already done in Program.cs):**
```csharp
builder.Services.Configure<TibberOAuthOptions>(
	builder.Configuration.GetSection("Tibber:OAuth"));
builder.Services.AddSingleton<TibberTokenStorage>();
builder.Services.AddSingleton<TibberOAuthService>();
builder.Services.AddHttpClient<TibberVehicleService>(client =>
{
	client.Timeout = TimeSpan.FromSeconds(60);
});
```

## Step 2: Update Dependency Injection in Components/Workers

**Before:**
```csharp
public class MyWorker(VWService vwService, ILogger<MyWorker> logger)
{
	// Use vwService
}
```

**After:**
```csharp
public class MyWorker(TibberVehicleService vehicleService, ILogger<MyWorker> logger)
{
	// Use vehicleService (same API!)
}
```

## Step 3: Update Event Subscriptions

**Before:**
```csharp
protected override async Task OnInitializedAsync()
{
	vwService.VWStatusRetrieved += OnVWStatusRetrieved;
}

private void OnVWStatusRetrieved(object? sender, VWStatusEventArgs e)
{
	var battery = e.VWStatusLimited?.BatteryLevel;
}
```

**After:**
```csharp
protected override async Task OnInitializedAsync()
{
	vehicleService.VehicleStatusRetrieved += OnVehicleStatusRetrieved;
}

private void OnVehicleStatusRetrieved(object? sender, TibberVehicleStatusEventArgs e)
{
	var battery = e.Status?.BatteryLevel;
}
```

## Step 4: Update Status Access

**Before (VWStatus):**
```csharp
var status = await vwService.GetStatusAsync();
if (status != null)
{
	var battery = status.BatteryLevel;
	var targetLevel = status.ChargingSettingsTargetLevel;
	var power = status.ChargingPower;
	var isCharging = status.ChargingRate > 0;
}
```

**After (TibberVehicleStatus):**
```csharp
var status = await vehicleService.GetStatusAsync();
if (status != null)
{
	var battery = status.BatteryLevel;
	var targetLevel = status.ChargingSettingsTargetLevel;
	var power = status.ChargingPower;
	var isCharging = status.IsCharging; // Direct boolean property!
}
```

**Property Mapping:**
| VWStatus | TibberVehicleStatus |
|----------|----------------------|
| `BatteryLevel` | `BatteryLevel` ✅ Same |
| `BatteryRange` | `Range` |
| `ChargingPower` | `ChargingPower` ✅ Same |
| `ChargingRate` | `ChargingRate` ✅ Same |
| `ChargingSettingsTargetLevel` | `ChargingSettingsTargetLevel` ✅ Same |
| `ChargingSettingsMaximumCurrent` | `ChargingSettingsMaximumCurrent` ✅ Same |
| `ChargingEstimatedDateReached` | `ChargingEstimatedDateReached` ✅ Same |
| N/A | `IsCharging` ✨ New boolean property |
| `VehicleState` | N/A (Removed - use IsCharging instead) |
| `Name`, `Vin`, `Position`, `Odometer` | Same properties ✅ |

## Step 5: Update Command Calls

The API is identical, so no changes needed:

```csharp
// Before (VWService)
await vwService.StartChargingAsync();
await vwService.StopChargingAsync();
await vwService.StartClimatizationAsync();
await vwService.StopClimatizationAsync();

// After (TibberVehicleService)
// Exactly the same!
await vehicleService.StartChargingAsync();
await vehicleService.StopChargingAsync();
await vehicleService.StartClimatizationAsync();
await vehicleService.StopClimatizationAsync();
```

## Step 6: Handle Authentication

Unlike VWService, TibberVehicleService requires OAuth2 authentication:

**Add Login Link in Navigation:**
```blazor
<!-- In MainLayout.razor or Components -->
<a href="/tibber-login" class="nav-link">
	<span class="oi oi-account-login"></span> Connect Tibber
</a>
```

**Check Authentication Status:**
```csharp
var status = await vehicleService.GetStatusAsync();
if (status == null)
{
	// Not authenticated - redirect to login
	Navigation.NavigateTo("/tibber-login");
}
```

## Step 7: Error Handling

**Before (VWService):**
```csharp
try
{
	var status = await vwService.GetStatusAsync();
}
catch (CarConnectionException ex)
{
	logger.LogError("Failed to get VW status");
}
```

**After (TibberVehicleService):**
```csharp
try
{
	var status = await vehicleService.GetStatusAsync();
	if (status == null)
	{
		// Authentication failed - tokens might be expired
		logger.LogWarning("Could not retrieve vehicle status - authentication may be required");
		// Optionally redirect to /tibber-login
	}
}
catch (Exception ex)
{
	logger.LogError(ex, "Error retrieving vehicle status");
}
```

## Files to Update

Search for these and update:
- `VWService` → Replace with `TibberVehicleService`
- `VWStatus` → Replace with `TibberVehicleStatus`
- `VWStatusEventArgs` → Replace with `TibberVehicleStatusEventArgs`
- `VWStatusLimited` → Replace with `TibberVehicleStatusLimited`
- `VWStatusResponse` → Replace with `TibberVehicleStatusResponse`
- `VWVehicleState` → Consider alternatives or remove
- `VWVehiclesResponse` → Replace with `TibberVehiclesResponse`

Likely candidates:
- `ChargeWorker.cs` - Probably uses VWService
- `DaikinWorker.cs` - Possibly uses VWService
- Any Blazor components displaying vehicle data
- UI pages showing battery/charging status

## Configuration

Update `appsettings.json` or `appsettings.Development.json`:

**Remove:**
```json
"Services": {
	"VWService": "http://localhost:5211/"
}
```

**Add/Update:**
```json
"Tibber": {
	"OAuth": {
		"ClientId": "YOUR_CLIENT_ID",
		"ClientSecret": "YOUR_CLIENT_SECRET",
		"RedirectUri": "http://localhost:5000/tibber-callback"
	}
}
```

## Testing Checklist

- [ ] Services compile without errors
- [ ] Dependency injection works (no missing service errors)
- [ ] Can navigate to `/tibber-login`
- [ ] OAuth flow redirects to Tibber correctly
- [ ] Callback succeeds and redirects to home
- [ ] Tokens are stored (check `.tibber-tokens` file)
- [ ] `GetStatusAsync()` returns vehicle data
- [ ] Event handlers fire when status updates
- [ ] Charging commands execute successfully
- [ ] Token refresh works (wait for token to expire or test manually)

## Rollback Plan

If needed, keep the old VWService in the codebase:
1. Both services can coexist
2. Update workers/components gradually
3. Remove VWService only after full migration

## Questions?

Refer to:
- `Services/TibberVehicle/README.md` - Full service documentation
- `TIBBER_OAUTH_IMPLEMENTATION.md` - Implementation details
