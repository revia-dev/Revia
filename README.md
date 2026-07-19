# Revia Web

Revia is an offline-capable household organizer built as a Blazor WebAssembly
PWA with an ASP.NET Core API. It combines a movie library, TV guide, games,
lending, BMI tracking, wishlists and household members in one installable
application.

![Dashboard](docs/screenshots/dashboard-light.png)

## What this repository demonstrates

- Offline-first reads through IndexedDB and an outbox for deferred writes.
- A hosted PWA with responsive light and dark themes.
- JWT authentication with rotating, hashed refresh tokens.
- Per-household authorization and per-module read/write permissions.
- SQLite persistence through Entity Framework Core migrations.
- Selective JSON backup compatible with the native Revia application.
- Health checks, structured HTTP logging, Problem Details and OpenTelemetry.
- Unit and HTTP integration tests in CI.

## Architecture

| Project | Responsibility |
|---|---|
| `Revia.Client` | Blazor WebAssembly UI, offline cache and synchronization |
| `Revia.Api` | ASP.NET Core API and PWA host |
| `Revia.Shared` | DTOs and cross-application backup contracts |
| `Revia` | Domain model, EF Core, Identity and reusable services |
| `Revia.Tests` | Domain and synchronization unit tests |
| `Revia.IntegrationTests` | Authentication, health, backup and API tests |

The client depends on shared contracts, not on database entities. The API owns
authentication, household scoping and persistence. Offline mutations are replayed
in order; transient failures back off and permanently failing entries are
quarantined instead of blocking the queue.

Architecture decisions are recorded in [`docs/adr`](docs/adr/).

## Features

- Movies with search, collections, posters and per-user watch progress
- Personal and shared TV schedules with live progress
- Games, matches, score tracking and file attachments
- Loans with people, dates, status transitions and photos
- BMI records, wishlists and household members
- Personal dashboard layouts and module permissions
- CSV exchange per module and selective full JSON backup
- German and English localization
- Installable PWA with offline application shell

## Requirements

- .NET 10 SDK
- A modern browser
- Optional: Docker Desktop
- Optional for LAN HTTPS: `mkcert`

## Development

Start the API project; it hosts both the API and the WebAssembly client:

```bash
dotnet restore Revia.slnx
dotnet run --project Revia.Api
```

Open `http://localhost:5223`.

Default development account:

```text
Username: admin
Password: Admin123!
```

In an IDE, select `Revia.Api` as the startup project. `Revia.Client` alone only
serves static WebAssembly assets and cannot process login requests.

Configuration belongs in environment variables, user secrets or an untracked
`.env` file. Copy `.env.example` for Docker/LAN settings.

## Tests

```bash
dotnet test Revia.slnx
```

The unit suite covers domain invariants, permissions, CSV round trips, layout
serialization, notifications and offline replay behavior. Integration tests run
through the real HTTP pipeline and cover login, authorization, rate limiting,
health probes and cross-application backup reconciliation.

CI restores, builds with warnings treated as errors and runs both test projects.

## Published LAN build

`0.0.0.0` is a bind address, not a browser address.

```bash
./setup-https.sh        # one-time local certificate setup
./run-lan.sh --publish  # publish and serve on HTTPS port 5047
```

Open `https://localhost:5047` on the host or
`https://<host-ip>:5047` from another device. The local CA must be trusted on
each client device for the service worker to work.

Stop the server with:

```bash
kill "$(lsof -tiTCP:5047 -sTCP:LISTEN)"
```

## Docker

```bash
cp .env.example .env
docker compose up -d --build
```

The database and uploads live in a Docker volume. For LAN access, terminate TLS
at a reverse proxy so the PWA remains in a secure browser context.

## Data and security

Local databases (`*.db`, `*.sqlite*`), runtime uploads, certificates, secrets and
publish output are ignored by Git. Uploaded files are stored in configurable
directories and are included only in explicit application backups.

Production requires a unique admin password and a JWT signing key of at least
32 characters. The API rejects missing or weak production signing keys.

## Production considerations

Public deployment requires environment-specific infrastructure, external secret
management, monitoring retention and a documented backup/restore runbook.
