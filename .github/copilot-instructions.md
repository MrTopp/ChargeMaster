# GitHub Copilot Instructions

## Tech Stack
- **Framework**: .NET 10 (ASP.NET Core, Blazor Interactive Server)
- **Language**: C# latest
- **Database**: Entity Framework Core with PostgreSQL for both development and production
- **Hosting**: ASP.NET Core Web Application
- **APIs**: RESTful API consumption
- **Testing**: xUnit for unit testing
- **Logging**: serilog through ILogger interface
- **Target Platform**: Raspberry Pi running Ubuntu 25.10.
- **Target Environment**: .NET 10 runtime and nginx web server as reverse proxy.

## Production environment
- The application will be hosted on a Raspberry Pi running Ubuntu 25.10.
- nginx is used as a reverse proxy to forward requests to the ASP.NET Core application.
- Logging is done by serilog sending output to stdout and stderr, which will be captured by the hosting environment.
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
- Do not use interfaces for dependency injection unless multiple implementations are expected for testing.
- Keep code clean and concise, removing dead code and cleaning up after refactoring to maintain a tidy codebase.
- Do not create obvious comments. Do comment if the code is not self-explanatory or if it provides important context that is not immediately clear from the code itself.

## Blazor Specifics
- Use `@inject` for dependency injection in Razor components.
- Ensure `InteractiveServer` render mode is considered where appropriate.

## Testing
- Use xUnit test framework.
- Do not mock external dependencies in unit tests unless explicitly required.
- Use a local PostgreSQL instance for testing Entity Framework Core operations.

## Commit Messages
- Format commit messages as a bullet list where each item starts with `-`. Example: `- Action 1\n- Action 2\n- Action 3`

# Functionality
The application, named "ChargeMaster", handles charging of the electric vehicle and manages the heatpump.
The main purpose is to optimize the usage of electricity by activate the heatpump and charge the electric vehicle when the electricity price is low.
It also limits the total usage of electricity for each hour. There is a cost for the maximum usage of electricity for 
the hour in the month where the usage is the highest. The application monitors the usage and stops the heatpump and electric vehicle charging if 
the usage is close to the maximum for the month.

# project structure

## Workers

Workers are background services that run continuously to perform tasks such as monitoring electricity prices, 
controlling the heatpump, and managing electric vehicle charging. They are implemented as hosted services in ASP.NET Core.

### ChargeWorker
The `ChargeWorker` is responsible for managing the charging of the electric vehicle. It monitors electricity usage
and controls the charging process based on the current electricity price and usage limits.

### DaikinWorker
The `DaikinWorker` is responsible for controlling the heatpump. It monitors electricity usage and controls the heatpump

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
a WebSocket API provided by Tibber. Data is stored in an InfluxDB database.

### WallboxWorker
The `WallboxWorker` is responsible for controlling the Wallbox electric vehicle charger. It monitors 
the charging process and controls it based on the current electricity price and usage limits. 
