# Revia

**Revia** ist eine offline-fähige **Progressive Web App (PWA)** für den Haushalt:
Filme, TV-Guide, Spiele & Partien, Ausleihen, BMI-Tracking und Wunschlisten – in
einer App, installierbar, mit Hell-/Dunkelmodus und einem iOS-ähnlichen Design.

Ursprünglich eine Blazor-Server-App, wurde Revia zu einer eigenständigen
**Blazor WebAssembly PWA** mit sauberer **Client / Api / Shared**-Schichtung
umgebaut, die offline funktioniert und Änderungen synchronisiert, sobald wieder
Verbindung besteht.

| Dashboard (hell) | Dashboard (dunkel) |
|---|---|
| ![Dashboard hell](docs/screenshots/dashboard-light.png) | ![Dashboard dunkel](docs/screenshots/dashboard-dark.png) |

| Filme | Login |
|---|---|
| ![Filme](docs/screenshots/movies-light.png) | ![Login](docs/screenshots/login-light.png) |

| Mobil (Dashboard) | Mobil (Film-Detail) |
|---|---|
| ![Mobil Dashboard](docs/screenshots/mobile-dashboard.png) | ![Mobil Film](docs/screenshots/mobile-moviedetail.png) |

---

## Highlights

- **Offline-first PWA** – Lese-Cache (IndexedDB) + Mutations-Queue (Outbox);
  der Server gewinnt bei Konflikten (Last-Write-Wins). Service Worker für echtes
  Offline (App schließen & neu laden).
- **Module:** Filme (inkl. TMDb-Poster & Detailseite), TV-Guide, Spiele mit
  Partien, Ausleihen, BMI, Wunschlisten, Personen.
- **Rechte & Layouts:** pro Nutzer Modul-Lese/Schreibrechte (in der API
  erzwungen, nicht nur in der UI) und ein persönliches Dashboard-Layout
  (Kacheln/Bereiche per Drag-and-Drop sortier- und ein-/ausblendbar).
- **Live-Wertung & geräteübergreifender Punkte-Tausch:** card game/board game-Style
  Scoreboard; ein Zahler kann an **1..n** Empfänger via QR-Code / Web Share
  (AirDrop, wo verfügbar) Punkte/Geld übertragen.
- **Haushalts-Verknüpfung:** Person per QR teilen → der andere Haushalt
  übernimmt sie → beide Haushalte teilen ihre Ausleihen.
- **Hell-/Dunkelmodus** automatisch nach Systemeinstellung; **Lokalisierung**
  (Deutsch / Englisch) über Ressourcendateien.
- **Demo-Modus** ohne Zugangsdaten, mit geseedeten Beispieldaten.
- **Sicherheit:** JWT (Access + rotierende Refresh-Tokens), Login-Lockout,
  household-scoped Datenzugriffe.

## Architektur

| Projekt | Rolle |
|---|---|
| `PrivateLibrary.Client` | Blazor **WebAssembly** PWA (UI, Offline-Cache, Sync-Engine) |
| `PrivateLibrary.Api` | ASP.NET Core Host: serviert die PWA **und** die REST-API (JWT) |
| `PrivateLibrary.Shared` | geteilte DTOs / Verträge zwischen Client und Api |
| `PrivateLibrary` | bestehende Domänen-/Identity-Schicht (von der Api wiederverwendet) |
| `PrivateLibrary.Tests` | xUnit-Tests |

Datenhaltung: **SQLite** (EF Core). Fotos/Dateien liegen in konfigurierbaren
Verzeichnissen.

## Schnellstart (Entwicklung)

Voraussetzung: **.NET 10 SDK**.

```bash
# API + PWA starten (Development)
dotnet run --project PrivateLibrary.Api
# -> http://localhost:5223
```

Standard-Login: `admin` / `Admin123!` (beim ersten Start geseedet; via
`Seed:AdminPassword` änderbar).

> Hinweis: Im Development-Build ist der Service Worker ein No-Op. Für **echtes
> Offline** den Published-Build verwenden (siehe unten / `run-lan.sh`).

## Tests

```bash
dotnet test PrivateLibrary.Tests
```

## Im LAN betreiben (Published-PWA)

`run-lan.sh` published die Api (inkl. WASM-Client + Service Worker) und serviert
sie auf `0.0.0.0:5047` in Production – damit funktioniert echtes Offline im
Heimnetz:

```bash
./run-lan.sh            # published (falls nötig) + startet auf :5047
./run-lan.sh --publish  # erzwingt frisches Publish
```

## Docker

```bash
docker compose up -d --build
# -> http://localhost:8080
```

`docker-compose.yml` legt ein persistentes Volume `revia-data` für DB + Uploads an.
**Wichtig:** `Jwt__SigningKey` (und ggf. `Seed__AdminPassword`) in der Compose-Datei
auf eigene Werte setzen.

## Demo-Modus

Mit `Demo__Enabled=true` (am besten gegen eine **separate** Demo-Datenbank)
erscheint auf der Login-Seite ein **„Demo ansehen"**-Button, der ohne
Zugangsdaten einloggt; ein Demo-Haushalt mit Beispieldaten wird beim Start
geseedet.

```bash
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__DefaultConnection="Data Source=/tmp/revia-demo.db" \
Demo__Enabled=true Jwt__SigningKey="<32+ Zeichen>" \
dotnet PrivateLibrary.Api.dll
```

## Lokale Domain & HTTPS

Siehe **[docs/local-domain-https.md](docs/local-domain-https.md)** für eine
kostenlose lokale Domain (`revia.local`) und ein lokal vertrauenswürdiges
HTTPS-Zertifikat mit **mkcert** (sowie Hinweise zu Let's Encrypt für eine echte
öffentliche Domain).

## Lizenz

Privates Projekt. Drittsoftware: QR-Generierung via
[qrcode-generator](https://github.com/kazuhikoarase/qrcode-generator) (MIT).
