using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PermissionEngine.Application.Telemetry;

namespace RbacSystem.Api.Infrastructure;

/// <summary>
/// Registers OpenTelemetry tracing and metrics for the RBAC system.
///
/// Tracing:
///   • ASP.NET Core inbound requests are auto-instrumented (spans created per request).
///   • HttpClient outbound calls are auto-instrumented.
///   • Custom RBAC pipeline spans from <see cref="RbacActivitySource"/> are included.
///   • Exported via OTLP gRPC to Jaeger (dev) or Azure Monitor (prod) using the
///     <c>OpenTelemetry:OtlpEndpoint</c> configuration key.
///   • Spans for /metrics scrape requests are filtered out to reduce noise.
///
/// Metrics:
///   • ASP.NET Core request metrics (http.server.request.duration, etc.) are included.
///   • Custom RBAC metrics from <see cref="RbacMetrics"/> are included.
///   • Exposed at <c>/metrics</c> in Prometheus text format via
///     <see cref="UseOpenTelemetryPrometheusScrapingEndpoint"/>.
///   • The Prometheus scraper (docker-compose prometheus service) polls /metrics every 15s.
///
/// Resource attributes (service.name, deployment.environment) are set here and
/// propagated to every trace and metric data point.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddRbacObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "rbac-system";
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var environment  = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: "1.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment
            });

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddSource(RbacActivitySource.Name)
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                    // Suppress noisy /metrics scrape spans — they would dominate traces
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics");
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(RbacMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());

        return services;
    }
}
