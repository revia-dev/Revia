#!/usr/bin/env bash
#
# run-lan.sh — runs Revia on the LAN as a real PWA.
#
# Publishes the Api project (which also builds + bundles the WebAssembly client and
# its service worker) and serves it on http://0.0.0.0:5047 in Production. Because it
# is a published build, the service worker ships and true offline (close + reload)
# works for everyone on the LAN — unlike `dotnet run` (Development), which omits it.
#
# This replaces the old Blazor Server app that used to own port 5047.
#
# Usage:
#   ./run-lan.sh            # publish (if needed) + serve
#   ./run-lan.sh --publish  # force a fresh publish first
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJ="$ROOT/PrivateLibrary.Api/PrivateLibrary.Api.csproj"
OUT="$ROOT/.lan-publish"
PORT="${REVIA_LAN_PORT:-5047}"

# Shared SQLite DB + photo storage (the same files the app already uses). Absolute so
# they don't depend on the process working directory.
DB_PATH="$ROOT/PrivateLibrary/Data/private-library.db"
DATA_DIR="$ROOT/PrivateLibrary/Data"

# A stable JWT signing key so issued tokens survive a restart. Override in the
# environment for anything beyond a home LAN.
SIGNING_KEY="${REVIA_JWT_KEY:-revia-lan-stable-signing-key-please-override-32+chars!!}"

if [[ "${1:-}" == "--publish" || ! -f "$OUT/PrivateLibrary.Api.dll" ]]; then
    echo "==> Publishing Revia (Api + WASM client + service worker)…"
    rm -rf "$OUT"
    dotnet publish "$API_PROJ" -c Release -o "$OUT"
fi

echo "==> Serving Revia on http://0.0.0.0:${PORT}  (DB: ${DB_PATH})"
echo "    LAN devices can reach it at  http://<this-machine-ip>:${PORT}"

exec env \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=${DB_PATH}" \
    Jwt__SigningKey="${SIGNING_KEY}" \
    BmiPhotos__StoragePath="${DATA_DIR}/bmi-photos" \
    LendingPhotos__StoragePath="${DATA_DIR}/lending-photos" \
    PersonPhotos__StoragePath="${DATA_DIR}/person-photos" \
    GameFiles__StoragePath="${DATA_DIR}/game-files" \
    dotnet "$OUT/PrivateLibrary.Api.dll" \
        --contentRoot "$OUT" \
        --urls "http://0.0.0.0:${PORT}"
