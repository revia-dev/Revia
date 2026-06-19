# Lokale Domain & kostenloses HTTPS

Revia läuft im LAN standardmäßig unter `http://<server-ip>:5047`. Diese URL
funktioniert sofort auf **jedem Gerät im selben WLAN** (Mac, iPhone, …) – die
Server-IP findest du auf dem Mac mit `ipconfig getifaddr en0`.

Mit einer lokalen Domain und einem lokal vertrauenswürdigen Zertifikat wird
daraus `https://revia.local` – ohne Browser-Warnung und kostenlos.

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

## 2. Vertrauenswürdiges HTTPS mit mkcert (empfohlen, kostenlos)

[mkcert](https://github.com/FiloSottile/mkcert) erzeugt ein **lokal
vertrauenswürdiges** Zertifikat – kein „Nicht sicher" im Browser.

```bash
# einmalig: lokale CA installieren
brew install mkcert nss   # macOS (nss für Firefox)
mkcert -install

# Zertifikat für die lokale Domain erzeugen
cd /Users/revia-dev/Documents/PrivateLibrary
mkdir -p certs
mkcert -cert-file certs/revia.local.pem -key-file certs/revia.local-key.pem revia.local localhost 127.0.0.1
```

Revia (Kestrel) auf HTTPS starten – die Zertifikatsdateien via Umgebung angeben:

```bash
ASPNETCORE_URLS="https://0.0.0.0:5047" \
Kestrel__Certificates__Default__Path="certs/revia.local.pem" \
Kestrel__Certificates__Default__KeyPath="certs/revia.local-key.pem" \
./run-lan.sh
```

Damit ist die App unter `https://revia.local:5047` (bzw. ohne Portangabe hinter
einem Reverse-Proxy) ohne Warnung erreichbar.

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
