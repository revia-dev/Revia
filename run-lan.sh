#!/usr/bin/env bash
#
# run-lan.sh — runs Revia on the LAN as a real PWA.
#
# Publishes the Api project (which also builds + bundles the WebAssembly client and
# its service worker) and serves it on https://0.0.0.0:5047 in Production. Because it
# is a published build, the service worker ships and true offline (close + reload)
# works for everyone on the LAN — unlike `dotnet run` (Development), which omits it.
#
# HTTPS is REQUIRED for offline: browsers only register a service worker (the
# offline app-shell cache) in a "secure context" — https or http://localhost. Over
# a plain-http LAN IP the service worker never registers, so the app always needs
# the server. We serve https with a locally-trusted mkcert certificate (generate it
# with ./setup-https.sh) and install the mkcert root CA on each phone once.
#
# This replaces the old Blazor Server app that used to own port 5047.
#
# Usage:
#   ./run-lan.sh            # publish (if needed) + serve
#   ./run-lan.sh --publish  # force a fresh publish first
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJ="$ROOT/Revia.Api/Revia.Api.csproj"
OUT="$ROOT/.lan-publish"
PORT="${REVIA_LAN_PORT:-5047}"
CERT="$ROOT/certs/revia-cert.pem"
KEY="$ROOT/certs/revia-key.pem"

# Shared SQLite DB + photo storage (the same files the app already uses). Absolute so
# they don't depend on the process working directory.
DB_PATH="$ROOT/Revia/Data/revia-library.db"
DATA_DIR="$ROOT/Revia/Data"

# A stable JWT signing key so issued tokens survive a restart. Override in the
# environment for anything beyond a home LAN.
SIGNING_KEY="${REVIA_JWT_KEY:-revia-lan-stable-signing-key-please-override-32+chars!!}"

if [[ "${1:-}" == "--publish" || ! -f "$OUT/Revia.Api.dll" ]]; then
    echo "==> Publishing Revia (Api + WASM client + service worker)…"
    rm -rf "$OUT"
    dotnet publish "$API_PROJ" -c Release -o "$OUT"
fi

# Prefer HTTPS (required for the offline PWA service worker). Fall back to plain
# HTTP only if the cert is missing, with a clear warning.
if [[ -f "$CERT" && -f "$KEY" ]]; then
    SCHEME=https
    CERT_ENV=("Kestrel__Certificates__Default__Path=$CERT" "Kestrel__Certificates__Default__KeyPath=$KEY")
else
    echo "WARN: certificate missing ($CERT) — serving HTTP; the offline PWA will NOT work." >&2
    echo "      Run ./setup-https.sh once to generate it." >&2
    SCHEME=http
    CERT_ENV=()
fi

echo "==> Serving Revia on ${SCHEME}://0.0.0.0:${PORT}  (DB: ${DB_PATH})"
echo "    LAN devices can reach it at  ${SCHEME}://<this-machine-ip>:${PORT}"

exec env \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=${DB_PATH}" \
    Jwt__SigningKey="${SIGNING_KEY}" \
    BmiPhotos__StoragePath="${DATA_DIR}/bmi-photos" \
    LendingPhotos__StoragePath="${DATA_DIR}/lending-photos" \
    PersonPhotos__StoragePath="${DATA_DIR}/person-photos" \
    GameFiles__StoragePath="${DATA_DIR}/game-files" \
    "${CERT_ENV[@]}" \
    dotnet "$OUT/Revia.Api.dll" \
        --contentRoot "$OUT" \
        --urls "${SCHEME}://0.0.0.0:${PORT}"
