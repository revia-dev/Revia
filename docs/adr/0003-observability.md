# 3. Observability: structured logs, OpenTelemetry, health, ProblemDetails

Date: 2026-06-22

## Status

Accepted

## Context

A sync system with optimistic writes and conflict resolution is unmaintainable in
production if you can't see what it did. The app previously had no logging, no
metrics/tracing, no health probes, and leaked raw exceptions to clients.

## Decision

- **Structured request logging** via `AddHttpLogging` (method, path, status,
  duration); startup migration/seed and auth events are logged explicitly.
- **Distributed tracing + metrics** via **OpenTelemetry** with auto-instrumentation
  for ASP.NET Core, outgoing HttpClient and the .NET runtime. The **OTLP exporter is
  added only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set**, so the app boots identically
  with or without a collector — no hard dependency on observability infrastructure.
- **Health probes:** `/health` (liveness) and `/health/ready` (EF Core DB check),
  with a Docker `HEALTHCHECK`.
- **Consistent errors:** a global `IExceptionHandler` emits RFC 7807 ProblemDetails
  with a trace id; the stack trace stays in the logs, never on the wire.

## Consequences

- **Pro:** the running system is inspectable (logs/traces/metrics) and orchestrators
  can gate traffic on readiness; clients get a stable error contract with a
  correlation id.
- **Con:** OpenTelemetry adds dependencies and a small per-request overhead.
  Mitigated by exporting only when configured.
- Operators correlate a client-visible trace id to the full server-side error,
  without the app ever exposing internals.
