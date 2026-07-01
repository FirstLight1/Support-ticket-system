# Deployment Guide — Docker app + systemd cloudflared on a Raspberry Pi (arm64)

Self-host the Support Ticket System (ASP.NET Core MVC, .NET 10, EF Core 10, SQLite) on a
**Raspberry Pi (arm64)** behind a **Cloudflare Tunnel**, reachable over HTTPS on a custom domain
**without opening any router/firewall ports**.

`cloudflared` runs as a **host systemd service**, not a container. This decouples the tunnel from
the app container so it survives `docker compose up --build` cycles, and uses cloudflared's
first-class `cloudflared service install` systemd integration.

## State of the repo

Code and Docker changes are **already applied**:

- `SupportTicketSystem/Program.cs`
  - `UseForwardedHeaders` (X-Forwarded-For + X-Forwarded-Proto, `KnownIPNetworks.Clear()`) so
    `Request.IsHttps` is true behind the tunnel → secure cookies issued, no HTTPS redirect loops.
  - `UseStaticFiles()` serves runtime-uploaded images from `wwwroot/Images/`.
  - `--migrate` flag applies migrations idempotently; the Dockerfile runs it on every start.
  - Data Protection key ring persisted to disk (`/app/keys`) so auth cookies/antiforgery survive
    redeployments.
  - Security response headers, rate limiting, health check at `/healthz`.
- `SupportTicketSystem/Dockerfile` — multi-stage build, runs as the non-root `app` user
  (UID 1654), pre-creates and chowns `/data`, `/app/keys`, `/app/logs`, `/app/wwwroot/Images`.
- `SupportTicketSystem/.dockerignore` — excludes bin/obj/db/keys/logs/dev settings.
- `docker-compose.yml` — `app` service only, binds **`127.0.0.1:400:400`** (loopback-only:
  cloudflared on the host reaches it via `localhost`, nothing on the LAN can), volumes for
  db/keys/logs/images, `BootstrapAdmin__*` env for first-admin bootstrapping.

**What's left** is the Cloudflare onboarding, tunnel creation, systemd service install, and the
first deploy — all on the Pi.

## Do I need a production web server (IIS/Nginx)?

**No.** ASP.NET Core ships **Kestrel**, a production-grade web server. `dotnet
SupportTicketSystem.dll` runs Kestrel directly on port 400 — no IIS, Nginx, or Apache needed.
Here cloudflared acts as your reverse proxy: it terminates TLS at Cloudflare's edge and forwards
plain HTTP to Kestrel on `localhost:400`. Kestrel handling HTTP behind a trusted proxy is a
supported, documented production topology.

## Two facts that drive everything

- The app is **stateful**: the SQLite file `app.db`, the Data Protection key ring (`/app/keys`),
  Serilog logs (`/app/logs`), and user-uploaded images under `wwwroot/Images` all live on the
  local filesystem. Each must sit on a **persistent Docker volume** or it's lost on every
  redeploy.
- The app **requires HTTPS to log in**: `Program.cs` sets `CookieSecurePolicy.Always` plus
  `UseHttpsRedirection`/HSTS. Behind a tunnel the app sees plain HTTP internally, so the
  **forwarded-headers** handling (already in `Program.cs`) is required or auth cookies silently
  break and HTTPS redirects loop.

The tunnel terminates TLS at Cloudflare's edge and connects **outbound** to the Pi, so nothing
is exposed on the home network.

## Architecture

```
Browser ──HTTPS──> Cloudflare edge ──encrypted tunnel──> cloudflared (systemd service on Pi)
                                                               │  http://localhost:400
                                                               ▼
                                                          app (docker container, 127.0.0.1:400)
                                                           ├─ /data               → volume: db_data      (app.db + -wal/-shm)
                                                           ├─ /app/keys           → volume: keys_data    (Data Protection key ring)
                                                           ├─ /app/logs           → volume: logs_data    (Serilog file sink)
                                                           └─ /app/wwwroot/Images → volume: images_data  (uploaded images)
```
- Only `127.0.0.1:400` is published on the host. cloudflared (on the host) reaches the app at
  `http://localhost:400`. No other host or LAN path exists.
- App listens HTTP only inside the container (`ASPNETCORE_HTTP_PORTS=400`); TLS is Cloudflare's job.

---

## Step 1 — Cloudflare onboarding (one-time, before the tunnel)

The domain isn't on Cloudflare yet, so onboard it first.

1. Create a free Cloudflare account.
2. **Add a Site** for `yourdomain.com` (Free plan is fine).
3. Cloudflare shows **two nameservers** — set them at your **domain registrar**, replacing the
   existing ones.
4. Wait for the zone to go **Active** (minutes to a few hours). The tunnel can't create DNS until
   this is done.

## Step 2 — Install tooling on the Pi

5. Install **Docker + the Compose plugin** (arm64).
6. Install **`cloudflared`** (arm64 binary) on the host — needed to authenticate, create the
   tunnel, and run as a systemd service.

## Step 3 — Create the tunnel (config-file mode) and install as a service

7. `cloudflared tunnel login` — opens a browser, pick your zone; downloads `cert.pem` to
   `~/.cloudflared/`.
8. `cloudflared tunnel create supportticket` — prints a **tunnel UUID** and writes a
   **credentials JSON** (`<UUID>.json`) to `~/.cloudflared/`.
9. `cloudflared tunnel route dns supportticket tickets.yourdomain.com` — auto-creates the CNAME
   (`<UUID>.cfargotunnel.com`). Pick your subdomain here.
10. Put the config and credentials on the host where the systemd service reads them:
    ```bash
    sudo mkdir -p /etc/cloudflared
    sudo cp ~/.cloudflared/<UUID>.json /etc/cloudflared/
    sudo tee /etc/cloudflared/config.yml >/dev/null <<'EOF'
    tunnel: <TUNNEL_UUID>
    credentials-file: /etc/cloudflared/<TUNNEL_UUID>.json
    ingress:
      - hostname: tickets.yourdomain.com
        service: http://localhost:400
      - service: http_status:404
    EOF
    ```
    Note the origin `service: http://localhost:400` — the host loopback port the app publishes.
11. Install and start the systemd service (reads `/etc/cloudflared/config.yml` by default):
    ```bash
    sudo cloudflared service install
    sudo systemctl enable --now cloudflared
    ```

**Key facts:**
- The tunnel is **outbound-only** — no router port-forwarding, works behind CGNAT. Nothing is
  exposed on your home IP.
- The origin service is `http://localhost:400` (host loopback), **not** a docker service name.
- `/etc/cloudflared/config.yml` and `<UUID>.json` live **on the Pi's filesystem, never in git**.
- TLS/cert is fully managed by Cloudflare; you never handle certificates.
- To change ingress rules later, edit `/etc/cloudflared/config.yml` and
  `sudo systemctl restart cloudflared`.

---

## Step 4 — Deploy the app (on the Pi)

1. Clone the repo onto the Pi and `cd` to the repo root.
2. Create a `.env` next to `docker-compose.yml` with the first admin (used once on first boot by
   `AdminBootstrapper` in `Program.cs`; leave empty to skip):
   ```env
   BOOTSTRAP_ADMIN_EMAIL=admin@yourdomain.com
   BOOTSTRAP_ADMIN_PASSWORD=<a strong password>
   ```
   Do **not** commit this file (it's not in `.gitignore` yet — add it if you keep secrets here).
3. Build and start (native arm64 build on the Pi is simplest):
   ```bash
   docker compose build
   docker compose up -d
   ```
   Cross-building from an x86 machine instead? use `docker buildx build --platform linux/arm64 ...`.
4. Watch logs:
   ```bash
   docker compose logs -f app        # confirm "Database migrated." and app listening on 400
   journalctl -u cloudflared -f      # confirm "Registered tunnel connection"
   ```

---

## Step 5 — Don't forget

1. **Forwarded headers** — already in `Program.cs`. Without it, login cookies are dropped and you
   get redirect loops behind the tunnel.
2. **Migrations** — auto-applied on every container start via the Dockerfile ENTRYPOINT
   (`dotnet ... --migrate && dotnet ...`). Idempotent; no-op when already up to date. Do **not**
   run `--seed` in prod (it inserts demo users/tickets and is refused in Production env anyway).
3. **HTTPS** — handled by Cloudflare's edge (Universal SSL); the app stays HTTP internally. Don't
   add a cert to the container.
4. **Volume write permissions** — the Dockerfile chowns `/data`, `/app/keys`, `/app/logs`, and
   `/app/wwwroot/Images` to the non-root `app` user (UID 1654) so named volumes inherit writable
   ownership on first creation. If you add a new writable volume, chown it too.
5. **SD-card wear / storage** — put the Docker volumes (or the whole data-root) on a **USB SSD**,
   not the microSD. SQLite does many small writes. Keep volumes on a **local** filesystem — never
   NFS/SMB (corrupts SQLite locking).
6. **Single instance only** — SQLite is single-writer. Do not scale the `app` service to multiple
   replicas.
7. **Backups** — consistent SQLite snapshot + the images/keys volumes off-box:
   ```bash
   docker compose exec -T app sh -c "sqlite3 /data/app.db '.backup /data/backup.db'"
   # then copy db_data, images_data, and keys_data volume contents to another disk / cloud bucket
   ```
   (`sqlite3` may not be in the runtime image — if not, stop the app, copy `/data/app.db`+`-wal`+
   `-shm` directly, then restart.)
8. **Production environment** — `ASPNETCORE_ENVIRONMENT=Production` (set in the Dockerfile) so the
   dev exception page is off.
9. **Cloudflare free-plan upload cap** — request body limited to **100 MB** at the edge (image
   uploads are tiny — the app caps requests at 6 MB — so fine; just know the ceiling).
10. **Loopback-only port** — `127.0.0.1:400:400` in compose keeps the app off the LAN. If you
    drop the `127.0.0.1:` prefix it binds `0.0.0.0` and any LAN device can reach the app directly,
    bypassing the tunnel.

---

## Step 6 — Verification

1. **Tunnel up:** `journalctl -u cloudflared` shows registered connections; the Cloudflare Zero
   Trust dashboard lists the tunnel as **Healthy**.
2. **HTTPS reachable:** browse to `https://tickets.yourdomain.com` — page loads over a valid cert.
3. **Login works (forwarded-headers test):** sign in; confirm you stay logged in (no redirect
   loop). Proves the forwarded-headers wiring is correct.
4. **Health check:** `curl http://localhost:400/healthz` on the Pi returns Healthy; over the
   tunnel `https://tickets.yourdomain.com/healthz` likewise.
5. **DB persistence:** create a ticket, then `docker compose restart app` — the ticket survives
   (it's on the `db_data` volume).
6. **Cookie persistence across redeploy:** restart the app; you should still be logged in (proves
   the `keys_data` Data Protection volume is working).
7. **Image upload + render:** attach an image to a ticket and confirm it displays at `/Images/...`
   (proves `UseStaticFiles()` + the `images_data` volume).
8. **No open ports:** from outside, confirm your home IP exposes nothing (the tunnel is the only
   path); on the LAN, confirm `http://<pi-ip>:400` is **not** reachable (only `localhost` is).

---

## Out of scope (later, only if you outgrow one node)
- Migrating SQLite → Postgres/SQL Server and images → object storage.
- Cloudflare Access in front of the app (the app already has its own cookie auth).
- CI/CD auto-deploy on push.
- Running cloudflared in Docker instead of systemd (the original containerized variant).
