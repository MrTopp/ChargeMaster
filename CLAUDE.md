# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ChargeMaster is a .NET 10 Blazor Server application that manages home EV charging and energy usage. It automates EV charging schedules based on Nord Pool electricity prices, monitors a Daikin heat pump, reads Shelly temperature sensors via MQTT, subscribes to Tibber Pulse real-time measurements, and writes time-series data to InfluxDB. The app is deployed on a Linux ARM64 machine (Raspberry Pi).

## Build and Run Commands

```powershell
# Build the main project
dotnet build ChargeMaster/ChargeMaster.csproj

# Run locally (uses launchSettings.json, starts on http://chargemaster.dev.localhost:5191)
dotnet run --project ChargeMaster/ChargeMaster.csproj

# Run all tests (active test project)
dotnet test ChargeMaster.xUnit/ChargeMaster.xUnit.csproj

# Run a single test class
dotnet test ChargeMaster.xUnit/ChargeMaster.xUnit.csproj --filter "FullyQualifiedName~ChargeWorkerTests"

# Run a single test method
dotnet test ChargeMaster.xUnit/ChargeMaster.xUnit.csproj --filter "FullyQualifiedName~ChargeWorkerTests.SomeTestMethod"

# Publish for Raspberry Pi deployment
dotnet publish ChargeMaster/ChargeMaster.csproj -c Release -r linux-arm64 --self-contained false
```

## Test Projects

There are two test projects:
- **`ChargeMaster.xUnit`** — the active project; uses xunit.v3 + Moq. This is the one to use for new tests.
- **`ChargeMaster.UnitTests`** — older project using xunit v2; exists for legacy reasons, do not add new tests here.

Some tests in `ChargeMaster.xUnit` are integration tests that require a live PostgreSQL instance (`Host=127.0.0.1;Port=5432;Database=chargemaster_db;Username=postgres;Password=bulle`). Tests with "Interactive" in the name also require live hardware (Tibber, Daikin, etc.).

## Configuration

Sensitive values are stored in **user secrets** (development) and excluded from source control. `appsettings.json` contains placeholder values for:
- `Tibber:ApiToken` / `Tibber:HomeId` — Tibber Pulse real-time electricity API
- `InfluxDB:Token` — InfluxDB write token
- `Services:VWService` — base URL to the local Python VW REST API module

In production, `DATA_PROTECTION_KEYS_PATH` environment variable sets the Data Protection key ring directory. The app is served under `/ChargeMaster` path base in production.

`appsettings.Development.json` is excluded from publish output (`CopyToPublishDirectory: Never`).

## Architecture

### Blazor UI

Interactive Server-mode Razor components in `ChargeMaster/Components/Pages/`. Pages inject Worker singletons directly to read live state and subscribe to events. Key pages:
- **Index** — main dashboard with live charge status, electricity prices, temperatures
- **MonthlyCosts** — calculates grid fees (effektavgift, högbelastningsavgift, nätavgift)
- **EnergyUsage** — hourly Wallbox energy consumption by month

### Workers (Background Services)

All workers are registered as both `AddSingleton<TWorker>()` and `AddHostedService(sp => sp.GetRequiredService<TWorker>())` so they can be injected into Razor pages and other services.

| Worker | Responsibility |
|--------|---------------|
| `ChargeWorker` | Core charging logic: evaluates electricity prices each quarter, starts/stops EV charging, enforces hourly consumption limits |
| `WallboxWorker` | Polls GARO Wallbox every ~10s; tracks energy consumption per hour; writes to InfluxDB |
| `DaikinWorker` | Hourly heat pump status polling; adjusts target temperature based on electricity price and outdoor temperature |
| `PriceFetchingWorker` | Fetches hourly Nord Pool prices (SE3) from elprisetjustnu.se and stores in PostgreSQL |
| `TibberWorker` | Subscribes to Tibber Pulse real-time measurements and writes to InfluxDB |
| `ShellyWorker` | Manages MQTT connection lifecycle for `ShellyMqttService` |
| `SmhiWorker` | Fetches SMHI weather forecasts (precipitation, temperature) |
| `LinuxWorker` | Reads `/proc/loadavg` on Linux for system load monitoring |

### Charging Decision Logic (`ChargeWorker`)

The core algorithm runs each minute and evaluates each quarter-hour:
1. Builds a "kvartlista" (list of allowed 15-min slots) by selecting the cheapest upcoming slots proportional to charge need (`LaddBehovProcent * 1.9` quarters).
2. Charging is blocked weekdays 07–19 during November–March (peak heating season).
3. The first two 15-min slots of each hour (minutes 0–29) are never used for charging to avoid network fees.
4. If hourly consumption exceeds the calculated limit after 10 minutes into the hour, charging is immediately stopped.
5. If the VW API fails, the Wallbox is set to `NotAvailable` mode as a safety fallback.

### Services

**`DaikinFacade`** — singleton wrapper over `DaikinService` providing events (`StatusChanged`) and cached state. `DaikinService` communicates with Daikin BRP069C4x local HTTP API (firmware < 2.8.0, classic key=value format).

**`ShellyMqttService`** — singleton that connects to MQTT broker at `192.168.1.10:1883`, subscribes to topics for three Shelly devices (`shelly-arbetsrum`, `shelly-hall`, `shelly-sovrum`). Implements exponential backoff reconnection. Notifies subscribers with initial temperature values when they first subscribe.

**`InfluxDbService`** — writes via an internal `Channel<WriteItem>` to serialize all writes (single reader). Batches points in groups of 10. Writes measurements from both Wallbox and Tibber Pulse.

**`ElectricityPriceService`** — fetches hourly prices from `elprisetjustnu.se` API for the SE3 price area. Prices are stored in PostgreSQL with 15-minute granularity (`ElectricityPrice` records). Has an in-memory cache keyed by day.

**`VWService`** — calls a local Python-based REST API module that wraps the unofficial VW Connect API. Configured via `Services:VWService` in appsettings.

### Database (PostgreSQL via EF Core)

`ApplicationDbContext` contains these tables:
- `ElectricityPrices` — hourly/quarterly Nord Pool spot prices
- `WallboxMeterReadings` — energy meter snapshots from the GARO charger
- `ChargeSessions` — EV charge session records (state, battery level, energy)
- `ShellyTemperatures` — temperature history per room device
- `WeatherForecasts` — SMHI weather forecast data
- `DaikinSessions` — heat pump operation log

All `DateTime` columns use `timestamp without time zone` and `DateTimeKind.Unspecified` to match PostgreSQL behavior. Migrations are managed via EF Core tooling.

## Code Conventions

- **Language mixing**: Code (identifiers, types, method names) is in English; comments, log messages, and some domain-specific variable names are in Swedish (e.g., `kvartlista`, `FörbrukningDennaTimme`, `BilenLaddar`).
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`).
- Worker inner loop methods (`ChargeLoop`, `DaikinLoop`) are `internal` to allow direct testing without mocking the full `BackgroundService` lifecycle.
- Services that are singletons but need scoped `DbContext` access use `IServiceScopeFactory` to create a scope per operation.
- `InternalsVisibleTo` is configured for `ChargeMaster.xUnit` in the csproj, so internal members are testable.
