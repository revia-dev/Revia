using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Revia.Api.Infrastructure;

/// <summary>
/// Wires OpenTelemetry tracing + metrics with auto-instrumentation for ASP.NET Core,
/// outgoing HttpClient calls and the .NET runtime. Distributed traces and metrics are
/// vendor-neutral and only exported when an OTLP endpoint is configured
/// (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>), so the app runs identically with or without a
/// collector attached — no hard dependency on observability infrastructure to boot.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "Revia.Api";

    public static IServiceCollection AddReviaTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        // The OTLP exporter is added only when an endpoint is set, keeping local/dev
        // runs free of export attempts while making production export a config switch.
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var exportEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: ServiceName,
                serviceVersion: typeof(Telemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation();
                if (exportEnabled) { tracing.AddOtlpExporter(); }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (exportEnabled) { metrics.AddOtlpExporter(); }
            });

        return services;
    }
}
