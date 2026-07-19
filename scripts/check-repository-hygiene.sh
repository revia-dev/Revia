#!/usr/bin/env bash
set -euo pipefail

pattern='(^|/)\.DS_Store$|(^|/)\.env$|\.(db|db-shm|db-wal|sqlite|sqlite3)$|(^|/)(game-files|bmi-photos|lending-photos|person-photos|uploads|backups)/|(^|/)(bin|obj|certs|\.lan-publish)/'
violations="$(git ls-files | grep -Ei "$pattern" || true)"

if [[ -n "$violations" ]]; then
  echo "Forbidden generated or private files are tracked:"
  echo "$violations"
  exit 1
fi

echo "Repository hygiene check passed."
