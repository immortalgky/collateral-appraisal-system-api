using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Observability;

public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        if (!options.Enabled)
            return services;

        // Not "is not null": Microsoft.Extensions.Configuration's JSON provider stores an explicit
        // JSON `null` leaf as an empty string, not a true null (IConfiguration's backing store is
        // string-only) — config binding then leaves OtlpEndpoint as "" rather than null. Confirmed
        // via config.GetSection("Observability:Tracing:OtlpEndpoint").Exists() == true here.
        //
        // No environment has an OTLP collector (see project_no_external_observability_stack), so
        // this is never true today — skip WithTracing() entirely rather than registering it with no
        // exporter. Same reasoning as the WithMetrics() removal below: a TracerProvider with
        // instrumentation subscribed but nothing reading the spans still samples and builds them for
        // nothing. Nothing in this codebase depends on a TracerProvider being registered — every
        // Activity.Current/ActivitySource.StartActivity call site (CorrelationIdMiddleware,
        // BusinessContextBehavior, WorkflowTracing/WorkflowSpan) is already null-safe, because
        // StartActivity legitimately returns null whenever there's no listener, which was already a
        // normal outcome (e.g. sampling) even with tracing configured.
        var hasOtlpExporter = !string.IsNullOrEmpty(options.Tracing.OtlpEndpoint);

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName
                }));

        if (hasOtlpExporter)
        {
            builder.WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(o =>
                    {
                        o.SetDbStatementForText = environment.IsDevelopment();
                    })
                    .AddSource("MassTransit")
                    .AddSource("Workflow")
                    // Null-forgiving: hasOtlpExporter (checked above, outside this lambda) already
                    // guarantees OtlpEndpoint is non-empty here — the compiler can't see across the
                    // closure boundary to know that.
                    .AddOtlpExporter(o => o.Endpoint = new Uri(options.Tracing.OtlpEndpoint!));
            });
        }
        // No WithMetrics() — the Prometheus exporter was the only reader and is gone (no
        // Prometheus/Grafana anywhere), and a MeterProvider with instrumentation but no reader
        // still subscribes and aggregates for nothing. SystemMetricsSampler reads
        // Microsoft.AspNetCore.Hosting directly via its own MeterListener, independent of this
        // OpenTelemetry pipeline.

        return services;
    }
}
