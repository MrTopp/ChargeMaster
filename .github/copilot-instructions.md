# GitHub Copilot Instructions

## Tech Stack
- **Framework**: .NET 10 (ASP.NET Core, Blazor Interactive Server)
- **Language**: C# latest
- **Database**: Entity Framework Core with PostgreSQL for both development and production. Database schema is managed with SQL scripts in `Data/Scripts/`, not EF migrations.
- **Hosting**: ASP.NET Core Web Application
- **Testing**: xUnit for unit testing
- **Logging**: Serilog through ILogger interface
- **InfluxDB**: Used for storing time-series data from Tibber and Wallbox workers.
- **Tibber SDK**: Used for consuming Websocket stream from Tibber Pulse unit.
- **MQTT**: Used for communication with Shelly thermometers. 

## Production environment
- The application will be hosted on a Raspberry Pi running Ubuntu 25.10.
- nginx is used as a reverse proxy to forward requests to the ASP.NET Core application.
- Logging is done by Serilog sending output to stdout and stderr, which will be captured by the hosting environment.
- Path to application is `/var/www/ChargeMaster`
- User running the application is `chargemasterapp`
- Use systemd to manage the application as a service, ensuring it starts on boot and restarts on failure.
- Application is running interface on http, nginx will handle SSL termination and forwarding to the application.
- systemd service name is `chargemaster-dotnet.service` and should be configured to run the application with the appropriate user and permissions.

## Coding Style & Conventions
- Use file-scoped namespaces (`namespace ChargeMaster;`).
- Use asynchronous methods (`async Task`) for I/O bound operations.
- Prefer `var` over explicit types when the type is obvious from the right-hand side. Use explicit types when it enhances readability.
- Follow standard C# naming conventions (PascalCase for classes/methods, camelCase for local variables).
- Use dependency injection through interfaces rather than concrete classes if it is needed by testing, otherwise inject concrete classes directly.
- Keep code clean and concise, removing dead code and cleaning up after refactoring to maintain a tidy codebase.
- Do not create obvious comments. Do comment if the code is not self-explanatory or if it provides important context that is not immediately clear from the code itself.
- Write comments in Swedish.
- Comments in code should not describe "what" the code is doing, they should describe "why" it is doing it.
- Public methods should have XML documentation comments.

## Configuration
- Use `appsettings.json` for configuration, with environment-specific overrides (`appsettings.Production.json` and `appsettings.Development.json`).
- In production environment, secret parts of the configuration are provided by the environment and override the values in `appsettings.Production.json`. 
- In development environment, secrets are stored in `appsettings.Development.json` which is not committed to version control.

## Blazor Specifics
- Use `@inject` for dependency injection in Razor components.
- Ensure `InteractiveServer` render mode is considered where appropriate.

## Testing
- Use xUnit test framework.
- Two test projects are used:
  - **ChargeMaster.UnitTests**: Contains unit tests with mocked external dependencies. Use `Moq` for mocking services, repositories, and other external dependencies. These tests verify isolated business logic and behavior of individual components without hitting real external services.
  - **ChargeMaster.xUnit**: Contains exploratory tests used to understand and validate behavior of external dependencies. These tests may use real services or integration with external systems. **Never run tests in ChargeMaster.xUnit project automatically. Only run tests in this project when the user explicitly requests it with specific instructions.**
- Do not mock external dependencies in unit tests unless explicitly required for isolation.
- Use a local PostgreSQL instance for testing Entity Framework Core operations (primarily in exploratory tests).
- If a private method needs to be tested, change its access modifier to `internal` instead of using reflection. Avoid reflection in tests.

## Commit Messages
- Format commit messages as a bullet list where each item starts with `-`. Example: `- Action 1\n- Action 2\n- Action 3`

# Functionality
The application, named "ChargeMaster", handles charging of the electric vehicle and manages the heatpump.
The main purpose is to optimize the usage of electricity by activating the heatpump and charging the electric vehicle when the electricity price is low.
It also limits the total usage of electricity for each hour. There is a cost for the maximum usage of electricity for the hour in the month where the usage is the highest. The application monitors the usage and stops the heatpump and electric vehicle charging if the usage is close to the maximum for the month.

# Project Structure

## Workers
Workers are background services that run continuously to perform tasks such as monitoring electricity prices, 
controlling the heatpump, and managing electric vehicle charging. They are implemented as hosted services in ASP.NET Core.

### ChargeWorker
The `ChargeWorker` is responsible for managing the charging of the electric vehicle. It monitors electricity usage
and controls the charging process based on the current electricity price and usage limits.

### DaikinWorker
The `DaikinWorker` is responsible for controlling the heatpump. It monitors electricity usage and controls the heatpump.

### PriceFetchingWorker
The `PriceFetchingWorker` is responsible for fetching the current electricity prices from an external API. 

### ShellyWorker
The `ShellyWorker` is responsible for controlling the Shelly thermometers that measure the temperature in different
rooms. They are used to optimize the heatpump operation based on the temperature in the rooms to get a stable
temperature while minimizing the electricity usage. 

### SmhiWorker
The `SmhiWorker` is responsible for fetching weather forecast data from the SMHI API.

### TibberWorker
The `TibberWorker` is responsible for fetching data from the Tibber Pulse unit. It is done by consuming 
a Websocket stream using Tibber SDK. Data is stored in an InfluxDB database.

### WallboxWorker
The `WallboxWorker` is responsible for controlling the Wallbox electric vehicle charger. It monitors 
the charging process and controls it based on the current electricity price and usage limits. 
Data is stored in the InfluxDB database.

## Services
Services contain the core business logic and external communication. They are registered as singletons
and injected into workers and Blazor components. Each service is located in its own subfolder under `Services/`.

### DaikinService & DaikinFacade
`DaikinService` handles HTTP communication with the Daikin heatpump via its local REST API (BRP069C4x WiFi module).
`DaikinFacade` provides a simplified interface over `DaikinService` with state management and a `StatusChanged` event
for consumers to react to heatpump state changes.

### ElectricityPriceService
Fetches and caches electricity prices from an external API. Uses the SE3 price class for the electricity region.
Provides current and upcoming prices to other services and UI components.

### InfluxDbService
Writes time-series data to InfluxDB. Aggregates data from multiple services (TibberPulse, Wallbox, VolksWagen, 
ElectricityPrice) into InfluxDB measurements for historical analysis and visualization.

### ShellyMqttService
Communicates with Shelly thermometers over MQTT using MQTTnet v5. Maintains a dictionary of temperature readings 
per device and raises events when new measurements arrive. Implements `IAsyncDisposable` for clean MQTT disconnection.

### SmhiWeatherService
Fetches weather forecast data from the SMHI open data API. Provides real and perceived temperature forecasts
for the next 12 hours, used by the heatpump optimization logic.

### TibberPulseService
Subscribes to real-time energy measurements from the Tibber Pulse unit using the Tibber SDK. Implements the 
`IObserver<RealTimeMeasurement>` pattern and raises events with live power consumption data. Uses 
`TaskCompletionSource` for error propagation to the calling worker.

### VWService
Retrieves Volkswagen vehicle status (battery level, charging state, etc.) via a companion HTTP service.
Raises events when new status data is available.

### WallboxService
Communicates with the Garo wallbox charger via its HTTP interface. Provides methods to read charger status
and control charging sessions.
