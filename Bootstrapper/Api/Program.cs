using Appraisal;
using Appraisal.Infrastructure;
using Document.Data;
using Common;
using Hangfire;
using Integration.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Request.Infrastructure;
using Shared.Configurations;
using Shared.Data;
using Shared.Data.Outbox;
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

builder.Services.AddCarterWithAssemblies(apiAssembly, requestAssembly, authAssembly, notificationAssembly,
    parameterAssembly, documentAssembly, workflowAssembly, collateralAssembly, appraisalAssembly, integrationAssembly,
    commonAssembly);
builder.Services.AddMediatRWithAssemblies(apiAssembly, requestAssembly, authAssembly, notificationAssembly,
    parameterAssembly, documentAssembly, workflowAssembly, collateralAssembly, appraisalAssembly, integrationAssembly,
    commonAssembly);

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
        configurator.UseMessageRetry(r => r.Exponential(5,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)));

        // In-memory outbox removed — using per-module persistent outbox via IntegrationEventDeliveryService
    });
});

builder.Services.AddHttpClient("CAS", client =>
{
    var baseUrl = builder.Configuration["AppBaseUrl"]
                  ?? throw new InvalidOperationException("AppBaseUrl is not configured in appsettings.");
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
    .AddCommonModule(builder.Configuration);

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
        name: "redis",
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
if (app.Environment.IsDevelopment()) app.MapOpenApi();
//if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

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
        Log.Warning("NAS path is not accessible: {NasBasePath}. Falling back to local storage.", fileStorageConfig.NasBasePath);
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
app.UseSerilogRequestLogging();
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

app.UseHangfire();

// Outbox cleanup: purge processed/dead-letter messages older than 7 days, daily at 2 AM UTC
RecurringJob.AddOrUpdate<OutboxCleanupJob<RequestDbContext>>(
    "outbox-cleanup-request", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2));
RecurringJob.AddOrUpdate<OutboxCleanupJob<AppraisalDbContext>>(
    "outbox-cleanup-appraisal", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2));
RecurringJob.AddOrUpdate<OutboxCleanupJob<DocumentDbContext>>(
    "outbox-cleanup-document", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2));
RecurringJob.AddOrUpdate<OutboxCleanupJob<WorkflowDbContext>>(
    "outbox-cleanup-workflow", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2));

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
    .UseCommonModule();

await app.RunAsync();

public partial class Program
{
}