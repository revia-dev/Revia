# 2. Token-based auth: JWT access + rotating hashed refresh tokens

Date: 2026-06-22

## Status

Accepted

## Context

The standalone WASM client can't rely on the server's cookie auth (it's a separate
origin/host concern and needs to attach credentials to API calls itself). We need
stateless request authorization plus a way to stay signed in without re-entering
credentials, and the ability to revoke a session.

## Decision

- **Short-lived JWT access tokens** carry the same claims the cookie path produced
  (user id, name, roles, household_id) by reusing the app's claims factory.
- **Long-lived refresh tokens** are issued alongside and **rotated on every use**
  (the old one is revoked when a new pair is issued).
- **Refresh tokens are hashed at rest** (SHA-256, hex). The plaintext is returned to
  the client once and never persisted; lookup hashes the incoming token. A DB leak
  therefore yields no usable token.
- Authorization is **enforced in the API** (per-module read/write via
  `ModuleAccessAttribute`), not just hidden in the UI.
- **Defense in depth on the login surface:** Identity per-account lockout *and* an
  edge fixed-window rate limiter on `…/auth/login` and `…/auth/refresh`.
- **Fail-fast config:** in Production the host refuses to start without a strong
  (>= 32 char) `Jwt:SigningKey` and rejects the shipped default admin password.

## Consequences

- **Pro:** stateless verification; revocable sessions; leak-resistant token storage;
  brute-force throttling at two layers. Verified end-to-end by integration tests
  (login 200, wrong password 401, rate-limit 429, protected route 401 without token).
- **Con:** rotation means a refresh token is single-use; a lost race (two tabs
  refreshing simultaneously) can force a re-login. Acceptable for the household scale.
- Refresh tokens being hashed means they cannot be displayed or recovered server-side
  — only revoked.
