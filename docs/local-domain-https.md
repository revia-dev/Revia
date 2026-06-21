# Lokale Domain & kostenloses HTTPS

Revia läuft im LAN **standardmäßig über HTTPS** unter `https://<server-ip>:5047`
bzw. `https://<server-name>.local:5047` – die Server-IP findest du auf dem Mac
mit `ipconfig getifaddr en0`.

> **HTTPS ist Pflicht, nicht optional.** Browser registrieren den Service Worker
> (den Offline-App-Cache) nur in einem **secure context** – also HTTPS oder
> `http://localhost`. Über eine reine `http://`-LAN-IP gibt es **gar keinen**
> Service Worker, nichts wird gecacht und die App braucht immer den laufenden
> Server. Deshalb serviert `run-lan.sh` / der LaunchAgent jetzt HTTPS; reines
> `http://…:5047` wird abgewiesen.
>
> **Schnellstart:** einmalig `./setup-https.sh` ausführen (erzeugt mit **mkcert**
> eine lokale CA + Zertifikat und kopiert `certs/rootCA.pem` zum Verteilen).
> Dann auf jedem Handy `certs/rootCA.pem` installieren und als vertrauenswürdig
> markieren (siehe Abschnitt 2). Eine lokale Domain (`revia.local`, Abschnitt 1)
> ist optional, aber praktisch, weil der Name stabil bleibt, wenn sich die IP
> per DHCP ändert.

## 1. Lokale Domain `revia.local`

### macOS / Linux / Windows (hosts-Datei)

Trage die IP des Server-Macs in die `hosts`-Datei **jedes Geräts** ein, das die
App per Namen erreichen soll. Auf dem Server-Mac selbst (einmalig, mit sudo):

```bash
# Server-IP ermitteln und als revia.local eintragen:
echo "$(ipconfig getifaddr en0)   revia.local" | sudo tee -a /etc/hosts
# danach testen:
curl -I http://revia.local:5047
```

`/etc/hosts` (macOS/Linux) bzw. `C:\Windows\System32\drivers\etc\hosts` (Windows):

```
192.168.2.81   revia.local
```

### iPhone / iPad (iOS hat keinen hosts-Zugriff)

iOS lässt keine `hosts`-Bearbeitung zu. Zwei Wege:

1. **Einfachster Weg – IP direkt nutzen:** `http://192.168.2.81:5047` im Safari
   öffnen. Funktioniert sofort, ohne weitere Einrichtung. Über *Teilen → Zum
   Home-Bildschirm* lässt sich Revia als PWA installieren.
2. **`revia.local` per Router-DNS:** Trage im Router einen lokalen DNS-Eintrag /
   statischen Host an, der `revia.local` auf die Server-IP zeigt. Beispiele:
   - **FritzBox:** Heimnetz → Netzwerk → dem Server-Mac einen Namen geben; bzw.
     einen lokalen DNS-Rewrite in „Netzwerkeinstellungen" setzen.
   - **Pi-hole / AdGuard Home:** unter *Local DNS Records* `revia.local` → IP.
   Sobald der Router das auflöst, erreichen **alle** WLAN-Geräte (inkl. iPhone)
   `http://revia.local:5047` ohne hosts-Datei.

## 2. Vertrauenswürdiges HTTPS mit mkcert (Standard, kostenlos)

[mkcert](https://github.com/FiloSottile/mkcert) erzeugt ein **lokal
vertrauenswürdiges** Zertifikat – kein „Nicht sicher" im Browser. Das übernimmt
das Helfer-Skript `setup-https.sh`:

```bash
brew install mkcert nss          # einmalig (nss nur für Firefox)
cd /Users/revia-dev/Documents/PrivateLibrary
./setup-https.sh                 # installiert die lokale CA (fragt dein Passwort)
                                 # und erzeugt certs/revia-cert.pem + revia-key.pem
```

`setup-https.sh` deckt automatisch die LAN-IP, `<server-name>.local`, `localhost`
und `127.0.0.1` als SANs ab und kopiert `certs/rootCA.pem` zum Verteilen auf die
Geräte. Danach startet die App über `run-lan.sh` bzw. den LaunchAgent **von
selbst per HTTPS** – das Zertifikat wird aus `certs/` gelesen, keine weiteren
Umgebungsvariablen nötig.

Erreichbar unter `https://<server-name>.local:5047` bzw. `https://<server-ip>:5047`
(bzw. ohne Portangabe hinter einem Reverse-Proxy) ohne Warnung.

> Brauchst du zusätzlich die Domain `revia.local` im Zertifikat (Abschnitt 1), gib
> sie beim Erzeugen mit an, z. B.:
> ```bash
> mkcert -cert-file certs/revia-cert.pem -key-file certs/revia-key.pem \
>     revia.local "$(scutil --get LocalHostName).local" \
>     "$(ipconfig getifaddr en0)" localhost 127.0.0.1
> ```

> Die `certs/`-Dateien sind **nicht** im Repo (siehe `.gitignore`) – sie sind
> geräte-/netzspezifisch.

### mkcert-Root-CA auf dem iPhone vertrauen

Damit das iPhone dem mkcert-Zertifikat vertraut (sonst „Nicht sicher"), muss die
**mkcert-Root-CA** einmalig auf dem Gerät installiert und vertraut werden:

1. Root-CA-Datei auf dem Mac finden und aufs iPhone bringen:
   ```bash
   open "$(mkcert -CAROOT)"     # zeigt rootCA.pem im Finder
   ```
   Schicke `rootCA.pem` per AirDrop ans iPhone (oder per Mail an dich selbst).
2. Auf dem iPhone die Datei öffnen → „Profil wurde geladen" →
   **Einstellungen → Allgemein → VPN & Geräteverwaltung → Profil installieren**.
3. Anschließend **Einstellungen → Allgemein → Info → Zertifikatsvertrauen** öffnen
   und für die **mkcert**-Root-CA das volle Vertrauen aktivieren (Schalter an).
4. Jetzt öffnet Safari `https://revia.local:5047` (Router-DNS vorausgesetzt, s. o.)
   ohne Zertifikatswarnung.

> Hinweis: Das Zertifikat muss die lokale Domain als SAN enthalten – oben ist
> `revia.local` bereits im `mkcert …`-Aufruf angegeben.

## 3. Echte öffentliche Domain + Let's Encrypt (optional)

Für eine echte Domain wie `revia-library.com` (Domain kostenpflichtig, ~10–15 €/Jahr):

1. Domain registrieren und ihren A-/AAAA-Record auf die öffentliche IP zeigen
   lassen (Port-Forwarding/Reverse-Proxy nötig).
2. Hinter einem Reverse-Proxy mit automatischem Let's Encrypt betreiben, z. B.
   **Caddy** (am einfachsten):

   ```caddyfile
   revia-library.com {
       reverse_proxy localhost:8080
   }
   ```

   Caddy holt und erneuert das Zertifikat automatisch und kostenlos.

Alternativen: Traefik oder nginx + `certbot`. In allen Fällen läuft Revia selbst
auf HTTP hinter dem Proxy; der Proxy terminiert TLS.
