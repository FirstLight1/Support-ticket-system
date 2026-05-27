# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ASP.NET Core MVC support-ticket app: .NET 10, EF Core 10, SQLite, Razor views, cookie auth. Single project `SupportTicketSystem/` inside solution `WebApplication2.sln` (one level up from the project folder).

For setup, run, and migration commands see @README.md. The notes below cover what isn't obvious from it.

## Critical: run dotnet from the project subfolder

`dotnet ef`, `dotnet run`, and seeding must run from `SupportTicketSystem/`, NOT the repo root. Seed the DB with `dotnet run -- --seed` from inside that folder. Running from the root fails or targets the wrong path.

## Code style (differs from defaults)

- `Nullable` is **enabled** — honor nullable annotations; `required` properties are enforced.
- `ImplicitUsings` is **disabled** — add explicit `using` statements in every file; there are no global usings.

## Domain conventions — don't "fix" these

- Models use **Czech names**: e.g. `Predmet` (subject), `DatumVytvorenia` (date created). Keep new code consistent with the existing names; don't rename to English.
- Ticket status enum is spelled `Inprogress` (not `InProgress`). Match the existing spelling.
- Timestamps are stored as ISO 8601 **strings** via `DateTime.UtcNow.ToString("o")`, not native datetime columns.
- User/entity IDs are `Guid` (stored as TEXT in SQLite).

## Database

- **SQLite only** (`app.db` in `SupportTicketSystem/`). The csproj also references the SqlServer package and the README shows how to switch, but assume SQLite and `UseSqlite` — don't migrate to SQL Server unless asked.

## Other gotchas

- **Auth:** cookie-based with claims (NameIdentifier, Name, Role); passwords hashed with `PasswordHasher<UserModel>`. Guard admin actions with `[Authorize(Roles = "Admin")]`. (`AccessDeniedPath` in `Program.cs` is noted as not yet implemented.)
- **Image uploads:** stored in `wwwroot/Images/`, validated by extension AND magic bytes (JPEG/PNG/BMP/GIF), 5 MB max, via `Utils/ImageUploadUtil.cs`.

## Workflow

- **No tests** in this repo currently — verify changes by building/running the app, not by running a test suite.
- **Never commit or push unless explicitly asked.** When asked, do NOT add a `Co-Authored-By: Claude` trailer.
