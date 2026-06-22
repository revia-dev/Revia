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
- **Modules:**
  - **Movies** – TMDb posters, detail page with movie collections (including
    parent collections), full-text search; create/edit/delete **for admins only**.
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
- **Light/dark mode** automatically following the system setting; **consistent,
  responsive design**; **localization** (German / English) via resource files.
- **Demo mode** without credentials, with seeded sample data (reset on every restart).
- **Security:** JWT (access + rotating refresh tokens), login lockout,
  household-scoped data access; 56 unit tests, 0 build warnings.

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
