# Offline-PWA-Umbau – Architektur- & Migrationsplan

Status: **Entwurf zur Freigabe** · Erstellt 2026-06-18

Ziel: Die bestehende **Blazor-Server**-App „Revia" wird zu einer **offline-fähigen
Blazor-WebAssembly-PWA** mit sauberer **Client / Api / Shared**-Trennung. Offline
funktioniert per **Read-Cache (IndexedDB) + Mutations-Queue**; bei Konflikten
gewinnt der Server (last-write-wins).

Leitprinzip: **Die laufende Server-App bleibt während des gesamten Umbaus
funktionsfähig.** Es wird parallel gebaut und erst am Ende umgeschaltet.

---

## 1. Ausgangslage (vermessen)

- Rendering: durchgehend `InteractiveServer` (Server-seitige UI, SignalR-Pflicht → **nicht offline-fähig**).
- **24** routbare Seiten, **9** EF-Services + `PersonService` (UserManager), **27** Entitäten, **29** Migrationen.
- **8** Controller (Login/Logout, CSV-Export ×7, Foto-/Datei-/QR-Serving).
- **4** Datei-Speicher auf Platte (Lending-, Person-, BMI-Fotos, Game-Dateien).
- **4** JS-Module (`swipe.js`, `notifications.js`, `mediaCapture.js`, ReconnectModal).
- Auth: ASP.NET **Identity-Cookies** + Antiforgery; Household-Id als Claim; `CurrentUserContext` überall fürs Scoping; `HouseholdLinkService` für haushaltsübergreifende Sichtbarkeit.
- Server-only: `PosterBackfillService` (HostedService), `TmdbService` (externe API), CSV-Import beim Start, Direkt-Dateizugriff.

**Kernkonsequenz:** Blazor Server kann prinzipiell nicht offline. Der Umbau auf
WASM berührt jede Seite, weil heute jede Seite direkt server-seitige EF-Services
injiziert. Diese direkten Aufrufe werden durch HTTP-API-Aufrufe (über einen
Offline-fähigen Client-Datenlayer) ersetzt.

---

## 2. Zielarchitektur

Drei Projekte (neue Solution-Struktur):

```
PrivateLibrary.Shared   – DTOs, Enums, Validierung, Vertrags-Interfaces (kein EF, kein UI)
PrivateLibrary.Api      – ASP.NET Core Web API: EF Core, Identity, REST-Endpunkte,
                          Datei-Serving, TMDB, Hintergrundjobs, CSV. Hostet die WASM-App.
PrivateLibrary.Client   – Blazor WASM: UI (migrierte Razor-Komponenten), IndexedDB-Cache,
                          Mutations-Queue, Sync-Engine, Token-Auth.
PrivateLibrary.Tests    – bestehend, erweitert
```

- **Shared**: pro Modul Request/Response-DTOs (statt EF-Entities über die Leitung). Validierungsattribute hier, von Client und Api genutzt.
- **Api**: übernimmt die heutigen Services nahezu 1:1 (sie bleiben server-seitig!), exponiert sie als Controller/Minimal-API. Household-Scoping bleibt **server-seitig erzwungen** (Sicherheit nie im Client).
- **Client**: ruft API über typisierte `IModuleApi`-Clients. Dahinter liegt der Offline-Layer (siehe §4). UI-Komponenten kommen weitgehend unverändert herüber.

Das heutige `PrivateLibrary`-Web-Projekt wird zerlegt; vorhandene Service-Logik
zieht in `Api` um (geringes Risiko, da Logik gleich bleibt).

---

## 3. Authentifizierung (Cookie → Token)

- Neue Endpunkte `POST /api/auth/login`, `/logout`, `/refresh` liefern **JWT** (Access + Refresh).
- Household-Id + Rollen + Modulrechte als **Claims im JWT**.
- Client: `AuthenticationStateProvider` auf Token-Basis; Token sicher im Speicher + Refresh-Token in IndexedDB; automatischer Refresh.
- Antiforgery entfällt (Token-Header statt Cookie); CORS/Same-Origin sauber konfigurieren (Api hostet die App → same origin).
- `IHttpContextAccessor`-Nutzung in `MainLayout`/`Login` entfällt.
- `CurrentUserContext` wird im Client aus dem JWT gespeist; in der Api weiter aus dem validierten Token.

**Offline-Auth:** Letzter gültiger Auth-Zustand wird lokal gehalten, sodass die
App offline „angemeldet" bleibt (read-only Daten + Queue), bis das Token beim
nächsten Online-Sync erneuert/abgelehnt wird.

---

## 4. Offline-Datenlayer (Read-Cache + Mutations-Queue)

Pro Modul ein Client-Repository mit einheitlichem Muster:

**Lesen**
1. Online: API rufen → Antwort in **IndexedDB** ablegen → an UI geben.
2. Offline: direkt aus IndexedDB lesen (letzter bekannter Stand).
3. Jede Tabelle bekommt `updatedAt`/`rowVersion` für deterministische Aktualität.

**Schreiben (Create/Update/Delete)**
1. Optimistisch lokal in IndexedDB anwenden (UI reagiert sofort).
2. Mutation in eine persistente **Outbox-Queue** schreiben (Modul, Op, Payload, lokale Id, Zeitstempel).
3. Online: Queue der Reihe nach an die Api senden; Server-Antwort ersetzt den lokalen Optimistik-Eintrag (echte Ids, Server-Werte).
4. **Konflikt: Server gewinnt.** Schlägt eine Mutation am Server fehl/ist veraltet, wird der Server-Stand übernommen und der Nutzer per Toast informiert.
5. Queue ist idempotent (Client-Mutations-Id), übersteht App-Neustart, wird bei Reconnect automatisch abgearbeitet.

**Technik:** IndexedDB via schlankem JS-Interop-Modul (`offline-store.js`) +
C#-Wrapper; Online/Offline-Erkennung über `navigator.onLine` + Sync-Trigger bei
`online`-Event und beim App-Start.

**Bewusste Grenzen (vereinbart):** Kein Feld-Level-Merge, keine bidirektionale
Voll-Replikation. Binärdaten (Fotos/Dateien) offline: Anzeige aus Cache, Upload
wird gequeued; sehr große Dateien ggf. erst bei Verbindung.

---

## 5. Dateien & Binärdaten

- Foto-/Datei-Speicher bleiben **server-seitig** (Api), erreichbar über Endpunkte
  (`GET /api/files/...`). Service-Worker cached angezeigte Bilder für Offline-Anzeige.
- Uploads offline → in Outbox als Multipart-Mutation; Versand bei Reconnect.
- QR-Generierung (`PersonQrController`) bleibt Api-seitig.

---

## 6. Server-only → Api

- `PosterBackfillService` (HostedService) und `TmdbService` bleiben in der **Api**.
- CSV-Export/-Import bleiben Api-Endpunkte.
- DB-Bootstrap/Seeder laufen weiter beim Api-Start.

---

## 7. PWA / Service-Worker

- Wechsel vom Blazor-Server-SW zum **WASM-PWA-SW** (App-Shell + Assets offline cachen).
- `manifest.json` (bereits „Revia") übernehmen; Installierbarkeit + Offline-Start sicherstellen.
- Versionierte Caches, sauberes Update-Handling.

---

## 8. Phasenplan (iterativ, App bleibt lauffähig)

> Jede Phase endet mit grünem Build + Tests; die Server-App bleibt bis zur
> finalen Umschaltung parallel nutzbar.

- **Phase 0 — Plan (dieses Dokument).** Freigabe abwarten.
- **Phase 1 — Gerüst.** Solution in Client/Api/Shared aufteilen; Api hostet eine leere WASM-App; bestehende Services nach Api umziehen (ohne Verhaltensänderung); Server-App weiter buildbar/lauffähig.
- **Phase 2 — Auth.** Token-Login end-to-end (Api + Client), Household-Claims im JWT, AuthState im Client.
- **Phase 3 — Offline-Kern.** IndexedDB-Store, Outbox-Queue, Sync-Engine, Online/Offline-Erkennung — als wiederverwendbare Bausteine, unit-getestet.
- **Phase 4 — Pilotmodul „Wünsche".** Ein Modul komplett offline (DTOs, Api-Endpunkte, Client-Repo, UI migriert). Validiert das gesamte Muster end-to-end.
- **Phase 5 — Module nachziehen.** Filme, Ausleihen (inkl. Kategorien/Personen/Fotos/Links), Spiele (inkl. Partien/Dateien), BMI, TV — je Modul ein abgeschlossener Schritt.
- **Phase 6 — Dashboard/Analytics/Permissions/Layouts** auf neuen Datenlayer heben.
- **Phase 7 — Härtung.** Konfliktfälle, Queue-Wiederanlauf, Fehlertoleranz, große Listen (Filme: 885+), Foto-Offline, PWA-Update, E2E-Offline-Tests (Netz aus → bedienen → Netz an → Sync).
- **Phase 8 — Umschaltung.** LAN-Server auf das Api-Projekt umstellen; Alt-Pfad entfernen.

**Reihenfolge der Module** in Phase 5 nach Risiko/Abhängigkeit:
Filme (read-lastig, einfach) → BMI → TV → Spiele → Ausleihen (komplexeste:
Fotos, Kategorien, Personen, Haushalts-Links).

---

## 9. Risiken & offene Punkte

- **Auth in WASM** (kein HttpOnly-Cookie) – Token-Sicherheit/XSS-Härtung beachten.
- **Datenmenge offline** – selektives Caching statt „alles" (z. B. Filme paginiert/lazy).
- **Fotos offline** – Upload-Queue + Cache-Strategie, Speicherlimits im Browser.
- **Haushalts-Sicherheit** – Scoping strikt server-seitig; Client-Cache darf nie fremde Haushalte enthalten.
- **Migrationsaufwand** – realistisch mehrere Wochen; deshalb strikt iterativ mit lauffähigen Zwischenständen.

---

## 10. Definition of Done

- Installierbare PWA auf Smartphone/Tablet, Start ohne erreichbaren Server.
- Lesen offline aus lokalem Cache; Schreiben offline → automatische Synchronisierung bei Verbindung; Server gewinnt bei Konflikt.
- Saubere Client/Api/Shared-Trennung; Household-Scoping server-seitig erzwungen.
- Funktionsparität mit der heutigen App; Tests grün; LAN-Server läuft auf dem neuen Stand.
