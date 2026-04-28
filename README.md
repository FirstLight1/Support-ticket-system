# SupportTicketSystem

An ASP.NET Core MVC web application for managing support tickets. Users can submit tickets categorized by type (Bug Report, Feature, Question) and severity (Low, Medium, High, Critical), and admins can assign tickets to users.

## Tech stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **ORM:** Entity Framework Core 10
- **Database:** SQLite (file-based, `app.db`)
- **View engine:** Razor

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [dotnet-ef CLI tool](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)

Install the EF CLI tool if you don't have it:

```bash
dotnet tool install --global dotnet-ef
```

## Dev environment setup

### 1. Clone the repository

```bash
git clone <repo-url>
cd SupportTicketSystem
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Set up the database

The app uses SQLite with a file called `app.db` in the project root. Apply migrations to create it:

```bash
dotnet ef database update
```

This runs all migrations under `Migrations/` and creates `app.db`.

### 4. Run the application

```bash
dotnet run
```

Or to use the `https` launch profile:

```bash
dotnet run --launch-profile https
```

The app will be available at:
- HTTP: `http://localhost:5034`
- HTTPS: `https://localhost:7177`

The `ASPNETCORE_ENVIRONMENT` is set to `Development` by both launch profiles, which enables the developer exception page.

## Configuration

| File | Purpose |
|---|---|
| `appsettings.json` | Base config; sets `DefaultConnection` to `Data Source=app.db` |
| `appsettings.Development.json` | Dev overrides (currently only log levels) |
| `Properties/launchSettings.json` | Launch profiles for `dotnet run` and IDEs |

To switch to a different database (e.g. SQL Server), change the `DefaultConnection` string in `appsettings.json` and swap `UseSqlite` for `UseSqlServer` in `Program.cs`.

## Project structure

```
SupportTicketSystem/
├── Controllers/        # MVC controllers
├── data/               # EF DbContext (AppDbContext)
├── Migrations/         # EF Core migrations
├── Models/             # Domain models and view models
├── Views/              # Razor views
├── wwwroot/            # Static assets (CSS, JS)
├── appsettings.json
└── Program.cs
```

## Common tasks

**Add a new migration** (after changing a model):

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

**Reset the database:**

```bash
rm app.db
dotnet ef database update
```

**Open in JetBrains Rider:**

Open `WebApplication2.sln` (one level up) — it includes this project.