# WorkforceManager

Elevator maintenance management with a Windows desktop application, an ASP.NET Core API, and web/mobile client projects.

## Projects

| Directory | Purpose |
| --- | --- |
| [ElevatorMaintenanceSystem](ElevatorMaintenanceSystem) | WPF desktop application, shared models, repositories, and maintenance services. |
| [ElevatorMaintenanceSystem.Api](ElevatorMaintenanceSystem.Api) | API for elevators, workers, and maintenance tickets. |
| [ElevatorMaintenanceSystem.Tests](ElevatorMaintenanceSystem.Tests) | Desktop, service, infrastructure, and view model tests. |
| [ElevatorMaintenanceSystem.Api.Tests](ElevatorMaintenanceSystem.Api.Tests) | API test project; includes API tests from `ElevatorMaintenanceSystem.Tests/Api`. |
| [BlazorWebManager](BlazorWebManager) | Blazor WebAssembly client project. |
| [MAUIWorkerApp](MAUIWorkerApp) | .NET MAUI Blazor client project. |

## Desktop and API development

Use Windows with the .NET 8 SDK. The desktop application uses WPF; the API and both test projects also target `net8.0-windows`. The desktop map uses WebView2, so the WebView2 Runtime must be available. MongoDB must be reachable for database-backed operations.

Configure `MongoDbSettings:ConnectionString` and `MongoDbSettings:DatabaseName` in each application's `appsettings.json` for your local database. The desktop application loads its settings from the output directory; its project copies `appsettings.json` there during the build. Keep credentials out of commits.

From the repository root, build and run the desktop application:

```powershell
dotnet build ElevatorMaintenanceSystem/ElevatorMaintenanceSystem.csproj
dotnet run --project ElevatorMaintenanceSystem/ElevatorMaintenanceSystem.csproj
```

To run the API, configure its MongoDB connection and set a local API key in the current PowerShell session before starting it:

```powershell
$env:ApiSettings__ApiKey = Read-Host 'Enter a local development API key'
dotnet run --project ElevatorMaintenanceSystem.Api/ElevatorMaintenanceSystem.Api.csproj
```

Use the listening address printed at startup. Controller requests require an `X-API-Key` header matching `ApiSettings:ApiKey`. The API also accepts `MongoDbSettings__ConnectionString` and `MongoDbSettings__DatabaseName` environment variables through ASP.NET Core configuration.

## Tests

Run both test projects explicitly from the repository root:

```powershell
dotnet test ElevatorMaintenanceSystem.Tests/ElevatorMaintenanceSystem.Tests.csproj
dotnet test ElevatorMaintenanceSystem.Api.Tests/ElevatorMaintenanceSystem.Api.Tests.csproj
```

The web and mobile clients have separate project files and platform requirements; the commands above cover the desktop application, API, and their tests.
