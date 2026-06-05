using Appraisal.Infrastructure;
using Auth.Infrastructure;
using Reporting;
using Common;
using Document.Data;
using Hangfire;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Request.Infrastructure;
using Request.Infrastructure.Reappraisal;
using Collateral.CollateralMasters.Services;
using Scalar.AspNetCore;
using Dapper;
using Shared.Configurations;
using Shared.Data;
using Shared.Data.Dapper;
using Shared.Data.Outbox;
using Shared.Logging;
using Shared.Security;
using Shared.Time;
using Integration.Application.EventHandlers.Outbound;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Messaging.Services;
using Workflow.Data;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Add shared services (time abstraction, security, etc.)
builder.Services.AddSharedServices(builder.Configuration);

// Dapper type handlers — DateOnly columns are returned by SqlClient as DateTime.
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
builder.Services.AddHangfire(builder.Configuration);

// Common services: carter, mediatR, fluentvalidators, etc.
var apiAssembly = typeof(Program).Assembly;
var requestAssembly = typeof(RequestModule).Assembly;
var authAssembly = typeof(AuthModule).Assembly;
var notificationAssembly = typeof(NotificationModule).Assembly;
var parameterAssembly = typeof(ParameterModule).Assembly;
var documentAssembly = typeof(DocumentModule).Assembly;
var workflowAssembly = typeof(WorkflowModule).Assembly;
var collateralAssembly = typeof(CollateralModule).Assembly;
var appraisalAssembly = typeof(AppraisalModule).Assembly;
var integrationAssembly = typeof(IntegrationModule).Assembly;
var commonAssembly = typeof(CommonModule).Assembly;
var reportingAssembly = typeof(ReportingModule).Assembly;

builder.Services.AddCarterWithAssemblies(apiAssembly, requestAssembly, authAssembly, notificationAssembly,
    parameterAssembly, documentAssembly, workflowAssembly, collateralAssembly, appraisalAssembly, integrationAssembly,
    commonAssembly, reportingAssembly);
builder.Services.AddMediatRWithAssemblies(apiAssembly, requestAssembly, authAssembly, notificationAssembly,
    parameterAssembly, documentAssembly, workflowAssembly, collateralAssembly, appraisalAssembly, integrationAssembly,
    commonAssembly, reportingAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// builder.Services.AddMassTransitWithAssemblies(builder.Configuration, requestAssembly, authAssembly,
//     notificationAssembly);

builder.Services.AddScoped<ISqlConnectionFactory>(provider =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("Database")!));

// Integration event outbox (per-module persistent outbox)
builder.Services.AddScoped<IOutboxScope, OutboxScope>();
builder.Services.AddScoped<IIntegrationEventOutbox, IntegrationEventOutbox>();
builder.Services.AddScoped(typeof(InboxGuard<>));
builder.Services.AddHostedService<IntegrationEventDeliveryService<RequestDbContext>>();
builder.Services.AddHostedService<IntegrationEventDeliveryService<AppraisalDbContext>>();
builder.Services.AddHostedService<IntegrationEventDeliveryService<DocumentDbContext>>();
builder.Services.AddHostedService<IntegrationEventDeliveryService<WorkflowDbContext>>();

builder.Services.AddMassTransit(config =>
{
    config.SetKebabCaseEndpointNameFormatter();

    config.AddConsumers(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, documentAssembly,
        collateralAssembly, appraisalAssembly, integrationAssembly, commonAssembly);
    config.AddSagaStateMachines(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, documentAssembly,
        collateralAssembly, appraisalAssembly, integrationAssembly);
    config.AddSagas(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, documentAssembly,
        collateralAssembly, appraisalAssembly, integrationAssembly);
    config.AddActivities(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, documentAssembly,
        collateralAssembly, appraisalAssembly, integrationAssembly);

    config.UsingRabbitMq((context, configurator) =>
    {
        configurator.Host(new Uri(builder.Configuration["RabbitMQ:Host"]!), host =>
        {
            host.Username(builder.Configuration["RabbitMQ:Username"]!);
            host.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        configurator.PrefetchCount = 16;
        configurator.ConfigureEndpoints(context);
        configurator.UseMessageRetry(r =>
        {
            r.Exponential(5,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(5));
            // Non-transient exceptions: data must change before retry can succeed.
            // Skip retries — go straight to dead-letter for ops triage.
            r.Ignore<Shared.Exceptions.ConflictException>();
            r.Ignore<Collateral.CollateralMasters.Exceptions.MissingIdentityKeyException>();
        });

        // Single partitioned endpoint for webhook ordering per appraisal.
        // WebhookDispatchConsumer is marked [ExcludeFromConfigureEndpoints] so ConfigureEndpoints
        // above does not create separate per-message queues for these two event types.
        configurator.ReceiveEndpoint("webhook-dispatch", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<WebhookDispatchConsumer>(context);
            e.UsePartitioner<AppraisalCreatedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
            e.UsePartitioner<AppraisalStatusChangedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
        });

        // In-memory outbox removed — using per-module persistent outbox via IntegrationEventDeliveryService
    });
});

builder.Services.AddHttpClient("CAS", client =>
{
    // In-process loopback calls (TokenHandler / RefreshTokenHandler) hit /connect/token via this client.
    // Behind an LB, AppBaseUrl is the public LB URL — but the TLS cert at that URL may not match the
    // hostname (RemoteCertificateNameMismatch) when traffic loops back to the local node. Allow ops to
    // point the loopback HttpClient at http(s)://localhost:<port> via Auth:InternalAuthBaseUrl while
    // keeping AppBaseUrl = LB URL for the OpenIddict issuer claim.
    var baseUrl = builder.Configuration["Auth:InternalAuthBaseUrl"]
                  ?? builder.Configuration["AppBaseUrl"]
                  ?? throw new InvalidOperationException(
                      "Neither Auth:InternalAuthBaseUrl nor AppBaseUrl is configured in appsettings.");
    client.BaseAddress = new Uri(baseUrl);
});

// Module services: request, etc.
builder.Services
    .AddRequestModule(builder.Configuration)
    .AddAuthModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddParameterModule(builder.Configuration)
    .AddDocumentModule(builder.Configuration)
    .AddWorkflowModule(builder.Configuration)
    .AddCollateralModule(builder.Configuration)
    .AddAppraisalModule(builder.Configuration)
    .AddIntegrationModule(builder.Configuration)
    .AddCommonModule(builder.Configuration)
    .AddReportingModule(builder.Configuration);

// Shared Data Protection keyring (persisted via AuthDbContext) — required when running behind a
// load balancer so antiforgery cookies and OpenIddict reference tokens issued on one node can be
// read by the others. MUST come after AddAuthModule so AuthDbContext is registered.
builder.Services.AddSharedDataProtection<AuthDbContext>();

// Reverse-proxy / load-balancer headers (IIS ARR, Nginx, etc.).
// Allows OpenIddict discovery + HTTPS redirection to reflect the public scheme/host.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust proxies within the deployment network. Tighten via KnownProxies/KnownNetworks
    // (configured per-environment) once the LB IPs are pinned.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Observability: OpenTelemetry metrics + tracing
builder.Services.AddObservability(builder.Configuration, builder.Environment);

builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("Database")!,
        name: "sqlserver",
        tags: ["db", "ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        "redis",
        tags: ["cache", "ready"])
    .AddRabbitMQ(
        name: "rabbitmq",
        tags: ["messaging", "ready"]);

var corsConfig = builder.Configuration
    .GetSection(CorsConfiguration.SectionName)
    .Get<CorsConfiguration>() ?? new CorsConfiguration();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SPAPolicy",
        policy =>
        {
            policy
                .WithOrigins(corsConfig.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for SignalR
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// OpenAPI document (/openapi/v1.json) + Scalar API reference UI (/scalar) are exposed in
// all environments so other systems can consume the API contract as documentation.
// NOTE: these endpoints are unauthenticated — restrict the /openapi and /scalar paths at the
// reverse proxy (F5/IIS) if the API surface should not be publicly reachable.
// AllowAnonymous bypasses the global fallback authorization policy (RequireAuthenticatedUser).
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(options => options.WithTitle("Collateral Appraisal System API"))
    .AllowAnonymous();
//if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

// Must be first: rewrites Request.Scheme / RemoteIp from X-Forwarded-* before any downstream
// middleware (HTTPS redirection, OpenIddict discovery, authn) makes scheme-dependent decisions.
app.UseForwardedHeaders();

app.UseHttpsRedirection();

// Configure static file serving based on storage mode
var fileStorageConfig = app.Configuration
    .GetSection(FileStorageConfiguration.SectionName)
    .Get<FileStorageConfiguration>();

if (fileStorageConfig?.Mode == StorageMode.Nas
    && !string.IsNullOrEmpty(fileStorageConfig.NasBasePath))
{
    if (Directory.Exists(fileStorageConfig.NasBasePath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(fileStorageConfig.NasBasePath),
            RequestPath = ""
        });
        Log.Information("Static files served from NAS: {NasBasePath}", fileStorageConfig.NasBasePath);
    }
    else
    {
        Log.Warning("NAS path is not accessible: {NasBasePath}. Falling back to local storage.",
            fileStorageConfig.NasBasePath);
        app.UseStaticFiles();
    }
}
else
{
    app.UseStaticFiles();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "Assets")),
    RequestPath = "/Assets"
});

app.UseCors("SPAPolicy");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(options => { });

// Add health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString(),
                data = x.Value.Data
            }),
            totalDuration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString(),
                data = x.Value.Data
            }),
            totalDuration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}).AllowAnonymous();

// Prometheus metrics scrape endpoint
app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapCarter();

app
    .UseRequestModule()
    .UseAuthModule()
    .UseNotificationModule()
    .UseParameterModule()
    .UseDocumentModule()
    .UseWorkflowModule()
    .UseCollateralModule()
    .UseAppraisalModule()
    .UseIntegrationModule()
    .UseCommonModule()
    .UseReportingModule();

app.UseHangfire();

// All recurring jobs run in the application's configured timezone (appsettings: TimeZone:DefaultTimeZone),
// not UTC. Cron hour values below are therefore local hours.
var appTimeZone = app.Services.GetRequiredService<IDateTimeProvider>().ApplicationTimeZone;
var jobOptions = new RecurringJobOptions { TimeZone = appTimeZone };

// Outbox cleanup: purge processed/dead-letter messages older than 7 days, daily at 02:00 local
RecurringJob.AddOrUpdate<OutboxCleanupJob<RequestDbContext>>(
    "outbox-cleanup-request", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2), jobOptions);
RecurringJob.AddOrUpdate<OutboxCleanupJob<AppraisalDbContext>>(
    "outbox-cleanup-appraisal", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2), jobOptions);
RecurringJob.AddOrUpdate<OutboxCleanupJob<DocumentDbContext>>(
    "outbox-cleanup-document", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2), jobOptions);
RecurringJob.AddOrUpdate<OutboxCleanupJob<WorkflowDbContext>>(
    "outbox-cleanup-workflow", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2), jobOptions);

// Logs cleanup: purge dbo.Logs rows older than 30 days, daily at 03:00 local
RecurringJob.AddOrUpdate<LogsCleanupJob>(
    "logs-cleanup", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(3), jobOptions);

// Reappraisal (AS400): ingest AS400 COLLATREV files monthly, 1st of month at 01:00 local.
// TODO(confirm): if the file arrives on a fixed day other than the 1st, adjust the cron.
// In dev, trigger manually via Hangfire dashboard (/hangfire → "Trigger now").
RecurringJob.AddOrUpdate<As400ReappraisalJob>(
    "reappraisal-as400", j => j.ExecuteAsync(CancellationToken.None),
    Cron.Monthly(1, 1), jobOptions); // 1st of each month at 01:00 local

// Reappraisal (block): daily at 01:00 local.
// Scans collateral.ProjectDetails for block projects past their reappraisal interval and
// materialises collateral.BlockReappraisalDue for Phase C (due-list screen).
RecurringJob.AddOrUpdate<BlockReappraisalJob>(
    "reappraisal-block", j => j.ExecuteAsync(CancellationToken.None),
    Cron.Daily(1), jobOptions); // 01:00 local

await app.RunAsync();

public partial class Program
{
}