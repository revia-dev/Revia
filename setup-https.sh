#!/usr/bin/env bash
#
# setup-https.sh — one-time HTTPS setup so the Revia PWA works offline.
#
# WHY: browsers only register a service worker (the offline app-shell cache) in a
# "secure context" — https, or http://localhost. Over a plain-http LAN IP
# (http://192.168.x.x) the service worker silently never registers, so nothing is
# cached and the app always needs the server. Serving https with a locally-trusted
# certificate fixes that. We use mkcert, which creates a local certificate
# authority (CA) and issues a cert for this machine's LAN IP + .local hostname.
#
# Run this once on the server Mac. Then install the printed root-CA file on every
# phone/tablet (one-time per device) so the certificate is trusted there too —
# without that, the phone shows a cert warning AND the service worker stays blocked.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERT_DIR="$ROOT/certs"
mkdir -p "$CERT_DIR"

if ! command -v mkcert >/dev/null 2>&1; then
    echo "mkcert not found. Install it first:  brew install mkcert" >&2
    exit 1
fi

LAN_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || true)"
HOSTLOCAL="$(scutil --get LocalHostName 2>/dev/null || hostname -s).local"

echo "==> Installing mkcert local CA into the system trust store (may ask for your password)…"
mkcert -install

echo "==> Issuing a certificate for: ${LAN_IP:-<no-ip>} ${HOSTLOCAL} localhost 127.0.0.1"
mkcert -cert-file "$CERT_DIR/revia-cert.pem" -key-file "$CERT_DIR/revia-key.pem" \
    ${LAN_IP:+$LAN_IP} "$HOSTLOCAL" localhost 127.0.0.1

# Copy the root CA next to the cert so it is easy to AirDrop / send to phones.
cp "$(mkcert -CAROOT)/rootCA.pem" "$CERT_DIR/rootCA.pem"

cat <<EOF

✅ Done.
   Server cert : $CERT_DIR/revia-cert.pem  (used by run-lan.sh / the LaunchAgent)
   Root CA     : $CERT_DIR/rootCA.pem     (install this on each phone/tablet)

   Reach the app at:
     https://${HOSTLOCAL}:5047           (stable across Wi-Fi/DHCP changes)
     https://${LAN_IP:-<this-mac-ip>}:5047   (fallback)

   On each phone, ONCE:
     iPhone/iPad : AirDrop rootCA.pem -> open -> Settings ▸ Profile loaded ▸ Install,
                   then Settings ▸ General ▸ About ▸ Certificate Trust Settings ▸
                   enable full trust for "mkcert …".
     Android     : Settings ▸ Security ▸ Encryption & credentials ▸ Install a
                   certificate ▸ CA certificate ▸ pick rootCA.pem.
   Then open the https URL, log in once (online), "Add to Home Screen". After that
   it opens and works with the Mac/server off.
EOF
