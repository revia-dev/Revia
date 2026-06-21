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

| Fernsehprogramm (Live-Fortschritt) | Personen |
|---|---|
| ![TV](docs/screenshots/tv-light.png) | ![Personen](docs/screenshots/persons-light.png) |

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
  Offline (App schließen & neu laden), inkl. offline sichtbarer Fotos.
- **Module:**
  - **Filme** – TMDb-Poster, Detailseite mit Filmreihen (inkl. übergeordneter
    Reihen), Volltextsuche; Anlegen/Bearbeiten/Löschen **nur für Admins**.
  - **Fernsehprogramm** – „Mein Programm" + „Familie" mit **Live-Fortschritts­balken**
    (läuft seit / noch X min) und **automatisch hochzählender Folge** bei TV-Serien.
  - **Spiele & Partien** – mit Foto-Upload, Spielregeln/Erweiterungs-Dateien.
  - **Ausleihen** – Gegenstände, Personen, Status, Fotos, Haushalts-Sharing.
  - **BMI**, **Wunschlisten**, **Personen**.
- **Personenverwaltung:** jeder Benutzer kann Personen **ohne Login** anlegen
  (reine Daten) sowie bearbeiten/löschen; ein Admin kann eine Person **direkt mit
  Anmeldedaten** (Benutzername/Passwort/Rolle) anlegen oder einer bestehenden
  loginlosen Person später **Login geben**. Personen per QR/Link mit anderen
  Haushalten teilen (Haushalts-Verknüpfung).
- **Live-Wertung & geräteübergreifender Punkte-Tausch:** card game/board game-Style
  Scoreboard; ein Zahler kann an **1..n** Empfänger via **QR-Code (mit Kamera-Scan),
  Code oder Web Share** (AirDrop, wo verfügbar) Punkte/Geld übertragen.
- **Rechte & Layouts:** pro Nutzer Modul-Lese/Schreibrechte (in der API
  erzwungen, nicht nur in der UI) und ein persönliches Dashboard-Layout
  (Kacheln/Bereiche per **Drag-and-Drop** – touch-tauglich – sortier- und
  ein-/ausblendbar).
- **CSV-Backup:** in **jedem** Modul Export **und** Import – zum Sichern,
  Wiederherstellen oder Übertragen zwischen Haushalten/Benutzern (Personen­bezüge
  werden per Name aufgelöst).
- **Hell-/Dunkelmodus** automatisch nach Systemeinstellung; **iOS-ähnliches,
  responsives Design**; **Lokalisierung** (Deutsch / Englisch) über Ressourcendateien.
- **Demo-Modus** ohne Zugangsdaten, mit geseedeten Beispieldaten (wird bei jedem
  Neustart zurückgesetzt).
- **Sicherheit:** JWT (Access + rotierende Refresh-Tokens), Login-Lockout,
  household-scoped Datenzugriffe; 56 Unit-Tests, 0 Build-Warnings.

## Architektur

| Projekt | Rolle |
|---|---|
| `Revia.Client` | Blazor **WebAssembly** PWA (UI, Offline-Cache, Sync-Engine) |
| `Revia.Api` | ASP.NET Core Host: serviert die PWA **und** die REST-API (JWT) |
| `Revia.Shared` | geteilte DTOs / Verträge zwischen Client und Api |
| `Revia` | bestehende Domänen-/Identity-Schicht (von der Api wiederverwendet) |
| `Revia.Tests` | xUnit-Tests |

Datenhaltung: **SQLite** (EF Core). Fotos/Dateien liegen in konfigurierbaren
Verzeichnissen.

## Schnellstart (Entwicklung)

Voraussetzung: **.NET 10 SDK**.

```bash
# API + PWA starten (Development)
dotnet run --project Revia.Api
# -> http://localhost:5223
```

Standard-Login: `admin` / `Admin123!` (beim ersten Start geseedet; via
`Seed:AdminPassword` änderbar).

> Hinweis: Im Development-Build ist der Service Worker ein No-Op. Für **echtes
> Offline** den Published-Build verwenden (siehe unten / `run-lan.sh`).

## Tests

```bash
dotnet test Revia.Tests
```

## Im LAN betreiben (Published-PWA)

`run-lan.sh` published die Api (inkl. WASM-Client + Service Worker) und serviert
sie **per HTTPS** auf `0.0.0.0:5047` in Production – damit funktioniert echtes
Offline im Heimnetz:

```bash
./setup-https.sh        # EINMALIG: lokale CA + Zertifikat erzeugen (mkcert)
./run-lan.sh            # published (falls nötig) + startet auf :5047 (https)
./run-lan.sh --publish  # erzwingt frisches Publish
```

Erreichbar im WLAN unter `https://<server-name>.local:5047` bzw.
`https://<server-ip>:5047` (IP auf dem Server-Mac mit `ipconfig getifaddr en0`),
z. B. auch vom iPhone. Über *Teilen → Zum Home-Bildschirm* lässt sich Revia als
PWA installieren.

> **HTTPS ist Pflicht fürs Offline-Funktionieren.** Browser registrieren den
> Service Worker (den Offline-App-Cache) nur in einem **secure context** – also
> HTTPS oder `http://localhost`. Über eine reine `http://`-LAN-IP wird der Service
> Worker stillschweigend **nie** registriert, nichts wird gecacht und die App
> braucht immer den Server. `setup-https.sh` erzeugt mit **mkcert** eine lokale
> CA + Zertifikat; die Datei `certs/rootCA.pem` muss **einmalig auf jedem Handy**
> installiert und als vertrauenswürdig markiert werden, sonst bleibt der Service
> Worker auch unter HTTPS blockiert. Danach: HTTPS-URL öffnen, **einmal online
> anmelden**, „Zum Home-Bildschirm" – ab dann läuft die App auch bei
> ausgeschaltetem Server.

## Docker (config-getrieben über `.env`)

Wer das Projekt zieht, muss nur ein paar Werte in `.env` setzen – der Rest läuft
out of the box (frische DB wird migriert und der Admin geseedet):

```bash
cp .env.example .env     # dann .env anpassen (mindestens REVIA_JWT_KEY)
docker compose up -d --build
# -> http://localhost:${REVIA_PORT}   (Default 8080)
```

`.env.example` dokumentiert alle Schalter (Port, JWT-Key, Admin-Passwort,
Demo an/aus + Reset, TMDb-Key). `docker-compose.yml` liest sie mit sicheren
Defaults; ein persistentes Volume `revia-data` hält DB + Uploads.

> Offline im Docker-Betrieb: Über **`http://localhost:8080`** funktioniert die
> PWA inkl. Offline out of the box (`localhost` ist ein secure context). Für
> Zugriff über das **Netzwerk** (LAN-IP/Domain) braucht der Offline-Service-Worker
> HTTPS – das terminiert man vor dem Container per **Reverse-Proxy** (Caddy/
> Traefik/nginx), der Container bleibt HTTP. Siehe
> [docs/local-domain-https.md](docs/local-domain-https.md) Abschnitt 3.

## Demo-Modus

Mit `Demo__Enabled=true` erscheint auf der Login-Seite ein **„Demo ansehen"**-
Button, der ohne Zugangsdaten einloggt; ein Demo-Haushalt mit Beispieldaten wird
beim Start geseedet und **bei jedem Neustart zurückgesetzt** (Demo nutzt einen
eigenen Haushalt und lässt echte Daten unberührt).

```bash
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__DefaultConnection="Data Source=/tmp/revia-demo.db" \
Demo__Enabled=true Jwt__SigningKey="<32+ Zeichen>" \
dotnet Revia.Api.dll
```

## Lokale Domain & HTTPS

HTTPS ist im LAN **Pflicht fürs Offline-Funktionieren** (Service Worker nur im
secure context). `./setup-https.sh` richtet einmalig eine lokal vertrauenswürdige
CA + Zertifikat mit **mkcert** ein; `certs/rootCA.pem` muss auf jedem Handy
installiert werden. Details, eine optionale lokale Domain (`revia.local`) und
Hinweise zu Let's Encrypt für eine echte öffentliche Domain stehen in
**[docs/local-domain-https.md](docs/local-domain-https.md)**.

## Lizenz

Privates Projekt. Drittsoftware (vendored, im Client):
[qrcode-generator](https://github.com/kazuhikoarase/qrcode-generator) (MIT, QR-
Erzeugung), [jsQR](https://github.com/cozmo/jsQR) (Apache-2.0, QR-Kamera-Scan),
[SortableJS](https://github.com/SortableJS/Sortable) (MIT, Touch-Drag-and-Drop).
