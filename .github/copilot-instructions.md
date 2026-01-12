# GitHub Copilot Instructions

## Tech Stack
- **Framework**: .NET 10 (ASP.NET Core, Blazor Interactive Server)
- **Language**: C# latest
- **Database**: Entity Framework Core with SQLite for production and SQL Server Express LocalDB for testing
- **Auth**: ASP.NET Core Identity
- **Hosting**: ASP.NET Core Web Application
- **APIs**: RESTful API consumption
- **Testing**: xUnit for unit testing
- **Logging**: serilog through ILogger interface
- **Target Platform**: Raspberry Pi running Ubuntu 25.10.
- **Target Environment**: .NET 10 runtime and nginx web server as reverse proxy.

## Wallbox charger interface
- Communicate with Wallbox chargers using http on url http://192.168.1.205:8080/
- 

## Coding Style & Conventions
- Use file-scoped namespaces (`namespace ChargeMaster;`).
- Use asynchronous methods (`async Task`) for I/O bound operations.
- Prefer `var` over explicit types when the type is obvious from the right-hand side.
- Follow standard C# naming conventions (PascalCase for classes/methods, camelCase for local variables).

## Blazor Specifics
- Use `@inject` for dependency injection in Razor components.
- Ensure `InteractiveServer` render mode is considered where appropriate.

## General
- Keep code clean and concise.
- When generating valid HTML/Razor, ensure accessibility best practices.

## Testing
- Write unit tests for critical components using xUnit.
- Do not mock external dependencies in unit tests; 
- use sql server express LocalDB for testing Entity Framework Core operations.

# Functionality

The main functionality is to read price for electricity from a web API and store it in a database. 
The application should also provide a Blazor Interactive Server front-end to display the stored prices.
- Implement a service to fetch electricity prices from a specified web API.
- Store the fetched prices in a SQLite database using Entity Framework Core.
- Create a timer service that fetches and stores the electricity prices once every day at 13:10.
- Call the web API at startup if there are no prices stored in the database for the current day.
- The API endpoint to fetch the electricity prices is: `https://www.elprisetjustnu.se/api/v1/prices/[ÅR]/[MÅNAD]-[DAG]_[PRISKLASS].json`
- The price class to use is `SE3`.

