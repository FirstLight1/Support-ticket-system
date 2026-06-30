# Deployment Guide — Docker + Compose + Cloudflare Tunnel on a Raspberry Pi (arm64)

Self-host the Support Ticket System (ASP.NET Core MVC, .NET 10, EF Core 10, SQLite) on a
**Raspberry Pi (arm64)** behind a **Cloudflare Tunnel**, reachable over HTTPS on a custom domain
**without opening any router/firewall ports**.

> This is a guide. None of the changes below are applied yet — follow the steps to deploy.

## Two facts that drive everything

- The app is **stateful**: the SQLite file `app.db` and user-uploaded images under
  `wwwroot/Images` (written by `Utils/ImageUploadUtil.cs` to `WebRootPath/Images`) live on the
  local filesystem. Both must sit on **persistent Docker volumes** or they are lost on every redeploy.
- The app **requires HTTPS to log in**: `Program.cs` sets `CookieSecurePolicy.Always` plus
  `UseHttpsRedirection`/HSTS. Behind a tunnel the app sees plain HTTP internally, so it needs
  **forwarded-headers** handling or auth cookies silently break and HTTPS redirects loop.

The tunnel terminates TLS at Cloudflare's edge and connects **outbound** to the Pi, so nothing
is exposed on the home network.

## Architecture

```
Browser ──HTTPS──> Cloudflare edge ──encrypted tunnel──> cloudflared (container)
                                                              │  http://app:8080 (docker network)
                                                              ▼
                                                         app (container)
                                                          ├─ /data               → volume: ticket-db  (app.db + -wal/-shm)
                                                          └─ /app/wwwroot/Images  → volume: ticket-images
```
- No host ports published. cloudflared reaches the app by compose **service name** `app` on port `8080`.
- App listens HTTP only inside the container (`ASPNETCORE_HTTP_PORTS=8080`); TLS is Cloudflare's job.

---

## Step 1 — Code changes (`SupportTicketSystem/Program.cs`)

Two small, idempotent changes make the app proxy-ready and self-migrating.

### 1a. Forwarded headers

Add the using:
```csharp
using Microsoft.AspNetCore.HttpOverrides;
```

Add this **before** `app.UseHttpsRedirection()` (place it first in the pipeline, right after the
`--seed` block):
```csharp
var fwd = new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwd.KnownNetworks.Clear();   // cloudflared is on the docker network, not loopback
fwd.KnownProxies.Clear();    // safe here: only cloudflared can reach the app (no host port)
app.UseForwardedHeaders(fwd);
```
This makes `Request.IsHttps` true from the `X-Forwarded-Proto: https` header, so the secure cookie
is issued and `UseHttpsRedirection` stops looping.

### 1b. Auto-migrate on startup

After `var app = builder.Build();` and **outside** the existing `--seed` block, apply migrations on
normal boot:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```
This applies migrations on first boot against the empty volume DB. Leave the `--seed` path
(which calls `Migrate()` then inserts demo data) as-is — do **not** run `--seed` in production.

### 1c. No change needed

The DB path is overridden at runtime via the `ConnectionStrings__DefaultConnection` env var, and
images are persisted by mounting a volume over `/app/wwwroot/Images`.

> **Verify / possible extra fix:** `Program.cs` uses `app.MapStaticAssets()` but has **no**
> `app.UseStaticFiles()`. `MapStaticAssets` only serves build-time assets, so **runtime-uploaded
> images may 404**. After deploy, upload an image and confirm it renders at `/Images/...`. If it
> doesn't, add `app.UseStaticFiles();` and rebuild.

---

## Step 2 — Files to create

### `SupportTicketSystem/Dockerfile` (multi-stage)
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
# Pre-create writable dirs and give them to the non-root app user so the named
# volumes inherit writable ownership on first creation (the aspnet image runs as
# UID 1654; root-owned volumes would block SQLite WAL writes and image uploads).
USER root
RUN mkdir -p /data /app/wwwroot/Images \
    && chown -R $APP_UID:$APP_UID /data /app/wwwroot/Images
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SupportTicketSystem.dll"]
```

### `SupportTicketSystem/.dockerignore`
```
bin/
obj/
*.db
*.db-shm
*.db-wal
wwwroot/Images/
.vs/
**/.git
```

### `docker-compose.yml` (repo root)
```yaml
services:
  app:
    build:
      context: ./SupportTicketSystem
      dockerfile: Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_HTTP_PORTS=8080
      - ConnectionStrings__DefaultConnection=Data Source=/data/app.db
    volumes:
      - ticket-db:/data
      - ticket-images:/app/wwwroot/Images
    restart: unless-stopped
    # no `ports:` — only cloudflared reaches it.
    # (Add ports: ["8080:8080"] temporarily to test before the tunnel works.)

  cloudflared:
    image: cloudflare/cloudflared:latest   # multi-arch, includes arm64
    command: tunnel --config /etc/cloudflared/config.yml run
    volumes:
      - ./cloudflared:/etc/cloudflared:ro
    restart: unless-stopped
    depends_on:
      - app

volumes:
  ticket-db:
  ticket-images:
```

### `cloudflared/config.yml` (repo root, committed)
```yaml
tunnel: <TUNNEL_UUID>
credentials-file: /etc/cloudflared/<TUNNEL_UUID>.json
ingress:
  - hostname: tickets.yourdomain.com
    service: http://app:8080
  - service: http_status:404
```

### `.gitignore` additions (tunnel secrets — config.yml is committed, secrets are not)
```
cloudflared/*.json
cloudflared/cert.pem
```

---

## Step 3 — Don't forget

1. **Forwarded headers** (Step 1a) — without it, login cookies are dropped and you get redirect loops behind the tunnel.
2. **Migrations** (Step 1b) — auto-migrate on startup. Do **not** run `--seed` in prod (it inserts demo users/tickets).
3. **HTTPS** — handled by Cloudflare's edge (Universal SSL); the app stays HTTP internally. Don't add a cert to the container.
4. **Volume write permissions** — the Dockerfile chown step; the non-root app user must own `/data` and `/app/wwwroot/Images`.
5. **SD-card wear / storage** — put the Docker volumes (or the whole data-root) on a **USB SSD**, not the microSD. SQLite does many small writes. Keep volumes on a **local** filesystem — never NFS/SMB (corrupts SQLite locking).
6. **Single instance only** — SQLite is single-writer. Do not scale the `app` service to multiple replicas.
7. **Backups** — cron job for a consistent SQLite snapshot + the images volume off-box:
   ```bash
   docker compose exec -T app sh -c "sqlite3 /data/app.db '.backup /data/backup.db'"
   # then copy ticket-db + ticket-images volume contents to another disk / cloud bucket
   ```
8. **Production environment** — `ASPNETCORE_ENVIRONMENT=Production` (set in compose) so the dev exception page is off.
9. **Cloudflare free-plan upload cap** — request body limited to **100 MB** at the edge (image uploads are tiny, so fine; just know the ceiling).

---

## Step 4 — Cloudflare Tunnel: what you need to know

The domain isn't on Cloudflare yet, so onboard it first.

**One-time domain onboarding (before creating the tunnel):**
1. Create a free Cloudflare account.
2. **Add a Site** for `yourdomain.com` (Free plan is fine).
3. Cloudflare shows **two nameservers** — set them at your **domain registrar**, replacing the existing ones.
4. Wait for the zone to go **Active** (minutes to a few hours). The tunnel can't create DNS until this is done.

**Install tooling on the Pi:**
5. Install **Docker + the Compose plugin** (arm64).
6. Install **`cloudflared`** (arm64) — needed once to authenticate and create the tunnel.

**Create the tunnel (config-file mode):**
7. `cloudflared tunnel login` — opens a browser, pick your zone; downloads `cert.pem` to `~/.cloudflared/`.
8. `cloudflared tunnel create supportticket` — prints a **tunnel UUID** and writes a **credentials JSON** (`<UUID>.json`) to `~/.cloudflared/`.
9. `cloudflared tunnel route dns supportticket tickets.yourdomain.com` — auto-creates the CNAME (`<UUID>.cfargotunnel.com`). Pick your subdomain here.
10. Copy `<UUID>.json` into the repo's `cloudflared/` folder and fill the UUID + hostname into `cloudflared/config.yml`.

**Key facts:**
- The tunnel is **outbound-only** — no router port-forwarding, works behind CGNAT. Nothing is exposed on your home IP.
- The origin service in `config.yml` is the **compose service name** `http://app:8080`, reachable only on the internal docker network.
- `config.yml` (ingress rules) is safe to commit; `*.json` credentials and `cert.pem` are **secrets — keep them out of git**.
- TLS/cert is fully managed by Cloudflare; you never handle certificates.

---

## Step 5 — Deploy (on the Pi)

1. Apply the Step 1 code changes; add the Step 2 files.
2. Complete Step 4 onboarding (domain Active, tunnel created, `config.yml` + credentials JSON in place).
3. Build for arm64 (native build on the Pi is simplest):
   ```bash
   docker compose build      # builds the app image for the Pi's arm64
   docker compose up -d      # starts app + cloudflared
   ```
   Cross-building from an x86 machine instead? use `docker buildx build --platform linux/arm64 ...`.
4. Watch logs: `docker compose logs -f` — confirm migrations applied and cloudflared shows
   "Registered tunnel connection".

---

## Step 6 — Verification

1. **Tunnel up:** `docker compose logs cloudflared` shows registered connections; the Cloudflare Zero Trust dashboard lists the tunnel as **Healthy**.
2. **HTTPS reachable:** browse to `https://tickets.yourdomain.com` — page loads over a valid cert.
3. **Login works (forwarded-headers test):** sign in; confirm you stay logged in (no redirect loop). Proves Step 1a is wired correctly.
4. **DB persistence:** create a ticket, then `docker compose restart app` — the ticket survives (it's on the `ticket-db` volume).
5. **Image upload + render:** attach an image to a ticket and confirm it displays at `/Images/...`. If it 404s, apply the `UseStaticFiles()` fix from Step 1 and rebuild.
6. **No open ports:** from outside, confirm your home IP exposes nothing (the tunnel is the only path).

---

## Out of scope (later, only if you outgrow one node)
- Migrating SQLite → Postgres/SQL Server and images → object storage.
- Cloudflare Access in front of the app (the app already has its own cookie auth).
- CI/CD auto-deploy on push.
