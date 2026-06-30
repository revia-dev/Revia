# Lern-Zusammenfassung: Watch-Progress, PWA-Fähigkeiten & On-Device-KI

Dieses Dokument fasst die drei Erweiterungen zusammen, die in einem Durchgang über
**beide** Revia-Apps (Blazor-WASM-PWA + native Apple-App) eingebaut wurden — als
Lernmaterial: *was*, *warum* und *wie*. Es ist bewusst konzeptionell gehalten; die
genauen Stellen im Code sind verlinkt.

---

## 1. Per-Benutzer-Sehfortschritt

### Idee
Jeder Benutzer eines Haushalts führt **seinen eigenen** Fortschritt pro Titel:
- **Film** → Wiederaufnahme-Minute (`positionSeconds`).
- **Serie** → zuletzt gesehene **Staffel/Folge** (`season`/`episode`).
- **Status** → `unwatched` / `inProgress` / `watched`.

Wichtig: Das ist *nicht* dasselbe wie das schon vorhandene, geteilte `watched`-Flag
am Film (Katalog-Ebene). Fortschritt ist **privat pro Person**.

### Datenmodell — die zentrale Entscheidung
Ein Titel ist geteilt (Haushalt), Fortschritt ist persönlich → also eine eigene
Tabelle/Entität mit dem Schlüssel **(Benutzer, Film)**, nicht ein Feld am Film.

**Revia (.NET / EF Core)** — [`WatchProgress.cs`](../Revia/Data/WatchProgress.cs):
- Felder `HouseholdId` (Mandant), `UserId` (Identity-User), `MovieId`, `Status`,
  `PositionSeconds?`, `Season?`, `Episode?`, `UpdatedAt`.
- Eindeutiger Index `(UserId, MovieId)` = der Upsert-Schlüssel; zusätzlicher Index
  `(HouseholdId, UserId, Status)` für „Weiterschauen"-Abfragen.
- EF-Migration `AddWatchProgress` — wird beim Start automatisch via
  `db.Database.MigrateAsync()` angewandt (deshalb funktioniert auch Docker/LAN ohne
  manuellen Schritt).

**ReviaApple (SwiftData)** — [`WatchProgress.swift`](../../ReviaApple/Revia/Models/WatchProgress.swift):
- Dieselbe Form, `personId` statt `UserId`.
- **CloudKit-kompatibel**: keine Unique-Constraints, jedes Nicht-Optional hat einen
  Default — sonst lehnt CloudKit das Schema ab.
- Schema-Evolution **explizit**: neues `ReviaSchemaV2` + eine *lightweight*
  (additive) `MigrationStage`. Das ist die Lektion: bei SwiftData + CloudKit fügt man
  Modelle über eine versionierte Schema-Kette hinzu, nicht „magisch".

### Sicherheits-/Mandanten-Lektion
Jede Lese-/Schreiboperation ist auf `(household, user)` eingeschränkt
([`WatchProgressService`](../Revia/Modules/Progress/WatchProgressService.cs)):
- Ein fremder Film (anderer Haushalt) → `null` → Controller antwortet `404`, statt
  eine verwaiste Zeile zu schreiben.
- Benutzer A kann den Fortschritt von Benutzer B weder sehen noch ändern.
- Auf Apple-Seite läuft die Schreibprüfung über `AppEnvironment` (der einzige Mutator)
  und braucht **Lese**-Recht für Filme — denn es sind *eigene* Daten, kein
  Admin-Katalog-Schreibrecht.

### Offline (nur PWA)
Fortschritt nutzt dieselbe Offline-Architektur wie alles andere: optimistischer
Cache-Schreibvorgang + Outbox-Mutation, die später per `PUT /api/progress`
(idempotenter Upsert) repliziert wird —
[`WatchProgressClientService`](../Revia.Client/Features/Progress/WatchProgressClientService.cs).

### Wo man es sieht
- PWA: Film-Detailseite, Abschnitt „Mein Fortschritt".
- Apple: `MovieDetailView` → [`WatchProgressSection`](../../ReviaApple/Revia/Features/Movies/WatchProgressSection.swift).

---

## 2. Native PWA-Fähigkeiten (nur Revia-Web)

Ziel: Die Blazor-WASM-PWA soll sich wie eine echte App anfühlen **und** ihre
client-seitigen Daten schützen. Umgesetzt in
[`pwa.js`](../Revia.Client/wwwroot/js/pwa.js) (degradiert immer sauber) +
[`PwaInterop.cs`](../Revia.Client/Features/Pwa/PwaInterop.cs) (typsicherer .NET-Wrapper).

| Fähigkeit | Was es bringt | Wo |
|---|---|---|
| `navigator.storage.persist()` | OS darf IndexedDB (Cache + Outbox) **nicht** mehr evakuieren — die wichtigste Zeile für eine client-only App | beim Start in `index.html` |
| **Badging API** | Anzahl unsynchronisierter Mutationen auf dem App-Icon | an `SyncEngine`-Events gekoppelt |
| **File System Access** | CSV-Backup in eine **echte** Datei speichern (Fallback: Download) | „Als Datei sichern" im CSV-Backup |
| **OPFS** | schnelles, privates rollendes Lokal-Backup | beim Speichern mitgeschrieben |
| **Web Share / Share Target** | Titel in Revia teilen → durchsucht die Bibliothek (`/movies?q=…`) | Manifest + `Movies.razor` |
| **App-Shortcuts** | Long-Press-Menü (Filme / TV / Übersicht) | `manifest.json` |
| **Window Controls Overlay** | eigene Titelleiste am Desktop | Manifest `display_override` + CSS `env(titlebar-area-*)` |
| **Safe-Area / Overscroll / Tap-Highlight / View Transitions** | iOS-Feel, weicher Seitenwechsel | `app.css` |

Manifest-Erweiterungen (`id`, `scope`, `display_override`, `categories`,
`launch_handler`, `shortcuts`, `share_target`): [`manifest.json`](../Revia.Client/wwwroot/manifest.json).
Der Service Worker (App-Shell-Caching für Sofortstart) war bereits vorhanden und
bleibt bewusst **API-frei** — Daten gehören in die Offline-Schicht, nicht in den SW-Cache.

**Lektion:** JS-Interop für native Browser-APIs immer *feature-detecten* und neutral
zurückfallen lassen, damit die C#-Seite nie selbst prüfen muss.

---

## 3. On-Device-KI (nur ReviaApple, Apple Foundation Models)

### Architektur-Leitlinie: Grounding-first
Das Modell macht **nur** Verständnis, Struktur und Begründung. Fakten (welche Titel
es gibt, deren Tags/Bewertungen) kommen **immer** aus dem echten Katalog — über
Snapshots oder das `LibrarySearchTool` (Tool-Calling). So kann das Modell keine Filme
„erfinden", die du nicht besitzt. Das ist das CV-taugliche Senior-Signal.

### Verfügbarkeit & Eskalation (die wichtigste Engineering-Lektion)
Foundation Models gibt es erst ab iOS/macOS **26** und nur auf iPhone 15 Pro /
Apple-Silicon-Macs aufwärts. Damit das Projekt **überall** baut und getestet werden
kann, ist der gesamte Framework-Pfad doppelt abgesichert:

```
#if canImport(FoundationModels)        // kompiliert nur, wo das SDK das Framework hat
    if #available(iOS 26, macOS 26, *) // Laufzeit-Gate
        switch SystemLanguageModel.default.availability { … }
#endif
```

Fehlt das Framework (ältere OS **oder** die headless SwiftPM-Test-Toolchain), nutzt
der Service eine **deterministische, voll getestete Heuristik**
([`EntryTextHeuristics`](../../ReviaApple/Revia/AI/EntryTextHeuristics.swift)). Die
öffentliche API von [`ReviaAIService`](../../ReviaApple/Revia/AI/ReviaAIService.swift)
ist in beiden Fällen identisch — **die UI verzweigt nie auf Verfügbarkeit**.

> Hinweis: Das aktuell installierte SDK ist älter als Foundation Models, daher wird
> der `#if canImport`-Block hier herauskompiliert und der Heuristik-Pfad ist das, was
> baut/getestet wird. Der Foundation-Models-Code zielt auf das **Xcode-26-SDK** und
> ist dort zu verifizieren.

### Features
- **Freitext → Struktur** via `@Generable` ([`AIGenerables.swift`](../../ReviaApple/Revia/AI/AIGenerables.swift)):
  „gestern Dune 2 gesehen, 9/10" → Titel/Jahr/Status/Bewertung → legt den Film an
  **und** setzt direkt den persönlichen Fortschritt (Verbindung zu Feature 1!).
- **Auto-Tagging / Zusammenfassen** im Editor.
- **Übersetzen / Umschreiben** eigener Notizen (offline).
- **Geerdeter Assistent**: Chat über die eigene Watchlist mit Tool-Calling.
- **Semantische Suche / Empfehlung mit Begründung** — über die Heuristik geerdet.
- **Multimodal** (Plakat-Foto → Titel) ist als nächster Schritt vorgesehen, sobald
  das Bild-Eingabe-API des 26er-SDK verfügbar ist (Roadmap).

---

## Querschnitt-Lektionen

1. **Geteilt vs. persönlich** ist eine Datenmodell-Entscheidung — eigene Tabelle mit
   Verbund-Schlüssel statt Feld am geteilten Objekt.
2. **Schema-Evolution explizit** halten: EF-Migration bzw. SwiftData-`VersionedSchema`
   + `MigrationStage` — nie auf implizite Magie verlassen (besonders mit CloudKit).
3. **Graceful Degradation** überall: Browser-API fehlt → Fallback; KI-Framework fehlt
   → Heuristik. Gleiche öffentliche API, keine Verzweigung in der UI.
4. **Grounding gegen Halluzination**: das Modell strukturiert/begründet, Fakten liefert
   der eigene Datenbestand.
5. **Tests an der richtigen Stelle**: reine Logik (Heuristik, Service) ist
   headless/unit-testbar; das End-to-End-Verhalten deckt ein Integrationstest ab.

## Tests (Stand dieser Erweiterung)
- `Revia.Tests`: **69** (inkl. 6 neue Watch-Progress-Service-Tests).
- `Revia.IntegrationTests`: **14** (inkl. 2 neue Progress-Endpoint-Tests).
- ReviaApple `swift test`: **46** (inkl. Heuristik-, KI-Fallback- und
  Watch-Progress-Store-Tests).

## Offen / Roadmap
- **Screenshots** (`docs/screenshots/`) zeigen die Fortschritts-UI noch nicht —
  nach einem App-Lauf neu aufnehmen.
- **Multimodaler KI-Import** (Plakat → Titel), sobald das 26er-Bild-API verfügbar ist.
- Foundation-Models-Pfad unter Xcode 26 kompilieren/verifizieren.
