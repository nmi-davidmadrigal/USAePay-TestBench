# USAePay Support Testbench
A local, internal Razor Pages app for reproducing and investigating USAePay REST, SOAP, and Pay.js behavior.

## Prerequisites
- .NET SDK 8.x
- SQLite (bundled with EF Core provider)

## Setup
```bash
dotnet tool install --global dotnet-ef
dotnet restore
dotnet run --project src/UsaepaySupportTestbench
```

The app auto-applies migrations on startup. You can also apply them explicitly:
```bash
dotnet ef database update --project src/UsaepaySupportTestbench
```

## Configuration
Edit `appsettings.json` for base URLs. Store credentials in user-secrets:
```bash
dotnet user-secrets set "Usaepay:Sandbox:SourceKey" "sandbox-source-key" --project src/UsaepaySupportTestbench
dotnet user-secrets set "Usaepay:Sandbox:Pin" "sandbox-pin" --project src/UsaepaySupportTestbench
dotnet user-secrets set "Usaepay:Production:SourceKey" "prod-source-key" --project src/UsaepaySupportTestbench
dotnet user-secrets set "Usaepay:Production:Pin" "prod-pin" --project src/UsaepaySupportTestbench
```

> Note: You can also enter Source Key + PIN from the Scenarios page; credentials entered there are stored in-session only.

## Features
- REST and SOAP proxy endpoints: `POST /api/proxy/rest`, `POST /api/proxy/soap`
- Preset builder and scenario runs
- Manual request composer
- Pay.js playground (stub config)
- Redacted logging and export
