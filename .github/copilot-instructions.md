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

## Wallbox Charger Interface
- Communicate with Wallbox chargers using HTTP on URL `http://192.168.1.205:8080/`
- The wallbox interface does not support HTTPS.

## Coding Style & Conventions
- Use file-scoped namespaces (`namespace ChargeMaster;`).
- Use asynchronous methods (`async Task`) for I/O bound operations.
- Prefer `var` over explicit types when the type is obvious from the right-hand side.
- Follow standard C# naming conventions (PascalCase for classes/methods, camelCase for local variables).
- Do not use interfaces for dependency injection unless multiple implementations are expected.
- Keep code clean and concise, removing dead code and cleaning up after refactoring to maintain a tidy codebase.

## Blazor Specifics
- Use `@inject` for dependency injection in Razor components.
- Ensure `InteractiveServer` render mode is considered where appropriate.

## General
- Keep code clean and concise.
- When generating valid HTML/Razor, ensure accessibility best practices.

## Testing
- Write unit tests for critical components using xUnit.
- Do not mock external dependencies in unit tests unless explicitly required.
- Use a local PostgreSQL instance for testing Entity Framework Core operations.

## Commit Messages
- Format commit messages as a bullet list where each item starts with `-`. Example: `- Action 1\n- Action 2\n- Action 3`

# Functionality

The main functionality is to read price for electricity from a web API and store it in a database. 
The application should also provide a Blazor Interactive Server front-end to display the stored prices.
- Implement a service to fetch electricity prices from a specified web API.
- Store the fetched prices in a PostgreSQL database using Entity Framework Core.
- Create a timer service that fetches and stores the electricity prices once every day at 13:10.
- Call the web API at startup if there are no prices stored in the database for the current day.
- The API endpoint to fetch the electricity prices is: `https://www.elprisetjustnu.se/api/v1/prices/[year]/[month]-[day]_[PRISKLASS].json`
- The price class to use is `SE3`.

## Production environment
- The application will be hosted on a Raspberry Pi running Ubuntu 25.10.
- Use nginx as a reverse proxy to forward requests to the ASP.NET Core application.
- Logging is done by serilog sending output to stdout and stderr, which will be captured by the hosting environment.
- Path to application is `/var/www/ChargeMaster`
- User running the application is `chargemasterapp`
- Use systemd to manage the application as a service, ensuring it starts on boot and restarts on failure.
- Application is running interface on http, nginx will handle SSL termination and forwarding to the application.
- systemd service name is `chargemaster-dotnet.service` and should be configured to run the application with the appropriate user and permissions.