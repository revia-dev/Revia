#!/usr/bin/env bash
set -euo pipefail

pattern='(^|/)\.DS_Store$|(^|/)\.env$|\.(db|db-shm|db-wal|sqlite|sqlite3)$|(^|/)(game-files|bmi-photos|lending-photos|person-photos|uploads|backups)/|(^|/)(bin|obj|certs|\.lan-publish)/'
violations="$(git ls-files | grep -Ei "$pattern" || true)"

if [[ -n "$violations" ]]; then
  echo "Forbidden generated or private files are tracked:"
  echo "$violations"
  exit 1
fi

secret_pattern='"ApiKey"[[:space:]]*:[[:space:]]*"[^"]+"|[A-Fa-f0-9]{32}|eyJ[A-Za-z0-9_-]{30,}|sk-[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9-]{20,}|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY'
secret_matches="$(git grep -I -n -E "$secret_pattern" -- . || true)"

if [[ -n "$secret_matches" ]]; then
  echo "Potential secret material is tracked at:"
  echo "$secret_matches" | cut -d: -f1-2
  exit 1
fi

echo "Repository hygiene check passed."
