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
using Collateral.Data;
using Scalar.AspNetCore;
using Dapper;
using Shared.Configurations;
using Shared.Data;
using Shared.Data.Dapper;
using Shared.Data.Outbox;
using Shared.Logging;
using Shared.Security;
using Integration.Application.EventHandlers.Outbound;
using Appraisal.Application.EventHandlers;
using Common.Application.EventHandlers;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Messaging.Services;
using Workflow.Data;
using Workflow.EventHandlers;
using Shared.Observability;
using Auth.Infrastructure.HealthChecks;
using Notification.Infrastructure.Email.HealthChecks;
using Integration.Infrastructure.HealthChecks;

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
builder.Services.AddHostedService<IntegrationEventDeliveryService<CollateralDbContext>>();
builder.Services.AddHostedService<IntegrationEventDeliveryService<Reporting.Data.ReportingDbContext>>();

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
        // Partitioned by AppraisalId so per-appraisal ordering holds on the single consuming node.
        // Each app node has its own RabbitMQ broker (no clustering) → no competing consumers, so the
        // in-process partitioner alone is sufficient; no SingleActiveConsumer needed.
        configurator.ReceiveEndpoint("webhook-dispatch", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<WebhookDispatchConsumer>(context);
            e.UsePartitioner<AppraisalCreatedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
            e.UsePartitioner<AppraisalStatusChangedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
        });

        // ---------------------------------------------------------------------
        // Ordering-critical endpoints. Each app node has its own RabbitMQ broker (no clustering),
        // so every queue is drained by a single consumer — an in-process partitioner is sufficient.
        // It serializes messages per AppraisalId (parallel across appraisals) even though the node
        // processes ConcurrentMessageLimit (= PrefetchCount = 16) in parallel. Each consumer is
        // marked [ExcludeFromConfigureEndpoints] so ConfigureEndpoints does not also auto-create a
        // default (unordered) queue for it.
        // ---------------------------------------------------------------------

        // #2 External cycle tracking — close-before-open must not silently no-op and
        // corrupt cycle counts / SLA business-minutes.
        configurator.ReceiveEndpoint("appraisal-ext-cycle", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<ExternalCycleTrackingHandler>(context);
            e.UsePartitioner<WorkflowTransitionedIntegrationEvent>(
                partitioner, m => m.Message.AppraisalId ?? m.Message.WorkflowInstanceId);
        });

        // #3 Appraisal sync — all events that mutate the appraisal + its active AppraisalAssignment
        // row are co-located on ONE partitioned endpoint so they serialize per appraisal:
        //   - WorkflowTransitioned: appraisal status sync + assignment-status transitions + the
        //     assignment-level SLADueDate stamp. (Out-of-order transitions must not overwrite the
        //     authoritative Appraisals.Status with a stale value — preserved by per-AppraisalId order.)
        //   - CompanyAssigned / InternalAssigned / InternalFollowupAssigned: the engagement events that
        //     mutate the SAME AppraisalAssignment row (eliminates the cross-type field-clobber race).
        // Co-locating WorkflowTransitioned with CompanyAssigned also fixes the stamp race: CompanyAssigned
        // is staged in the outbox before the transition (earlier OccurredAt), so it is delivered first and,
        // serialized here, sets the assignment to Assigned before the transition handler stamps SLADueDate
        // at the window's start activity (otherwise the Pending guard would skip the stamp).
        configurator.ReceiveEndpoint("appraisal-sync", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<WorkflowTransitionedIntegrationEventHandler>(context);
            e.ConfigureConsumer<CompanyAssignedIntegrationEventHandler>(context);
            e.ConfigureConsumer<InternalAssignedIntegrationEventHandler>(context);
            e.ConfigureConsumer<InternalFollowupAssignedIntegrationEventHandler>(context);
            e.UsePartitioner<WorkflowTransitionedIntegrationEvent>(
                partitioner, m => m.Message.AppraisalId ?? m.Message.WorkflowInstanceId);
            e.UsePartitioner<CompanyAssignedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
            e.UsePartitioner<InternalAssignedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
            e.UsePartitioner<InternalFollowupAssignedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
        });

        // #4 Dashboard status counter — the decrement-old/increment-new bucket move is not
        // commutative; serialize per appraisal so out-of-order transitions don't drift counts.
        configurator.ReceiveEndpoint("appraisal-status-dashboard", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<AppraisalStatusChangedDashboardHandler>(context);
            e.UsePartitioner<AppraisalStatusChangedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
        });

        // #5 Assignment SLA recalculation — unconditionally re-stamps AppraisalAssignment.SLADueDate
        // whenever an appointment-anchored group-window deadline shifts due to a reschedule.
        // Partitioned by AppraisalId so per-appraisal ordering holds.
        configurator.ReceiveEndpoint("appraisal-sla-recalc", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<AssignmentSlaRecalculatedIntegrationEventConsumer>(context);
            e.UsePartitioner<AssignmentSlaRecalculatedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
        });

        // #6 Appointment date changed — two concurrent reschedule events for the same appraisal
        // must not publish the stale SLA recalculation last. Partitioned by AppraisalId so
        // per-appraisal ordering holds. Consumer is [ExcludeFromConfigureEndpoints] to prevent
        // ConfigureEndpoints from also creating an unordered auto-queue.
        configurator.ReceiveEndpoint("workflow-appointment-changed", e =>
        {
            var partitioner = e.CreatePartitioner(16);
            e.ConfigureConsumer<AppointmentDateChangedIntegrationEventConsumer>(context);
            e.UsePartitioner<AppointmentDateChangedIntegrationEvent>(partitioner, m => m.Message.AppraisalId);
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
builder.Services.AddSharedDataProtection<AuthDbContext>(builder.Configuration);

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
    // RabbitMQ messaging readiness is covered by MassTransit's own "masstransit-bus" health check
    // (auto-registered by AddMassTransit, tagged "ready"), which probes the real bus connection.
    // The AspNetCore.HealthChecks.RabbitMQ check is not used: its v9 overload resolves a
    // RabbitMQ.Client.IConnection from DI, which MassTransit does not register.
    // External integrations (LDAP / SMTP / SFTP). Tagged "external", NOT "ready": an outage here
    // surfaces in /health and /health/external but must not pull a node out of LB rotation.
    .AddAuthHealthChecks()
    .AddNotificationHealthChecks()
    .AddIntegrationHealthChecks();

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

// External-dependency probes (LDAP / SMTP / SFTP) only. Lets ops poll the integration links
// without the DB/cache/bus noise — and keeps them out of /health/ready (LB rotation).
app.MapHealthChecks("/health/external", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("external"),
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

// Recurring jobs are registered per-module inside each UseXModule() (see UseModuleRecurringJobs<T>),
// each reading its own {schema}.JobSchedules table. Nothing to register centrally here.

await app.RunAsync();

public partial class Program
{
}