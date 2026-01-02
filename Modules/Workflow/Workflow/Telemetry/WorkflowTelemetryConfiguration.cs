using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Workflow.Telemetry;

/// <summary>
/// Configuration options for workflow telemetry.
/// </summary>
public class WorkflowTelemetryOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "WorkflowTelemetry";

    /// <summary>
    /// Whether telemetry is enabled. Defaults to true in Development, false otherwise.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to enable tracing. Defaults to true.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Whether to enable metrics. Defaults to true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Whether to export to console. Defaults to true in Development.
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Whether to enable OTLP exporter. Defaults to false.
    /// </summary>
    public bool EnableOtlpExporter { get; set; } = false;

    /// <summary>
    /// OTLP endpoint for traces and metrics.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Service name for telemetry resource.
    /// </summary>
    public string ServiceName { get; set; } = "WorkflowService";

    /// <summary>
    /// Service version for telemetry resource.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Additional resource attributes.
    /// </summary>
    public Dictionary<string, object> ResourceAttributes { get; set; } = new();

    /// <summary>
    /// Sampling configuration for traces.
    /// </summary>
    public SamplingOptions Sampling { get; set; } = new();

    /// <summary>
    /// Performance tuning options.
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();

    /// <summary>
    /// Export configuration options.
    /// </summary>
    public ExportOptions Export { get; set; } = new();

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (EnableOtlpExporter && string.IsNullOrWhiteSpace(OtlpEndpoint))
        {
            throw new InvalidOperationException($"{nameof(OtlpEndpoint)} must be provided when {nameof(EnableOtlpExporter)} is true.");
        }
    }
}

/// <summary>
/// Sampling configuration for telemetry data.
/// </summary>
public class SamplingOptions
{
    /// <summary>
    /// Trace sampling ratio (0.0 to 1.0). Defaults to 1.0 (sample all traces).
    /// </summary>
    public double TraceRatio { get; set; } = 1.0;

    /// <summary>
    /// Activity sampling strategy.
    /// </summary>
    public string SamplingStrategy { get; set; } = "AlwaysOn";

    /// <summary>
    /// Custom sampling rules for specific operations.
    /// </summary>
    public Dictionary<string, double> CustomRules { get; set; } = new();
}

/// <summary>
/// Performance tuning options for telemetry.
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// Maximum queue size for telemetry data. Defaults to 2048.
    /// </summary>
    public int MaxQueueSize { get; set; } = 2048;

    /// <summary>
    /// Export timeout in seconds. Defaults to 30.
    /// </summary>
    public int ExportTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Batch export size. Defaults to 512.
    /// </summary>
    public int BatchExportSize { get; set; } = 512;

    /// <summary>
    /// Maximum number of attributes per span/metric. Defaults to 128.
    /// </summary>
    public int MaxAttributes { get; set; } = 128;

    /// <summary>
    /// Maximum length of attribute values. Defaults to 1024.
    /// </summary>
    public int MaxAttributeLength { get; set; } = 1024;
}

/// <summary>
/// Export configuration options.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Console export options.
    /// </summary>
    public ConsoleExportOptions Console { get; set; } = new();

    /// <summary>
    /// OTLP export options.
    /// </summary>
    public OtlpExportOptions Otlp { get; set; } = new();
}

/// <summary>
/// Console exporter configuration.
/// </summary>
public class ConsoleExportOptions
{
    /// <summary>
    /// Whether to include timestamps. Defaults to true.
    /// </summary>
    public bool IncludeTimestamps { get; set; } = true;

    /// <summary>
    /// Whether to include scopes. Defaults to true.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// Whether to use single line format. Defaults to false.
    /// </summary>
    public bool SingleLine { get; set; } = false;
}

/// <summary>
/// OTLP exporter configuration.
/// </summary>
public class OtlpExportOptions
{
    /// <summary>
    /// Export protocol (grpc or http/protobuf). Defaults to "grpc".
    /// </summary>
    public string Protocol { get; set; } = "grpc";

    /// <summary>
    /// Additional headers for OTLP export.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Timeout for OTLP export in seconds. Defaults to 10.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to use compression. Defaults to true.
    /// </summary>
    public bool UseCompression { get; set; } = true;
}

/// <summary>
/// Extension methods for configuring workflow telemetry.
/// </summary>
public static class WorkflowTelemetryConfiguration
{
    /// <summary>
    /// Adds workflow telemetry configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Configure options with defaults based on environment
        services.Configure<WorkflowTelemetryOptions>(options =>
        {
            configuration.GetSection(WorkflowTelemetryOptions.SectionName).Bind(options);
            
            // Set environment-specific defaults
            if (environment.IsDevelopment())
            {
                options.EnableConsoleExporter = options.EnableConsoleExporter || true;
            }
        });

        // Validate configuration
        services.PostConfigure<WorkflowTelemetryOptions>(options => options.Validate());

        // Configure OpenTelemetry only if enabled
        var telemetryOptions = configuration.GetSection(WorkflowTelemetryOptions.SectionName).Get<WorkflowTelemetryOptions>() 
            ?? new WorkflowTelemetryOptions();

        if (!telemetryOptions.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => ConfigureResource(resourceBuilder, telemetryOptions))
            .WithTracing(tracerBuilder => ConfigureTracing(tracerBuilder, telemetryOptions, environment))
            .WithMetrics(meterBuilder => ConfigureMetrics(meterBuilder, telemetryOptions, environment));

        return services;
    }

    /// <summary>
    /// Configures the OpenTelemetry resource with workflow-specific attributes.
    /// </summary>
    /// <param name="resourceBuilder">The resource builder.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The configured resource builder.</returns>
    private static ResourceBuilder ConfigureResource(ResourceBuilder resourceBuilder, WorkflowTelemetryOptions options)
    {
        var attributes = new Dictionary<string, object>
        {
            ["service.name"] = options.ServiceName,
            ["service.version"] = options.ServiceVersion,
            ["workflow.module"] = "CollateralAppraisal.Workflow",
            ["workflow.version"] = WorkflowTelemetryConstants.Version
        };

        // Add custom resource attributes
        foreach (var (key, value) in options.ResourceAttributes)
        {
            attributes[key] = value;
        }

        return resourceBuilder.AddAttributes(attributes);
    }

    /// <summary>
    /// Configures OpenTelemetry tracing for workflow operations.
    /// </summary>
    /// <param name="tracerBuilder">The tracer provider builder.</param>
    /// <param name="options">The telemetry options.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The configured tracer provider builder.</returns>
    private static TracerProviderBuilder ConfigureTracing(
        TracerProviderBuilder tracerBuilder,
        WorkflowTelemetryOptions options,
        IHostEnvironment environment)
    {
        if (!options.EnableTracing)
        {
            return tracerBuilder;
        }

        tracerBuilder
            // Add workflow-specific activity source
            .AddSource(WorkflowTelemetryConstants.ActivitySourceName)
            // Configure sampling based on options
            .SetSampler(CreateSampler(options))
            // Add ASP.NET Core instrumentation for HTTP requests
            .AddAspNetCoreInstrumentation(aspNetOptions =>
            {
                aspNetOptions.RecordException = true;
                aspNetOptions.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("workflow.http.endpoint", request.Path);
                };
                aspNetOptions.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("workflow.http.status_code", response.StatusCode);
                };
            })
            // Add HTTP client instrumentation for external calls
            .AddHttpClientInstrumentation(httpOptions =>
            {
                httpOptions.RecordException = true;
                httpOptions.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    activity.SetTag("workflow.external_call.url", request.RequestUri?.ToString());
                    activity.SetTag("workflow.external_call.method", request.Method.Method);
                };
                httpOptions.EnrichWithHttpResponseMessage = (activity, response) =>
                {
                    activity.SetTag("workflow.external_call.status_code", (int)response.StatusCode);
                };
            })
            // Add Entity Framework Core instrumentation for database operations
            .AddEntityFrameworkCoreInstrumentation(efOptions =>
            {
                efOptions.SetDbStatementForText = true;
                efOptions.SetDbStatementForStoredProcedure = true;
                efOptions.EnrichWithIDbCommand = (activity, command) =>
                {
                    activity.SetTag("workflow.db.operation", "query");
                };
            });

        // Add exporters based on configuration
        if (options.EnableConsoleExporter)
        {
            tracerBuilder.AddConsoleExporter();
        }

        if (options.EnableOtlpExporter && !string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            tracerBuilder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                otlpOptions.TimeoutMilliseconds = options.Export.Otlp.TimeoutSeconds * 1000;
                
                foreach (var header in options.Export.Otlp.Headers)
                {
                    otlpOptions.Headers = $"{otlpOptions.Headers},{header.Key}={header.Value}".TrimStart(',');
                }
            });
        }

        return tracerBuilder;
    }

    /// <summary>
    /// Configures OpenTelemetry metrics for workflow operations.
    /// </summary>
    /// <param name="meterBuilder">The meter provider builder.</param>
    /// <param name="options">The telemetry options.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The configured meter provider builder.</returns>
    private static MeterProviderBuilder ConfigureMetrics(
        MeterProviderBuilder meterBuilder,
        WorkflowTelemetryOptions options,
        IHostEnvironment environment)
    {
        if (!options.EnableMetrics)
        {
            return meterBuilder;
        }

        meterBuilder
            // Add workflow-specific meter
            .AddMeter(WorkflowTelemetryConstants.MeterName)
            // Add ASP.NET Core instrumentation for HTTP metrics
            .AddAspNetCoreInstrumentation()
            // Add HTTP client instrumentation for external call metrics
            .AddHttpClientInstrumentation();

        // Add exporters based on configuration
        if (options.EnableConsoleExporter)
        {
            meterBuilder.AddConsoleExporter();
        }

        if (options.EnableOtlpExporter && !string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            meterBuilder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                otlpOptions.TimeoutMilliseconds = options.Export.Otlp.TimeoutSeconds * 1000;
                
                foreach (var header in options.Export.Otlp.Headers)
                {
                    otlpOptions.Headers = $"{otlpOptions.Headers},{header.Key}={header.Value}".TrimStart(',');
                }
            });
        }

        return meterBuilder;
    }

    /// <summary>
    /// Creates a sampler based on the configuration options.
    /// </summary>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The configured sampler.</returns>
    private static Sampler CreateSampler(WorkflowTelemetryOptions options)
    {
        return options.Sampling.SamplingStrategy.ToLowerInvariant() switch
        {
            "alwayson" => new AlwaysOnSampler(),
            "alwaysoff" => new AlwaysOffSampler(),
            "traceidratio" => new TraceIdRatioBasedSampler(options.Sampling.TraceRatio),
            _ => new AlwaysOnSampler()
        };
    }

    /// <summary>
    /// Adds workflow telemetry services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowTelemetryServices(this IServiceCollection services)
    {
        // Register telemetry constants as singleton for easy access
        services.AddSingleton(WorkflowTelemetryConstants.ActivitySource);
        services.AddSingleton(WorkflowTelemetryConstants.Meter);

        return services;
    }
}