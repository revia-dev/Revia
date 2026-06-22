# 1. Offline-first sync via outbox + server-wins

Date: 2026-06-22

## Status

Accepted

## Context

Revia is a household PWA that must remain usable with no connectivity (the home
server may be off, the phone may be out of range) and reconcile changes when the
connection returns. We need a model for local reads, local writes, and conflict
resolution that is simple enough to reason about for a small multi-user household
and that does not require a CRDT/operational-transform engine.

## Decision

- **Reads** are served from the API when online (and cached to IndexedDB), and from
  the IndexedDB cache when offline.
- **Writes** are applied optimistically to the local cache and appended to an
  **outbox** (a queue of mutations) keyed by a client-generated GUID for idempotency.
- A **SyncEngine** replays the outbox **oldest-first** when connectivity returns,
  with **exponential backoff** on transient failures and a **poison** flag after a
  cap so one stuck mutation cannot wedge the queue. Each collection registers a
  replay handler; unknown collections are dropped.
- **Conflict resolution is server-wins (last-write-wins):** after a successful flush
  the client refreshes from the server, discarding divergent local state.

## Consequences

- **Pro:** simple mental model; resilient to flaky networks; the engine is pure and
  unit-tested headlessly (see `SyncEngineTests`) via the `IOutboxStore` /
  `IConnectivity` abstractions.
- **Con:** last-write-wins can silently lose a concurrent edit. Acceptable for a
  household's data volume and collaboration patterns; revisit (e.g. per-field merge
  or conflict surfacing) if multi-editor contention becomes real.
- A mutation that keeps failing is surfaced to the user as "poisoned" and can be
  retried manually, rather than blocking sync.
