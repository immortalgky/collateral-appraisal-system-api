using Microsoft.Extensions.Options;
using Microsoft.Extensions.Resilience;
using Polly;
using Polly.Registry;
using Polly.CircuitBreaker;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;
using System.Net;
using System.Net.Sockets;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Configuration;
using Workflow.AssigneeSelection.Strategies;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Services;
using Workflow.Workflow.Hubs;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Versioning;
using Workflow.Workflow.Expressions;
using Workflow.Workflow.ImportExport;
using Workflow.Telemetry;
using Microsoft.Extensions.Hosting;

namespace Workflow;

public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services, IConfiguration configuration)
    {
        // PHASE 5: Add comprehensive telemetry services with health checks
        services.AddWorkflowTelemetryServices();
        
        // Register workflow telemetry health check
        services.AddScoped<WorkflowTelemetryHealthCheck>();

        // PHASE 2: Add structured logging services for comprehensive workflow observability
        services.AddScoped<IWorkflowLogger, WorkflowLogger>();

        // PHASE 3: Add metrics services for comprehensive workflow observability and monitoring
        services.AddScoped<IWorkflowMetrics, WorkflowMetrics>();

        // PHASE 4: Add distributed tracing services for workflow observability across services
        services.AddScoped<IWorkflowTracing, WorkflowTracing>();

        services.AddTransient<IAssignmentService, AssignmentService>();

        services.AddScoped<IAssignmentRepository, AssignmentRepository>();

        // User group and hashing services
        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IGroupHashService, GroupHashService>();

        // Assignee selector services
        services.AddScoped<ManualAssigneeSelector>();
        services.AddScoped<RoundRobinAssigneeSelector>();
        services.AddScoped<WorkloadBasedAssigneeSelector>();
        services.AddScoped<RandomAssigneeSelector>();
        services.AddScoped<SupervisorAssigneeSelector>();
        services.AddScoped<PreviousOwnerAssigneeSelector>();
        services.AddScoped<IAssigneeSelectorFactory, AssigneeSelectorFactory>();
        services.AddScoped<ICascadingAssignmentEngine, CascadingAssignmentEngine>();

        // Custom assignment services - extensible assignment logic
        services.AddCustomAssignmentServices()
            .AddService<ClientPreferenceAssignmentService>()
            .AddService<BusinessRulesAssignmentService>()
            .Build();

        // Mock supervisor configuration (remove when UserManagement is implemented)
        services.Configure<MockSupervisorOptions>(configuration.GetSection(MockSupervisorOptions.SectionName));
        services.PostConfigure<MockSupervisorOptions>(options => options.Validate());


        // Workflow services - new clean architecture
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowActivityExecutionRepository, WorkflowActivityExecutionRepository>();

        // Enhanced workflow repositories for transaction patterns
        services.AddScoped<IWorkflowBookmarkRepository, WorkflowBookmarkRepository>();
        services.AddScoped<IWorkflowExecutionLogRepository, WorkflowExecutionLogRepository>();
        services.AddScoped<IWorkflowOutboxRepository, WorkflowOutboxRepository>();

        // Enhanced workflow services for transaction patterns
        services.AddScoped<IWorkflowBookmarkService, WorkflowBookmarkService>();

        // Configure HTTP client for external calls
        services.AddHttpClient<ITwoPhaseExternalCallService, TwoPhaseExternalCallService>();

        // Fault handling services
        services.AddScoped<IWorkflowFaultHandler, WorkflowFaultHandler>();

        services.AddScoped<IWorkflowActivityFactory, WorkflowActivityFactory>();

        // NEW ARCHITECTURE: Specialized service components
        services.AddScoped<IFlowControlManager, FlowControlManager>();
        services.AddScoped<IWorkflowLifecycleManager, WorkflowLifecycleManager>();
        services.AddScoped<IWorkflowPersistenceService, WorkflowPersistenceService>();
        services.AddScoped<IWorkflowEventPublisher, WorkflowEventPublisher>();
        services.AddScoped<IWorkflowStateManager, WorkflowStateManager>();
        services.AddScoped<IWorkflowSchemaValidator, WorkflowSchemaValidator>();

        // Core execution engine - single-step atomic execution
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();

        // ENHANCED: Orchestrator for step-by-step execution following "one step = one transaction"
        services.AddScoped<IWorkflowOrchestrator, WorkflowOrchestrator>();

        // Service layer - thin coordination layer 
        services.AddScoped<IWorkflowService, WorkflowService>();

        services.AddScoped<IWorkflowNotificationService, WorkflowNotificationService>();
        services.AddScoped<IWorkflowAuditService, WorkflowAuditService>();
        services.AddScoped<IWorkflowResilienceService, WorkflowResilienceService>();
        services.AddScoped<ITaskConfigurationService, TaskConfigurationService>();

        // Workflow expression evaluator and action executor
        services.AddScoped<IWorkflowExpressionEvaluator, WorkflowExpressionEvaluator>();
        services.AddScoped<IWorkflowActionExecutor, WorkflowActionExecutor>();

        // NEW ENHANCED SERVICES - Phase 2-5 Restoration
        services.AddScoped<IWorkflowVersioningService, WorkflowVersioningService>();
        services.AddScoped<IWorkflowExpressionService, WorkflowExpressionService>();
        services.AddScoped<IWorkflowImportExportService, WorkflowImportExportService>();

        // Background services for workflow processing
        services.AddHostedService<OutboxDispatcherService>();
        services.AddHostedService<WorkflowTimerService>();
        services.AddHostedService<WorkflowCleanupService>();

        // Workflow activities
        services.AddScoped<HumanTaskActivity>();

        // Configure Workflow Resilience Options
        services.Configure<WorkflowResilienceOptions>(configuration.GetSection(WorkflowResilienceOptions.SectionName));

        // Configure resilience pipelines using Microsoft.Extensions.Resilience
        services.AddResiliencePipeline("workflow-retry", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<WorkflowResilienceOptions>>().Value;
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                Delay = options.Retry.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                MaxDelay = options.Retry.MaxDelay,
                UseJitter = true
            });
        });

        services.AddResiliencePipeline("workflow-database", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<WorkflowResilienceOptions>>().Value;
            builder
                .AddTimeout(options.Timeout.DatabaseOperation)
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<DbUpdateConcurrencyException>()
                });
        });

        services.AddResiliencePipeline("workflow-external", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<WorkflowResilienceOptions>>().Value;
            builder
                // Circuit breaker to protect against cascading failures
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5, // Open circuit if 50% of requests fail
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                    BreakDuration = options.CircuitBreaker.BreakDuration,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .Handle<SocketException>()
                })
                // Timeout for individual external calls
                .AddTimeout(options.Timeout.ExternalHttpCall)
                // Retry with exponential backoff and jitter
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    Delay = options.Retry.BaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    MaxDelay = options.Retry.MaxDelay,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .Handle<SocketException>()
                });
        });

        services.AddResiliencePipeline("workflow-activity", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<WorkflowResilienceOptions>>().Value;
            builder
                .AddTimeout(options.Timeout.ActivityExecution)
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts
                });
        });

        // Workflow DbContext with its own migration assembly and history table
        services.AddDbContext<WorkflowDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.GetName()
                    .Name); // Workflow assembly
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "workflow");
            });
        });

        // Saga DbContext with separate migration assembly and history table
        services.AddDbContext<AppraisalSagaDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppraisalSagaDbContext).Assembly.GetName()
                    .Name); // Separate saga assembly
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
            });
        });

        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry for the Workflow module. Should be called after AddWorkflowModule.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureWorkflowTelemetry(
        this IServiceCollection services, 
        IConfiguration configuration, 
        IHostEnvironment environment)
    {
        services.AddWorkflowTelemetry(configuration, environment);
        return services;
    }

    public static IApplicationBuilder UseWorkflowModule(this IApplicationBuilder app)
    {
        app.UseMigration<WorkflowDbContext>();
        app.UseMigration<AppraisalSagaDbContext>();

        // Configure SignalR workflow hub
        app.UseEndpoints(endpoints => { endpoints.MapHub<WorkflowHub>("/workflowHub"); });

        return app;
    }
}