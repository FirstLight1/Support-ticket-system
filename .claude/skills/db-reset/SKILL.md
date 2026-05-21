---
name: db-reset
description: Reset the local SQLite database for SupportTicketSystem — delete app.db, re-apply all migrations, and optionally re-seed. Use when the dev database is in a bad state or you want fresh data.
disable-model-invocation: true
---

Reset the local development database.

This is destructive — it deletes all local data in `app.db`. Confirm with the user before running if there's any chance the data matters.

Steps (all run from the `SupportTicketSystem/` project folder, not the repo root):
1. Delete the database file:
   - PowerShell: `Remove-Item app.db -Force -ErrorAction SilentlyContinue`
2. Re-create the schema from migrations:
   ```
   dotnet ef database update
   ```
3. Re-seed fake users and tickets (the seed flag is built into the app):
   ```
   dotnet run -- --seed
   ```
4. Report success and remind the user that all previous local data was discarded.

Notes:
- SQLite only; `app.db` lives in the `SupportTicketSystem/` folder.
- If `$ARGUMENTS` contains `no-seed`, skip step 3.
