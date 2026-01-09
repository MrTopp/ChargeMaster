# GitHub Copilot Instructions

## Tech Stack
- **Framework**: .NET 10 (ASP.NET Core, Blazor Interactive Server)
- **Language**: C# latest
- **Database**: Entity Framework Core with SQLite
- **Auth**: ASP.NET Core Identity

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
