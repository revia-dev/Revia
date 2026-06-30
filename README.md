# Revia

**Revia** is an offline-capable **Progressive Web App (PWA)** for the household:
movies, TV guide, games & matches, lending, BMI tracking and wishlists — all in
one installable app, with light/dark mode and an consistent design.

Originally a Blazor Server app, Revia was rebuilt into a standalone
**Blazor WebAssembly PWA** with a clean **Client / Api / Shared** layering that
works offline and syncs changes as soon as a connection is available again.

| Dashboard (light) | Dashboard (dark) |
|---|---|
| ![Dashboard light](docs/screenshots/dashboard-light.png) | ![Dashboard dark](docs/screenshots/dashboard-dark.png) |

| TV guide (live progress) | People |
|---|---|
| ![TV](docs/screenshots/tv-light.png) | ![People](docs/screenshots/persons-light.png) |

| Movies | Login |
|---|---|
| ![Movies](docs/screenshots/movies-light.png) | ![Login](docs/screenshots/login-light.png) |

| Mobile (Dashboard) | Mobile (Movie detail) |
|---|---|
| ![Mobile Dashboard](docs/screenshots/mobile-dashboard.png) | ![Mobile Movie](docs/screenshots/mobile-moviedetail.png) |

---

## Highlights

- **Offline-first PWA** – read cache (IndexedDB) + mutation queue (outbox);
  the server wins on conflicts (last-write-wins). Service worker for true
  offline (close & reload the app), including photos visible while offline.
- **Native-app capabilities** – installable with a standalone manifest,
  **app shortcuts** (long-press menu), a **share target** (share a title into Revia
  → it searches your library), **Window Controls Overlay** (custom desktop title
  bar), and safe-area / overscroll / tap-highlight tuning plus cross-fade
  **View Transitions**. On startup it requests **persistent storage**
  (`navigator.storage.persist()`) so the OS can't evict your offline data, mirrors
  the **pending-sync count onto the app icon badge**, and can save a CSV backup to a
  real file via the **File System Access API** (with an **OPFS** rolling copy and a
  download fallback). See [PWA capabilities](docs/pwa-migration-plan.md).
- **Modules:**
  - **Movies** – TMDb posters, detail page with movie collections (including
    parent collections), full-text search; create/edit/delete **for admins only**.
    **Per-user watch progress**: every member tracks their own "how far did I get" —
    a resume minute for films, the last watched **season/episode** for series, and a
    `not started / in progress / watched` status. Private per user, offline-capable
    (cached + queued like every other write).
  - **TV guide** – "My program" + "Family" with a **live progress bar**
    (running for / X min left) and an **automatically advancing episode** for TV series.
  - **Games & matches** – with photo upload, rules/expansion files.
  - **Lending** – items, people, status, photos, household sharing.
  - **BMI**, **wishlists**, **people**.
- **People management:** every user can create people **without a login**
  (data only) as well as edit/delete them; an admin can create a person **directly
  with credentials** (username/password/role) or later **grant a login** to an
  existing login-less person. Share people with other households via QR/link
  (household linking).
- **Live scoring & cross-device point transfer:** card game/board game-style
  scoreboard; one payer can transfer points/money to **1..n** recipients via
  **QR code (with camera scan), code, or Web Share** (AirDrop where available).
- **Permissions & layouts:** per-user module read/write rights (enforced in the
  API, not just in the UI) and a personal dashboard layout (tiles/sections via
  **drag-and-drop** — touch-friendly — sortable and show/hide-able).
- **CSV backup:** export **and** import in **every** module — to back up, restore,
  or transfer between households/users (person references are resolved by name).
- **Global JSON backup (admin):** a whole-database export/import on the dashboard
  (`/api/backup`, admin-only) in the **same JSON format as the native app**, so the web
  and native databases can be kept consistent by exchanging one file. Restore is
  **selective** — only the tables present in the file are overwritten. Covers the movie
  catalogue (shared Guid ids) plus the per-person tables — **watch progress**, **BMI**,
  **loans**, **game matches** and **wishlists** — which **reconcile by name** (web Identity
  ids ≠ native UUIDs, so each row re-attaches to the local person/game of the same name;
  rows whose person/game can't be resolved are dropped rather than mis-attached).
  Wishlists are per-user on the web and household-wide on native, so the owner travels as a
  name (the importer becomes the owner when none is given). (See `Revia.Shared/Backup`.)
- **Light/dark mode** automatically following the system setting; **consistent,
  responsive design**; **localization** (German / English) via resource files.
- **Demo mode** without credentials, with seeded sample data (reset on every restart).
- **Security:** JWT (access + rotating refresh tokens, **hashed at rest**), login
  lockout **and edge rate limiting** on the auth endpoints, household-scoped data
  access; **69 unit + 19 integration tests**, 0 build warnings.
- **Operations & observability:** structured request logging, a global
  RFC 7807 ProblemDetails error handler (no stack traces leak to clients),
  `/health` + `/health/ready` (DB) probes, baseline security headers, and OpenAPI
  in Development. See [Operations](#operations--observability).

## Architecture

| Project | Role |
|---|---|
| `Revia.Client` | Blazor **WebAssembly** PWA (UI, offline cache, sync engine) |
| `Revia.Api` | ASP.NET Core host: serves the PWA **and** the REST API (JWT) |
| `Revia.Shared` | shared DTOs / contracts between Client and Api |
| `Revia` | existing domain/identity layer (reused by the Api) |
| `Revia.Tests` | xUnit tests |

Data storage: **SQLite** (EF Core). Photos/files live in configurable directories.

## Quick start (development)

Prerequisite: **.NET 10 SDK**.

```bash
# Start API + PWA (Development)
dotnet run --project Revia.Api
# -> http://localhost:5223
```

Default login: `admin` / `Admin123!` (seeded on first start; changeable via
`Seed:AdminPassword`).

> Note: in the Development build the service worker is a no-op. For **true
> offline** use the published build (see below / `run-lan.sh`).

## Tests

```bash
dotnet test Revia.Tests
```

Two test projects:

- **`Revia.Tests`** — unit tests of the domain logic (lending status, BMI, CSV
  round-trips, permissions, point transfer) **and the offline `SyncEngine`** (FIFO
  replay, exponential backoff, poison-message handling, server-wins dispatch),
  tested headlessly against in-memory fakes via the `IOutboxStore` / `IConnectivity`
  abstractions.
- **`Revia.IntegrationTests`** — end-to-end over the real HTTP pipeline with
  `WebApplicationFactory`: health probes, the login flow (200 / 401 / unauthorized
  without a token), and the auth rate limiter returning **429** under a burst.

```bash
dotnet test Revia.slnx   # runs both
```

Architecture decisions are recorded as ADRs in [docs/adr/](docs/adr/), and there's a
narrated code &amp; architecture walkthrough in
[docs/architecture/index.html](docs/architecture/index.html).

## Operations & observability

The API host is set up to run in production, not just on a laptop:

- **Structured logging** of every request (method, path, status, duration) via
  `AddHttpLogging`; startup migration/seed logs and auth events are logged too.
- **Distributed tracing + metrics** via **OpenTelemetry** (ASP.NET Core, HttpClient
  and runtime instrumentation). Traces/metrics are exported over OTLP **only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set**, so the app runs unchanged without a collector.
- **Fail-fast config:** in Production the host refuses to start without a strong
  (>= 32 char) `Jwt:SigningKey`, and logs a loud warning if the default admin
  password is still in place.
- **Consistent errors:** an `IExceptionHandler` turns any unhandled exception into
  an RFC 7807 ProblemDetails response with a trace id — the stack trace stays in the
  logs, never on the wire.
- **Health probes:** `GET /health` (liveness) and `GET /health/ready` (also checks
  the database). The Docker image ships a `HEALTHCHECK` against `/health`.
- **Rate limiting:** a fixed-window limiter throttles `…/api/auth/login` and
  `…/api/auth/refresh` (10/min) on top of Identity's per-account lockout.
- **Refresh tokens are hashed at rest** (SHA-256); the plaintext is returned to the
  client once and never stored, so a DB leak yields no usable token.
- **Security headers** (`X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`) on every response.
- **OpenAPI** is mapped at `/openapi/v1.json` in Development.

CI (`.github/workflows/ci.yml`) builds the **whole solution** with
`-warnaserror` and runs the tests; deploy publishes **`Revia.Api`** (the PWA + API
host), not the legacy project.

## Run on the LAN (published PWA)

`run-lan.sh` publishes the Api (including the WASM client + service worker) and
serves it **over HTTPS** on `0.0.0.0:5047` in Production — this is what makes true
offline work on the home network:

```bash
./setup-https.sh        # ONE-TIME: generate a local CA + certificate (mkcert)
./run-lan.sh            # publishes (if needed) + starts on :5047 (https)
./run-lan.sh --publish  # forces a fresh publish
```

Reachable on the Wi-Fi at `https://<server-name>.local:5047` or
`https://<server-ip>:5047` (get the IP on the server Mac with `ipconfig getifaddr en0`),
e.g. also from an iPhone. Via *Share → Add to Home Screen* Revia can be installed
as a PWA.

> **HTTPS is mandatory for offline to work.** Browsers register the service worker
> (the offline app cache) only in a **secure context** — i.e. HTTPS or
> `http://localhost`. Over a plain `http://` LAN IP the service worker is silently
> **never** registered, nothing is cached, and the app always needs the server.
> `setup-https.sh` uses **mkcert** to generate a local CA + certificate; the file
> `certs/rootCA.pem` must be installed and trusted **once on each phone**, otherwise
> the service worker stays blocked even over HTTPS. After that: open the HTTPS URL,
> **sign in once while online**, "Add to Home Screen" — from then on the app runs
> even with the server turned off.

## Docker (config-driven via `.env`)

Anyone who pulls the project only needs to set a few values in `.env` — the rest
runs out of the box (a fresh DB is migrated and the admin is seeded):

```bash
cp .env.example .env     # then adjust .env (at least REVIA_JWT_KEY)
docker compose up -d --build
# -> http://localhost:${REVIA_PORT}   (default 8080)
```

`.env.example` documents all switches (port, JWT key, admin password,
demo on/off + reset, TMDb key). `docker-compose.yml` reads them with safe
defaults; a persistent volume `revia-data` holds the DB + uploads.

> Offline in Docker: over **`http://localhost:8080`** the PWA works including
> offline out of the box (`localhost` is a secure context). For access over the
> **network** (LAN IP/domain) the offline service worker needs HTTPS — terminate
> it in front of the container with a **reverse proxy** (Caddy/Traefik/nginx), the
> container stays HTTP. See
> [docs/local-domain-https.md](docs/local-domain-https.md) section 3.

## Demo mode

With `Demo__Enabled=true` a **"View demo"** button appears on the login page that
logs in without credentials; a demo household with sample data is seeded at start
and **reset on every restart** (the demo uses its own household and leaves real
data untouched).

```bash
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__DefaultConnection="Data Source=/tmp/revia-demo.db" \
Demo__Enabled=true Jwt__SigningKey="<32+ characters>" \
dotnet Revia.Api.dll
```

## Local domain & HTTPS

On the LAN, HTTPS is **mandatory for offline to work** (service worker only in a
secure context). `./setup-https.sh` sets up a locally trusted CA + certificate
with **mkcert** once; `certs/rootCA.pem` must be installed on each phone. Details,
an optional local domain (`revia.local`) and notes on Let's Encrypt for a real
public domain are in **[docs/local-domain-https.md](docs/local-domain-https.md)**.

## License

Private project. Third-party software (vendored, in the client):
[qrcode-generator](https://github.com/kazuhikoarase/qrcode-generator) (MIT, QR
generation), [jsQR](https://github.com/cozmo/jsQR) (Apache-2.0, QR camera scan),
[SortableJS](https://github.com/SortableJS/Sortable) (MIT, touch drag-and-drop).
