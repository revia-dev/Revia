# Lokale Domain & kostenloses HTTPS

Revia läuft im LAN standardmäßig unter `http://<ip>:5047`. Mit einer lokalen
Domain und einem lokal vertrauenswürdigen Zertifikat wird daraus
`https://revia.local` – ohne Browser-Warnung und kostenlos.

## 1. Lokale Domain `revia.local`

Trage die IP des Server-Macs in die `hosts`-Datei **jedes Geräts** ein, das die
App per Namen erreichen soll.

**macOS / Linux** (`/etc/hosts`):

```
192.168.1.50   revia.local
```

**Windows** (`C:\Windows\System32\drivers\etc\hosts`): gleiche Zeile.

> Für mobile Geräte ohne `hosts`-Zugriff entweder die IP direkt nutzen oder im
> Router einen lokalen DNS-Eintrag (z. B. Pi-hole / FritzBox) für `revia.local`
> setzen.

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
einem Reverse-Proxy) ohne Warnung erreichbar. Für mobile Geräte muss die mkcert-
Root-CA (`mkcert -CAROOT`) einmalig auf dem Gerät installiert/vertraut werden.

> Die `certs/`-Dateien sind **nicht** im Repo (siehe `.gitignore`) – sie sind
> geräte-/netzspezifisch.

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
