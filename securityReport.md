# Security Report — Support Ticket System

_Reviewed: 2026-05-20 · Re-checked: 2026-05-21 · Scope: auth, controllers, image upload, data layer, Razor views_

Findings are ordered worst-first. File references use `file:line`.

> **Re-check note (2026-05-21):** The controller was refactored — per-id queries now go
> through `data/TicketAccess.cs`, and admin actions live in a dedicated `AdminController`.
> Status of each finding is marked inline below.

| # | Finding | Status |
|---|---------|--------|
| 1 | IDOR / broken access control | ✅ Fixed |
| 2 | Mass assignment (Create + Edit `ImagePath`) | ⚠️ Partial |
| 3 | CSRF | ❌ Still present |
| 4 | Upload size not enforced / validation skipped | ⚠️ Partial |
| 5 | Seeded default admin creds | ❌ Still present |
| 6 | Login brute-force | ❌ Still present |
| 7 | Session lifetime inconsistency | ❌ Still present |
| 8 | Silent exception swallowing | ⚠️ Partial |
| 9 | AccessDeniedPath not implemented | ❌ Still present |

---

## 🔴 Critical — Broken access control (IDOR) — ✅ FIXED

Every per-id action is now owner-scoped through `data/TicketAccess.cs`, which filters on
`t.CreatedByUserId == userId` unless `isAdmin`. Missing-vs-forbidden are deliberately
indistinguishable (both return `null` → `NotFound`), so ids can't be probed.

- `Controllers/TicketsController.cs:86` — `Edit` (GET): `_tickets.GetForUser(id, userId, isAdmin)` ✅
- `Controllers/TicketsController.cs:120` — `Edit` (POST): `_tickets.UpdateEditable(id, userId, isAdmin, …)` ✅
- `Controllers/TicketsController.cs:131-138` — `Details`: now has `[Authorize]` **and** owner check ✅
- `Controllers/TicketsController.cs:151` — `Delete`: `_tickets.Delete(ticketId, userId, isAdmin)` ✅
- Owner rule lives in `data/TicketAccess.cs:87-92, 110-114, 131-135`. Admins allowed through via `isAdmin`.

---

## 🔴 Critical — Mass assignment / over-posting — ⚠️ PARTIALLY FIXED

- `Controllers/TicketsController.cs:53` — `Create(Tickets ticket)` **still binds the whole entity**.
  `Status` and `CreatedByUserId` are now forced server-side in `TicketAccess.Create`
  (`data/TicketAccess.cs:99-100`), so "mark own ticket Completed" via create is closed. **But**
  `ImagePath`, `AssignedToUserId`, `DatumVytvorenia`, and `CompletionNote` are still over-postable
  (none are reset server-side). Still no dedicated `CreateTicketModel`.
- `Controllers/TicketsController.cs:110` / `data/TicketAccess.cs:118-124` — the old `SetValues(ticket)`
  blanket copy is **gone**; `UpdateEditable` copies only the editable fields and never touches
  `Status`/ownership. ✅ **However**, `EditTicketModel.ImagePath` is still bound from the request and
  copied through when non-null (`data/TicketAccess.cs:123-124`), so an attacker can still overwrite
  `ImagePath` to an arbitrary string **without uploading**. Exclude `ImagePath` from binding; set it
  only from a validated upload.

---

## 🟠 High — No CSRF protection — ❌ STILL PRESENT

`Program.cs:36` is still plain `AddControllersWithViews()`. No `[ValidateAntiForgeryToken]` on any
action and no global filter, so cross-site POSTs to create/edit/delete/assign/complete/register all
work.

**Fix** — one line in `Program.cs:36`:

```csharp
builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
```

---

## 🟠 High — Upload size limit not enforced + validation skipped — ⚠️ PARTIALLY FIXED

- `Controllers/TicketsController.cs:168-177` — `TryStoreImage` still adds a ModelState error for
  >5 MB **but keeps going and saves the file anyway** — the size limit still does nothing.
- `Controllers/TicketsController.cs:55` — `Create` now guards `if (!ModelState.IsValid)`, so
  `[Required]` on `Predmet`/`TicketText` is enforced ✅ — **but the check runs before the image is
  processed**, so the oversize error added later is never seen.
- `Controllers/TicketsController.cs:110-127` — `Edit` still has **no** `ModelState.IsValid` check at all.

**Fix:** validate after image processing and actually stop on oversize; add a `ModelState.IsValid`
guard to `Edit`. Consider `[RequestSizeLimit]` / a multipart body limit too (DoS).

---

## 🟠 High — Seeded default admin credentials — ❌ STILL PRESENT

`Utils/PopulateDb.cs:28-29` still seeds `admin@admin.sk` / `admin2@admin.sk` with `Admin123!`.
Seeding is gated on the `--seed` arg (`Program.cs:43`) but **not** on the Development environment,
so it can still run against prod → instant full admin compromise. Gate seeding behind Development
or pull credentials from config/secrets.

---

## 🟡 Medium

- **No brute-force protection** on login (`Controllers/AuthController.cs:112`) — ❌ still none. No
  lockout, no rate limiter registered. Add .NET rate-limiting middleware and/or failed-attempt lockout.
- **Session lifetime inconsistency** (`Controllers/AuthController.cs:87-88`) — ❌ still present. `SignIn`
  forces `IsPersistent = true` + `ExpiresUtc = AddDays(14)`, overriding the 8-hour sliding
  `ExpireTimeSpan` in `Program.cs:27`. Pick one intentionally.
- **Silent exception swallowing** — ⚠️ reduced. The mutation actions no longer `Console.WriteLine(e)`
  and continue (genuine DB faults now bubble up from `TicketAccess`). **But** `TryStoreImage` still
  swallows image errors and proceeds (`Controllers/TicketsController.cs:179-182`), so a failed/invalid
  upload still saves the ticket and redirects as "success"; `Authenticator.FindUser` also still swallows
  (`Controllers/AuthController.cs:45-49`).
- **AccessDeniedPath not implemented** — ❌ still present. `Program.cs:32` points to
  `/Auth/AccessDenied`, but no such action exists in `AuthController` (only `Index`/`Logout`), so
  denials 404.

---

## ✅ Already solid — don't regress these

- EF Core LINQ (no SQL injection)
- Razor auto-encoding, no `Html.Raw` (no stored XSS — `@ImagePath` in `<img src>` is encoded)
- `PasswordHasher` for passwords
- Cookie is `HttpOnly` + `Secure` + `SameSite=Lax`
- HTTPS redirect + HSTS
- Uploads validated by extension **and** magic bytes, saved under GUID filenames (no path traversal)
- `RegisterViewModel` has no `IsAdmin`, set to `false` server-side (no privilege escalation via signup)
- Open-redirect guard: `RedirectBack` only honors local URLs (`Controllers/TicketsController.cs:159-162`)

---

**Highest-value remaining fix:** CSRF (High #3) — still completely untouched and a one-liner. The
IDOR pair-mate (Critical #1) is now resolved.
