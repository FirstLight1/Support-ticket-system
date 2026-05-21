---
name: migrate
description: Create and apply an EF Core migration for the SupportTicketSystem project. Use after changing a model/entity or DbContext. Invoke with the migration name, e.g. /migrate AddTicketPriority.
disable-model-invocation: true
---

Create and apply an Entity Framework Core migration.

`$ARGUMENTS` is the migration name (PascalCase, e.g. `AddTicketPriority`). If empty, ask the user for one before proceeding.

Steps:
1. All commands MUST run from the `SupportTicketSystem/` project folder, not the repo root.
2. Create the migration:
   ```
   dotnet ef migrations add <name>
   ```
3. Review the generated migration under `Migrations/` — confirm the Up/Down operations match the intended model change before applying.
4. Apply it to the SQLite database:
   ```
   dotnet ef database update
   ```
5. Report what changed. If the migration looks wrong, revert with `dotnet ef migrations remove` (only safe before it's applied) rather than hand-editing.

Notes:
- This project targets SQLite only — don't add SQL Server–specific column types.
- Requires the `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef`).
