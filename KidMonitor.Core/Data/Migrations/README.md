# EF Core Migrations

Migrations are generated via the EF Core CLI. Run from the solution root:

```bash
dotnet ef migrations add InitialCreate --project KidMonitor.Core --startup-project KidMonitor.Service
dotnet ef database update --project KidMonitor.Core --startup-project KidMonitor.Service
```

The SQLite database file is placed at the path configured in `appsettings.json` → `Database:Path`.
