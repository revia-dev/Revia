using Xunit;

// All integration tests share one SQLite database file (see ReviaApiFactory's static
// ctor). Running test classes in parallel makes them write to that file concurrently,
// which intermittently trips SQLite's "database is locked" (surfacing as unrelated
// failures, e.g. the health checks). Serialise the classes so the shared host/DB is
// never hit concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
