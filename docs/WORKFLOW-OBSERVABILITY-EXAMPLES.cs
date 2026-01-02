/*
 * Workflow Engine Observability Code Examples
 * 
 * This file contains standalone, copy-paste ready examples for implementing
 * comprehensive observability in workflow engine components.
 * 
 * Examples include:
 * - Custom activities with full telemetry
 * - Service implementations with observability
 * - Configuration setups
 * - Health check implementations
 * - Integration test examples
 * - Performance monitoring patterns
 * 
 * All examples follow best practices and are production-ready.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#region 1. Complete Custom Activity with Full Telemetry

/// <summary>
/// Example of a custom workflow activity with comprehensive observability.
/// This demonstrates the complete pattern for logging, metrics, and tracing.
/// </summary>
public class CompleteObservableActivity : WorkflowActivityBase
{
    private readonly IWorkflowLogger _logger;
    private readonly IWorkflowMetrics _metrics;
    private readonly IWorkflowTracing _tracing;
    private readonly IBusinessService _businessService;

    public CompleteObservableActivity(
        IWorkflowLogger logger,
        IWorkflowMetrics metrics,
        IWorkflowTracing tracing,
        IBusinessService businessService)
    {
        _logger = logger;
        _metrics = metrics;
        _tracing = tracing;
        _businessService = businessService;
    }

    public override async Task<ActivityResult> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var activityId = "business-processing";
        var activityType = nameof(CompleteObservableActivity);
        
        // 1. Create correlation scope for structured logging
        using var logScope = _logger.CreateActivityCorrelationScope(
            context.WorkflowInstance.Id,
            activityId,
            activityType,
            context.CorrelationId);

        // 2. Create distributed tracing span
        using var span = _tracing.CreateActivitySpan(
            activityId,
            activityType,
            context.WorkflowInstance.Id,
            Guid.NewGuid());

        // 3. Enrich span with business context
        span
            .SetWorkflowDefinitionId(context.WorkflowDefinition.Id)
            .SetCorrelationId(context.CorrelationId)
            .SetAttribute("business.entity_type", context.Variables.GetValueOrDefault("EntityType"))
            .SetAttribute("business.entity_id", context.Variables.GetValueOrDefault("EntityId"))
            .SetAttribute("business.priority", context.Variables.GetValueOrDefault("Priority"))
            .SetAttribute("activity.retry_count", context.RetryCount)
            .SetAttribute("activity.execution_path", GetExecutionPath(context));

        try
        {
            // 4. Log activity start with context
            _logger.LogActivityStarting(activityId, activityType, context.WorkflowInstance.Id);
            _metrics.RecordActivityStarted(activityType, activityId, "BusinessWorkflow");

            span.AddEvent("activity.validation.start");
            
            // 5. Input validation with observability
            var validationResult = await ValidateInputWithTelemetryAsync(context, span, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errorMessage = $"Validation failed: {string.Join(", ", validationResult.Errors)}";
                
                span.AddEvent("activity.validation.failed", new Dictionary<string, object>
                {
                    ["error_count"] = validationResult.Errors.Count,
                    ["error_types"] = string.Join(",", validationResult.Errors.Select(e => e.Type))
                });

                _logger.LogActivityFailed(activityId, activityType, context.WorkflowInstance.Id, errorMessage);
                _metrics.RecordActivityDuration(activityType, activityId, "BusinessWorkflow", stopwatch.Elapsed, "ValidationFailed");
                
                span.Fail(errorMessage);
                return ActivityResult.Failed(errorMessage);
            }

            span.AddEvent("activity.validation.succeeded");
            span.AddEvent("activity.business_processing.start");

            // 6. Main business processing with sub-span
            var processingResult = await ProcessBusinessLogicWithTelemetryAsync(context, span, cancellationToken);
            
            stopwatch.Stop();

            // 7. Record success metrics and logs
            _metrics.RecordActivityCompleted(activityType, activityId, "BusinessWorkflow", "Success");
            _metrics.RecordActivityDuration(activityType, activityId, "BusinessWorkflow", stopwatch.Elapsed, "Success");
            _logger.LogActivityCompleted(activityId, activityType, context.WorkflowInstance.Id, stopwatch.Elapsed, ActivityResultStatus.Success);

            // 8. Enrich span with results
            span
                .SetAttribute("business.result_count", processingResult.ProcessedItems)
                .SetAttribute("business.duration_ms", stopwatch.Elapsed.TotalMilliseconds)
                .AddEvent("activity.completed", new Dictionary<string, object>
                {
                    ["processed_items"] = processingResult.ProcessedItems,
                    ["output_variables"] = processingResult.OutputVariables?.Count ?? 0,
                    ["performance_tier"] = ClassifyPerformance(stopwatch.Elapsed)
                });

            span.Complete();
            return ActivityResult.Success(processingResult.OutputVariables);
        }
        catch (BusinessValidationException ex)
        {
            stopwatch.Stop();
            return await HandleBusinessExceptionWithTelemetryAsync(ex, activityId, activityType, context, span, stopwatch.Elapsed);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            return await HandleTimeoutExceptionWithTelemetryAsync(ex, activityId, activityType, context, span, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return await HandleGenericExceptionWithTelemetryAsync(ex, activityId, activityType, context, span, stopwatch.Elapsed);
        }
    }

    private async Task<ValidationResult> ValidateInputWithTelemetryAsync(
        WorkflowExecutionContext context, 
        IWorkflowSpan parentSpan, 
        CancellationToken cancellationToken)
    {
        using var validationSpan = _tracing.CreateActivitySpan(
            "input-validation",
            "ValidationActivity",
            context.WorkflowInstance.Id,
            Guid.NewGuid());

        validationSpan
            .SetAttribute("validation.input_count", context.Variables.Count)
            .SetAttribute("validation.required_fields", "EntityId,EntityType,Priority");

        var errors = new List<ValidationError>();
        
        // Required field validation
        if (!context.Variables.ContainsKey("EntityId"))
        {
            errors.Add(new ValidationError("EntityId", "Required"));
            validationSpan.AddEvent("validation.missing_field", new Dictionary<string, object> { ["field"] = "EntityId" });
        }

        if (!context.Variables.ContainsKey("EntityType"))
        {
            errors.Add(new ValidationError("EntityType", "Required"));
            validationSpan.AddEvent("validation.missing_field", new Dictionary<string, object> { ["field"] = "EntityType" });
        }

        // Business rule validation
        if (context.Variables.TryGetValue("Priority", out var priority) && 
            !IsValidPriority(priority?.ToString()))
        {
            errors.Add(new ValidationError("Priority", "Invalid value"));
            validationSpan.AddEvent("validation.invalid_value", new Dictionary<string, object> 
            { 
                ["field"] = "Priority", 
                ["value"] = priority?.ToString() ?? "null" 
            });
        }

        var result = new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };

        validationSpan
            .SetAttribute("validation.result", result.IsValid ? "success" : "failed")
            .SetAttribute("validation.error_count", errors.Count);

        if (result.IsValid)
            validationSpan.Complete();
        else
            validationSpan.Fail($"Validation failed with {errors.Count} errors");

        return result;
    }

    private async Task<ProcessingResult> ProcessBusinessLogicWithTelemetryAsync(
        WorkflowExecutionContext context,
        IWorkflowSpan parentSpan,
        CancellationToken cancellationToken)
    {
        using var processingSpan = _tracing.CreateActivitySpan(
            "business-processing",
            "ProcessingActivity",
            context.WorkflowInstance.Id,
            Guid.NewGuid());

        var entityId = context.Variables["EntityId"]?.ToString();
        var entityType = context.Variables["EntityType"]?.ToString();

        processingSpan
            .SetAttribute("business.entity_id", entityId)
            .SetAttribute("business.entity_type", entityType)
            .SetAttribute("business.operation", "process_entity");

        try
        {
            var result = await _businessService.ProcessEntityAsync(
                entityId,
                entityType,
                context.Variables,
                cancellationToken);

            processingSpan
                .SetAttribute("business.processing_duration_ms", result.ProcessingTime.TotalMilliseconds)
                .SetAttribute("business.items_processed", result.ProcessedItems)
                .AddEvent("business.processing.completed", new Dictionary<string, object>
                {
                    ["entity_type"] = entityType,
                    ["processing_time_ms"] = result.ProcessingTime.TotalMilliseconds,
                    ["items_processed"] = result.ProcessedItems
                });

            processingSpan.Complete();
            return result;
        }
        catch (Exception ex)
        {
            processingSpan
                .RecordException(ex)
                .SetAttribute("error.business_operation", "process_entity")
                .Fail("Business processing failed", ex);
            throw;
        }
    }

    private async Task<ActivityResult> HandleBusinessExceptionWithTelemetryAsync(
        BusinessValidationException ex,
        string activityId,
        string activityType,
        WorkflowExecutionContext context,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogActivityFailed(activityId, activityType, context.WorkflowInstance.Id, ex.Message, ex);
        _metrics.RecordActivityDuration(activityType, activityId, "BusinessWorkflow", elapsed, "BusinessValidationFailed");

        span
            .RecordException(ex)
            .SetAttribute("error.type", "business_validation")
            .SetAttribute("error.validation_type", ex.ValidationType)
            .SetAttribute("error.field", ex.FieldName)
            .AddEvent("activity.business_validation_failed", new Dictionary<string, object>
            {
                ["validation_type"] = ex.ValidationType,
                ["field_name"] = ex.FieldName,
                ["error_code"] = ex.ErrorCode,
                ["is_retriable"] = ex.IsRetriable
            })
            .Fail("Business validation failed", ex);

        return ex.IsRetriable 
            ? ActivityResult.Retry(ex.Message, TimeSpan.FromMinutes(5))
            : ActivityResult.Failed(ex.Message);
    }

    private async Task<ActivityResult> HandleTimeoutExceptionWithTelemetryAsync(
        TimeoutException ex,
        string activityId,
        string activityType,
        WorkflowExecutionContext context,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogActivityFailed(activityId, activityType, context.WorkflowInstance.Id, "Operation timed out", ex);
        _metrics.RecordActivityDuration(activityType, activityId, "BusinessWorkflow", elapsed, "Timeout");

        span
            .RecordException(ex)
            .SetAttribute("error.type", "timeout")
            .SetAttribute("error.timeout_duration_ms", elapsed.TotalMilliseconds)
            .AddEvent("activity.timeout", new Dictionary<string, object>
            {
                ["operation_duration_ms"] = elapsed.TotalMilliseconds,
                ["timeout_threshold_ms"] = 30000, // Example threshold
                ["retry_recommended"] = true
            })
            .Fail("Operation timed out", ex);

        return ActivityResult.Retry("Operation timed out, retrying", TimeSpan.FromMinutes(2));
    }

    private async Task<ActivityResult> HandleGenericExceptionWithTelemetryAsync(
        Exception ex,
        string activityId,
        string activityType,
        WorkflowExecutionContext context,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogActivityFailed(activityId, activityType, context.WorkflowInstance.Id, ex.Message, ex);
        _metrics.RecordActivityDuration(activityType, activityId, "BusinessWorkflow", elapsed, "Failed");

        span
            .RecordException(ex)
            .SetAttribute("error.type", ex.GetType().Name)
            .SetAttribute("error.is_retriable", IsRetriableException(ex))
            .AddEvent("activity.error", new Dictionary<string, object>
            {
                ["exception_type"] = ex.GetType().Name,
                ["is_retriable"] = IsRetriableException(ex),
                ["has_inner_exception"] = ex.InnerException != null
            })
            .Fail("Activity execution failed", ex);

        throw; // Re-throw to trigger workflow error handling
    }

    private static string GetExecutionPath(WorkflowExecutionContext context)
    {
        // Example: Generate execution path based on previous activities
        return context.ExecutionHistory?.LastOrDefault()?.ActivityName ?? "start";
    }

    private static string ClassifyPerformance(TimeSpan duration)
    {
        return duration.TotalSeconds switch
        {
            < 1 => "fast",
            < 5 => "normal",
            < 30 => "slow",
            _ => "very_slow"
        };
    }

    private static bool IsValidPriority(string priority) => 
        new[] { "low", "normal", "high", "urgent" }.Contains(priority?.ToLower());

    private static bool IsRetriableException(Exception ex) =>
        ex is not (BusinessValidationException or ArgumentException or InvalidOperationException);
}

#endregion

#region 2. Observable Service Implementation

/// <summary>
/// Example of a service class with comprehensive observability patterns.
/// Demonstrates correlation context propagation and cross-cutting concerns.
/// </summary>
public class ObservableWorkflowService : IWorkflowService
{
    private readonly IWorkflowLogger _logger;
    private readonly IWorkflowMetrics _metrics;
    private readonly IWorkflowTracing _tracing;
    private readonly IWorkflowEngine _engine;
    private readonly IWorkflowRepository _repository;

    public ObservableWorkflowService(
        IWorkflowLogger logger,
        IWorkflowMetrics metrics,
        IWorkflowTracing tracing,
        IWorkflowEngine engine,
        IWorkflowRepository repository)
    {
        _logger = logger;
        _metrics = metrics;
        _tracing = tracing;
        _engine = engine;
        _repository = repository;
    }

    public async Task<WorkflowResult> StartWorkflowAsync(StartWorkflowRequest request)
    {
        // Create workflow-level span
        using var workflowSpan = _tracing.CreateWorkflowSpan(
            "workflow.service.start",
            request.WorkflowInstanceId,
            request.WorkflowDefinitionId,
            request.CorrelationId);

        workflowSpan
            .SetAttribute("service.method", "StartWorkflowAsync")
            .SetAttribute("workflow.name", request.InstanceName)
            .SetAttribute("workflow.started_by", request.StartedBy)
            .SetAttribute("workflow.initial_variables_count", request.InitialVariables?.Count ?? 0)
            .SetAttribute("workflow.auto_start", request.AutoStart);

        // Create correlation scope for all operations
        using var correlationScope = _logger.CreateCorrelationScope(
            request.WorkflowInstanceId,
            request.CorrelationId,
            "StartWorkflow");

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogWorkflowStarting(
                request.WorkflowDefinitionId,
                request.InstanceName,
                request.StartedBy,
                request.CorrelationId);

            _metrics.RecordWorkflowStarted(
                "GenericWorkflow",
                request.WorkflowDefinitionId.ToString(),
                new KeyValuePair<string, object?>("started_by", request.StartedBy),
                new KeyValuePair<string, object?>("auto_start", request.AutoStart));

            workflowSpan.AddEvent("workflow.validation.start");

            // Pre-flight validation with observability
            await ValidateWorkflowRequestAsync(request, workflowSpan);

            workflowSpan.AddEvent("workflow.engine.start");

            // Start workflow with engine
            var result = await _engine.StartAsync(request);
            
            stopwatch.Stop();

            // Record success metrics
            _metrics.RecordWorkflowCompleted(
                "GenericWorkflow",
                request.WorkflowDefinitionId.ToString(),
                "Started");

            _metrics.RecordWorkflowDuration(
                "GenericWorkflow",
                request.WorkflowDefinitionId.ToString(),
                stopwatch.Elapsed,
                "Started",
                new KeyValuePair<string, object?>("initial_activity_count", result.InitialActivityCount));

            _logger.LogWorkflowStarted(result.WorkflowInstance);

            // Enrich span with results
            workflowSpan
                .SetAttribute("workflow.execution_id", result.ExecutionId)
                .SetAttribute("workflow.initial_activity_count", result.InitialActivityCount)
                .SetAttribute("workflow.duration_ms", stopwatch.Elapsed.TotalMilliseconds)
                .AddEvent("workflow.started", new Dictionary<string, object>
                {
                    ["execution_id"] = result.ExecutionId,
                    ["initial_activities"] = result.InitialActivityCount,
                    ["startup_duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
                })
                .Complete();

            return result;
        }
        catch (WorkflowValidationException ex)
        {
            return await HandleWorkflowValidationExceptionAsync(ex, request, workflowSpan, stopwatch.Elapsed);
        }
        catch (WorkflowNotFoundException ex)
        {
            return await HandleWorkflowNotFoundExceptionAsync(ex, request, workflowSpan, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return await HandleGenericWorkflowExceptionAsync(ex, request, workflowSpan, stopwatch.Elapsed);
        }
    }

    public async Task<WorkflowResult> ResumeWorkflowAsync(ResumeWorkflowRequest request)
    {
        using var span = _tracing.CreateWorkflowSpan(
            "workflow.service.resume",
            request.WorkflowInstanceId,
            Guid.Empty, // Definition ID retrieved from context
            request.CorrelationId);

        span
            .SetAttribute("service.method", "ResumeWorkflowAsync")
            .SetAttribute("workflow.bookmark_name", request.BookmarkName)
            .SetAttribute("workflow.trigger_data_size", request.TriggerData?.Count ?? 0);

        using var correlationScope = _logger.CreateCorrelationScope(
            request.WorkflowInstanceId,
            request.CorrelationId,
            "ResumeWorkflow");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Resuming workflow {WorkflowInstanceId} at bookmark {BookmarkName}",
                request.WorkflowInstanceId,
                request.BookmarkName);

            // Retrieve workflow context for telemetry enrichment
            var workflowInstance = await _repository.GetByIdAsync(request.WorkflowInstanceId);
            if (workflowInstance == null)
            {
                throw new WorkflowNotFoundException($"Workflow instance {request.WorkflowInstanceId} not found");
            }

            span.SetWorkflowDefinitionId(workflowInstance.WorkflowDefinitionId);

            _metrics.RecordWorkflowStarted(
                "GenericWorkflow",
                workflowInstance.WorkflowDefinitionId.ToString(),
                new KeyValuePair<string, object?>("operation", "resume"),
                new KeyValuePair<string, object?>("bookmark", request.BookmarkName));

            var result = await _engine.ResumeAsync(request);
            stopwatch.Stop();

            _metrics.RecordWorkflowDuration(
                "GenericWorkflow",
                workflowInstance.WorkflowDefinitionId.ToString(),
                stopwatch.Elapsed,
                "Resumed");

            _logger.LogInformation(
                "Workflow {WorkflowInstanceId} resumed successfully from bookmark {BookmarkName}",
                request.WorkflowInstanceId,
                request.BookmarkName);

            span
                .SetAttribute("workflow.resume_duration_ms", stopwatch.Elapsed.TotalMilliseconds)
                .AddEvent("workflow.resumed", new Dictionary<string, object>
                {
                    ["bookmark_name"] = request.BookmarkName,
                    ["resume_duration_ms"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["activities_resumed"] = result.ActivitiesResumed
                })
                .Complete();

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Failed to resume workflow {WorkflowInstanceId} at bookmark {BookmarkName}: {ErrorMessage}",
                request.WorkflowInstanceId,
                request.BookmarkName,
                ex.Message);

            span
                .RecordException(ex)
                .SetAttribute("workflow.resume_duration_ms", stopwatch.Elapsed.TotalMilliseconds)
                .Fail("Workflow resume failed", ex);

            throw;
        }
    }

    private async Task<ValidationResult> ValidateWorkflowRequestAsync(
        StartWorkflowRequest request,
        IWorkflowSpan parentSpan)
    {
        using var validationSpan = _tracing.CreateActivitySpan(
            "workflow-request-validation",
            "ValidationActivity",
            request.WorkflowInstanceId,
            Guid.NewGuid());

        validationSpan
            .SetAttribute("validation.request_type", "start_workflow")
            .SetAttribute("validation.workflow_definition_id", request.WorkflowDefinitionId.ToString());

        var errors = new List<string>();

        if (request.WorkflowDefinitionId == Guid.Empty)
        {
            errors.Add("WorkflowDefinitionId is required");
            validationSpan.AddEvent("validation.error", new Dictionary<string, object>
            {
                ["field"] = "WorkflowDefinitionId",
                ["error"] = "Required field missing"
            });
        }

        if (string.IsNullOrWhiteSpace(request.InstanceName))
        {
            errors.Add("InstanceName is required");
            validationSpan.AddEvent("validation.error", new Dictionary<string, object>
            {
                ["field"] = "InstanceName", 
                ["error"] = "Required field missing"
            });
        }

        // Check if workflow definition exists
        var definitionExists = await _repository.WorkflowDefinitionExistsAsync(request.WorkflowDefinitionId);
        if (!definitionExists)
        {
            errors.Add($"Workflow definition {request.WorkflowDefinitionId} not found");
            validationSpan.AddEvent("validation.error", new Dictionary<string, object>
            {
                ["field"] = "WorkflowDefinitionId",
                ["error"] = "Workflow definition not found",
                ["definition_id"] = request.WorkflowDefinitionId.ToString()
            });
        }

        var isValid = !errors.Any();
        
        validationSpan
            .SetAttribute("validation.result", isValid ? "success" : "failed")
            .SetAttribute("validation.error_count", errors.Count);

        if (isValid)
        {
            validationSpan.Complete();
        }
        else
        {
            validationSpan.Fail($"Request validation failed: {string.Join(", ", errors)}");
            throw new WorkflowValidationException(string.Join("; ", errors));
        }

        return new ValidationResult { IsValid = isValid, Errors = errors.Select(e => new ValidationError("Request", e)).ToList() };
    }

    private async Task<WorkflowResult> HandleWorkflowValidationExceptionAsync(
        WorkflowValidationException ex,
        StartWorkflowRequest request,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogError(ex,
            "Workflow validation failed for {WorkflowDefinitionId}: {ErrorMessage}",
            request.WorkflowDefinitionId,
            ex.Message);

        _metrics.RecordWorkflowCompleted(
            "GenericWorkflow",
            request.WorkflowDefinitionId.ToString(),
            "ValidationFailed");

        span
            .RecordException(ex)
            .SetAttribute("error.type", "workflow_validation")
            .SetAttribute("error.validation_errors", ex.ValidationErrors?.Count ?? 0)
            .SetAttribute("workflow.duration_ms", elapsed.TotalMilliseconds)
            .AddEvent("workflow.validation_failed", new Dictionary<string, object>
            {
                ["validation_errors"] = ex.ValidationErrors?.Count ?? 0,
                ["error_message"] = ex.Message
            })
            .Fail("Workflow validation failed", ex);

        throw; // Re-throw validation exceptions as they shouldn't be swallowed
    }

    private async Task<WorkflowResult> HandleWorkflowNotFoundExceptionAsync(
        WorkflowNotFoundException ex,
        StartWorkflowRequest request,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogError(ex,
            "Workflow definition {WorkflowDefinitionId} not found",
            request.WorkflowDefinitionId);

        _metrics.RecordWorkflowCompleted(
            "GenericWorkflow",
            request.WorkflowDefinitionId.ToString(),
            "NotFound");

        span
            .RecordException(ex)
            .SetAttribute("error.type", "workflow_not_found")
            .SetAttribute("error.workflow_definition_id", request.WorkflowDefinitionId.ToString())
            .SetAttribute("workflow.duration_ms", elapsed.TotalMilliseconds)
            .Fail("Workflow definition not found", ex);

        throw;
    }

    private async Task<WorkflowResult> HandleGenericWorkflowExceptionAsync(
        Exception ex,
        StartWorkflowRequest request,
        IWorkflowSpan span,
        TimeSpan elapsed)
    {
        _logger.LogError(ex,
            "Unexpected error starting workflow {WorkflowDefinitionId}: {ErrorMessage}",
            request.WorkflowDefinitionId,
            ex.Message);

        _metrics.RecordWorkflowCompleted(
            "GenericWorkflow",
            request.WorkflowDefinitionId.ToString(),
            "Error");

        span
            .RecordException(ex)
            .SetAttribute("error.type", ex.GetType().Name)
            .SetAttribute("error.is_retriable", IsRetriableException(ex))
            .SetAttribute("workflow.duration_ms", elapsed.TotalMilliseconds)
            .AddEvent("workflow.error", new Dictionary<string, object>
            {
                ["exception_type"] = ex.GetType().Name,
                ["is_retriable"] = IsRetriableException(ex),
                ["workflow_definition_id"] = request.WorkflowDefinitionId.ToString()
            })
            .Fail("Workflow start failed", ex);

        throw;
    }

    private static bool IsRetriableException(Exception ex) =>
        ex is TimeoutException or HttpRequestException or TaskCanceledException;
}

#endregion

#region 3. Configuration Setup Examples

/// <summary>
/// Complete configuration setup examples for different environments.
/// </summary>
public static class WorkflowObservabilityConfiguration
{
    /// <summary>
    /// Development environment setup with console output and full sampling.
    /// </summary>
    public static void ConfigureForDevelopment(IServiceCollection services, IConfiguration configuration)
    {
        // Register telemetry services
        services.AddWorkflowTelemetry(configuration, HostEnvironment.Development);
        services.AddWorkflowTelemetryServices();

        // Configure for development-friendly settings
        services.Configure<WorkflowTelemetryOptions>(options =>
        {
            options.Enabled = true;
            options.EnableConsoleExporter = true;
            options.EnableOtlpExporter = false;
            options.ServiceName = "CollateralAppraisal.Workflow.Dev";
            options.ServiceVersion = "dev-build";
            options.Sampling.TraceRatio = 1.0; // Sample everything in development
            options.Sampling.SamplingStrategy = "AlwaysOn";
            
            // Development-friendly export settings
            options.Export.Console.IncludeTimestamps = true;
            options.Export.Console.IncludeScopes = true;
            options.Export.Console.SingleLine = false;
            
            // Add development resource attributes
            options.ResourceAttributes = new Dictionary<string, string>
            {
                ["environment"] = "development",
                ["developer"] = Environment.UserName,
                ["machine"] = Environment.MachineName
            };
        });

        // Add health checks
        services.AddHealthChecks()
            .AddWorkflowTelemetry();
    }

    /// <summary>
    /// Staging environment setup with OTLP export and moderate sampling.
    /// </summary>
    public static void ConfigureForStaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflowTelemetry(configuration, HostEnvironment.Staging);
        services.AddWorkflowTelemetryServices();

        services.Configure<WorkflowTelemetryOptions>(options =>
        {
            options.Enabled = true;
            options.EnableConsoleExporter = false;
            options.EnableOtlpExporter = true;
            options.OtlpEndpoint = configuration.GetValue<string>("OTLP_ENDPOINT") ?? "http://jaeger-staging:14268";
            options.ServiceName = "CollateralAppraisal.Workflow.Staging";
            options.ServiceVersion = configuration.GetValue<string>("APP_VERSION") ?? "staging";
            
            // Moderate sampling for staging
            options.Sampling.TraceRatio = 0.5;
            options.Sampling.SamplingStrategy = "TraceIdRatio";
            
            // Staging-optimized performance settings
            options.Performance.MaxQueueSize = 2048;
            options.Performance.BatchExportSize = 512;
            options.Performance.ExportTimeoutSeconds = 15;
            
            // OTLP configuration
            options.Export.Otlp.Protocol = "grpc";
            options.Export.Otlp.TimeoutSeconds = 10;
            options.Export.Otlp.UseCompression = true;
            
            options.ResourceAttributes = new Dictionary<string, string>
            {
                ["environment"] = "staging",
                ["datacenter"] = "staging-us-east-1",
                ["version"] = options.ServiceVersion
            };
        });

        services.AddHealthChecks()
            .AddWorkflowTelemetry();
    }

    /// <summary>
    /// Production environment setup with optimized performance and minimal sampling.
    /// </summary>
    public static void ConfigureForProduction(IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflowTelemetry(configuration, HostEnvironment.Production);
        services.AddWorkflowTelemetryServices();

        services.Configure<WorkflowTelemetryOptions>(options =>
        {
            options.Enabled = configuration.GetValue<bool>("WorkflowTelemetry:Enabled", true);
            options.EnableConsoleExporter = false;
            options.EnableOtlpExporter = true;
            options.OtlpEndpoint = configuration.GetRequiredSection("WorkflowTelemetry:OtlpEndpoint").Value;
            options.ServiceName = "CollateralAppraisal.Workflow";
            options.ServiceVersion = configuration.GetRequiredSection("APP_VERSION").Value;
            
            // Conservative sampling for production
            options.Sampling.TraceRatio = configuration.GetValue<double>("WorkflowTelemetry:Sampling:TraceRatio", 0.1);
            options.Sampling.SamplingStrategy = "TraceIdRatio";
            
            // Custom sampling rules for critical paths
            options.Sampling.CustomRules = new Dictionary<string, double>
            {
                ["workflow.execute"] = 0.05,           // Sample 5% of workflow executions
                ["workflow.activity.execute"] = 0.02,  // Sample 2% of activity executions  
                ["workflow.external_call"] = 1.0,      // Sample all external calls
                ["workflow.error"] = 1.0               // Sample all errors
            };
            
            // Production-optimized performance settings
            options.Performance.MaxQueueSize = 8192;
            options.Performance.BatchExportSize = 2048;
            options.Performance.ExportTimeoutSeconds = 30;
            options.Performance.MaxAttributes = 64;
            options.Performance.MaxAttributeLength = 512;
            
            // Production OTLP configuration with authentication
            options.Export.Otlp.Protocol = "grpc";
            options.Export.Otlp.TimeoutSeconds = 30;
            options.Export.Otlp.UseCompression = true;
            options.Export.Otlp.Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {configuration.GetRequiredSection("OTLP_AUTH_TOKEN").Value}"
            };
            
            options.ResourceAttributes = new Dictionary<string, string>
            {
                ["environment"] = "production",
                ["datacenter"] = configuration.GetValue<string>("DATACENTER", "us-east-1"),
                ["cluster"] = configuration.GetValue<string>("CLUSTER", "prod-cluster"),
                ["version"] = options.ServiceVersion,
                ["service.namespace"] = "collateral-appraisal"
            };
        });

        // Add resilience wrapper for production
        services.Decorate<IWorkflowMetrics, ResilientWorkflowMetrics>();
        services.Decorate<IWorkflowTracing, ResilientWorkflowTracing>();

        services.AddHealthChecks()
            .AddWorkflowTelemetry();
    }

    /// <summary>
    /// Docker container configuration with environment variable overrides.
    /// </summary>
    public static void ConfigureForContainer(IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflowTelemetry(configuration, HostEnvironment.Production);
        services.AddWorkflowTelemetryServices();

        services.Configure<WorkflowTelemetryOptions>(options =>
        {
            // Container-friendly configuration using environment variables
            options.Enabled = GetBoolFromEnv("WORKFLOW_TELEMETRY_ENABLED", true);
            options.EnableConsoleExporter = GetBoolFromEnv("WORKFLOW_TELEMETRY_CONSOLE", false);
            options.EnableOtlpExporter = GetBoolFromEnv("WORKFLOW_TELEMETRY_OTLP", true);
            options.OtlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4317";
            options.ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "CollateralAppraisal.Workflow";
            options.ServiceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "1.0.0";
            
            options.Sampling.TraceRatio = GetDoubleFromEnv("WORKFLOW_TELEMETRY_SAMPLING_RATIO", 0.1);
            
            // Container resource detection
            options.ResourceAttributes = new Dictionary<string, string>
            {
                ["environment"] = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "production",
                ["container.id"] = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown",
                ["k8s.pod.name"] = Environment.GetEnvironmentVariable("POD_NAME") ?? "unknown",
                ["k8s.namespace.name"] = Environment.GetEnvironmentVariable("NAMESPACE") ?? "default"
            };
        });

        services.AddHealthChecks()
            .AddWorkflowTelemetry();
    }

    private static bool GetBoolFromEnv(string key, bool defaultValue) =>
        bool.TryParse(Environment.GetEnvironmentVariable(key), out var result) ? result : defaultValue;

    private static double GetDoubleFromEnv(string key, double defaultValue) =>
        double.TryParse(Environment.GetEnvironmentVariable(key), out var result) ? result : defaultValue;
}

#endregion

#region 4. Custom Health Check Implementation

/// <summary>
/// Custom health check implementation for workflow telemetry system.
/// </summary>
public class WorkflowTelemetryHealthCheck : IHealthCheck
{
    private readonly WorkflowTelemetryOptions _options;
    private readonly ILogger<WorkflowTelemetryHealthCheck> _logger;

    public WorkflowTelemetryHealthCheck(
        IOptionsMonitor<WorkflowTelemetryOptions> optionsMonitor,
        ILogger<WorkflowTelemetryHealthCheck> logger)
    {
        _options = optionsMonitor.CurrentValue;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var healthData = new Dictionary<string, object>();
        var healthStatus = HealthStatus.Healthy;
        var healthDescription = "All telemetry components are healthy";

        try
        {
            // 1. Check if telemetry is enabled
            healthData["telemetry_enabled"] = _options.Enabled;
            if (!_options.Enabled)
            {
                return HealthCheckResult.Degraded("Telemetry is disabled", null, healthData);
            }

            // 2. Check individual components
            healthData["tracing_enabled"] = _options.EnableTracing;
            healthData["metrics_enabled"] = _options.EnableMetrics;
            healthData["console_exporter"] = _options.EnableConsoleExporter;
            healthData["otlp_exporter"] = _options.EnableOtlpExporter;

            // 3. Validate service configuration
            healthData["service_name"] = _options.ServiceName;
            healthData["service_version"] = _options.ServiceVersion;
            
            if (string.IsNullOrWhiteSpace(_options.ServiceName))
            {
                healthStatus = HealthStatus.Degraded;
                healthDescription = "Service name is not configured";
            }

            // 4. Check ActivitySource status
            var activitySourceStatus = CheckActivitySourceHealth();
            healthData["activity_source_status"] = activitySourceStatus.IsHealthy;
            if (!activitySourceStatus.IsHealthy)
            {
                healthStatus = HealthStatus.Degraded;
                healthDescription = $"ActivitySource issue: {activitySourceStatus.Error}";
            }

            // 5. Check Meter status  
            var meterStatus = CheckMeterHealth();
            healthData["meter_status"] = meterStatus.IsHealthy;
            if (!meterStatus.IsHealthy)
            {
                healthStatus = HealthStatus.Degraded;
                healthDescription = $"Meter issue: {meterStatus.Error}";
            }

            // 6. Test OTLP connectivity if enabled
            if (_options.EnableOtlpExporter)
            {
                var otlpStatus = await CheckOtlpConnectivityAsync(cancellationToken);
                healthData["otlp_connectivity"] = otlpStatus.IsHealthy;
                healthData["otlp_endpoint"] = _options.OtlpEndpoint;
                
                if (!otlpStatus.IsHealthy)
                {
                    healthStatus = HealthStatus.Degraded;
                    healthDescription = $"OTLP connectivity issue: {otlpStatus.Error}";
                }
            }

            // 7. Check sampling configuration
            healthData["sampling_ratio"] = _options.Sampling.TraceRatio;
            healthData["sampling_strategy"] = _options.Sampling.SamplingStrategy;
            
            if (_options.Sampling.TraceRatio < 0 || _options.Sampling.TraceRatio > 1)
            {
                healthStatus = HealthStatus.Unhealthy;
                healthDescription = "Invalid sampling ratio configuration";
            }

            // 8. Performance settings validation
            healthData["max_queue_size"] = _options.Performance.MaxQueueSize;
            healthData["batch_export_size"] = _options.Performance.BatchExportSize;
            
            if (_options.Performance.MaxQueueSize <= 0 || _options.Performance.BatchExportSize <= 0)
            {
                healthStatus = HealthStatus.Unhealthy;
                healthDescription = "Invalid performance configuration";
            }

            return new HealthCheckResult(healthStatus, healthDescription, null, healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during telemetry health check");
            
            healthData["error"] = ex.Message;
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                "Telemetry health check failed",
                ex,
                healthData);
        }
    }

    private ComponentHealthStatus CheckActivitySourceHealth()
    {
        try
        {
            var activitySource = WorkflowTelemetryConstants.ActivitySource;
            if (activitySource == null)
            {
                return new ComponentHealthStatus(false, "ActivitySource is null");
            }

            // Test activity creation
            using var testActivity = activitySource.StartActivity("health-check-test");
            if (testActivity == null && activitySource.HasListeners())
            {
                return new ComponentHealthStatus(false, "Cannot create activities despite having listeners");
            }

            return new ComponentHealthStatus(true, null);
        }
        catch (Exception ex)
        {
            return new ComponentHealthStatus(false, ex.Message);
        }
    }

    private ComponentHealthStatus CheckMeterHealth()
    {
        try
        {
            var meter = WorkflowTelemetryConstants.Meter;
            if (meter == null)
            {
                return new ComponentHealthStatus(false, "Meter is null");
            }

            // Test meter functionality
            var testCounter = meter.CreateCounter<int>("health_check_test_counter");
            testCounter.Add(1, new KeyValuePair<string, object?>("test", "health-check"));

            return new ComponentHealthStatus(true, null);
        }
        catch (Exception ex)
        {
            return new ComponentHealthStatus(false, ex.Message);
        }
    }

    private async Task<ComponentHealthStatus> CheckOtlpConnectivityAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.OtlpEndpoint))
        {
            return new ComponentHealthStatus(false, "OTLP endpoint not configured");
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            // Simple connectivity check (HEAD request to OTLP endpoint)
            var response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, _options.OtlpEndpoint),
                cancellationToken);

            // OTLP endpoints typically return 405 Method Not Allowed for HEAD requests
            // We just want to verify connectivity, not functionality
            return new ComponentHealthStatus(true, null);
        }
        catch (HttpRequestException ex)
        {
            return new ComponentHealthStatus(false, $"HTTP connectivity failed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return new ComponentHealthStatus(false, $"Connection timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ComponentHealthStatus(false, $"Unexpected error: {ex.Message}");
        }
    }

    private record ComponentHealthStatus(bool IsHealthy, string? Error);
}

/// <summary>
/// Extension methods for registering workflow telemetry health checks.
/// </summary>
public static class WorkflowTelemetryHealthCheckExtensions
{
    public static IServiceCollection AddWorkflowTelemetryHealthCheck(
        this IServiceCollection services,
        string name = "workflow_telemetry",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return services.AddHealthChecks()
            .AddCheck<WorkflowTelemetryHealthCheck>(
                name,
                failureStatus ?? HealthStatus.Degraded,
                tags)
            .Services;
    }
}

#endregion

#region 5. Integration Test Examples

/// <summary>
/// Integration test examples for workflow observability.
/// </summary>
[TestClass]
public class WorkflowObservabilityIntegrationTests
{
    private TestServer _server;
    private HttpClient _client;
    private IServiceProvider _serviceProvider;
    private TestTelemetryExporter _testExporter;

    [TestInitialize]
    public async Task Setup()
    {
        // Create test server with telemetry
        var builder = WebApplication.CreateBuilder();
        
        // Configure test telemetry
        builder.Services.AddWorkflowTelemetry(builder.Configuration, builder.Environment);
        builder.Services.AddWorkflowTelemetryServices();
        
        // Add test exporter to capture telemetry data
        _testExporter = new TestTelemetryExporter();
        builder.Services.AddSingleton<TestTelemetryExporter>(_testExporter);
        
        // Override configuration for testing
        builder.Services.Configure<WorkflowTelemetryOptions>(options =>
        {
            options.Enabled = true;
            options.EnableConsoleExporter = false;
            options.EnableOtlpExporter = false;
            options.ServiceName = "TestWorkflowService";
            options.Sampling.TraceRatio = 1.0; // Sample everything in tests
        });

        // Add test services
        builder.Services.AddScoped<IWorkflowService, TestWorkflowService>();
        builder.Services.AddScoped<IWorkflowRepository, InMemoryWorkflowRepository>();
        
        var app = builder.Build();
        _server = new TestServer(builder.WebHost);
        _client = _server.CreateClient();
        _serviceProvider = _server.Services;
    }

    [TestMethod]
    public async Task StartWorkflow_WithObservability_GeneratesCompleteTelemity()
    {
        // Arrange
        var workflowService = _serviceProvider.GetRequiredService<IWorkflowService>();
        var request = new StartWorkflowRequest
        {
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowDefinitionId = Guid.NewGuid(),
            InstanceName = "Test Workflow",
            StartedBy = "integration-test",
            CorrelationId = "test-correlation-123",
            InitialVariables = new Dictionary<string, object>
            {
                ["TestVariable"] = "TestValue",
                ["Priority"] = "high"
            }
        };

        // Act
        var result = await workflowService.StartWorkflowAsync(request);

        // Assert - Verify telemetry was generated
        Assert.IsNotNull(result);
        
        // Verify traces were created
        var traces = _testExporter.GetExportedTraces();
        Assert.IsTrue(traces.Any(t => t.OperationName.Contains("workflow.service.start")));
        
        var workflowTrace = traces.First(t => t.OperationName.Contains("workflow.service.start"));
        Assert.AreEqual(request.WorkflowInstanceId.ToString(), workflowTrace.GetAttribute("workflow.instance.id"));
        Assert.AreEqual(request.CorrelationId, workflowTrace.GetAttribute("workflow.correlation.id"));
        Assert.AreEqual("StartWorkflowAsync", workflowTrace.GetAttribute("service.method"));

        // Verify metrics were recorded
        var metrics = _testExporter.GetExportedMetrics();
        Assert.IsTrue(metrics.Any(m => m.Name == "workflow_executions_total"));
        Assert.IsTrue(metrics.Any(m => m.Name == "workflow_execution_duration"));

        // Verify logs contain correlation context
        var logs = _testExporter.GetExportedLogs();
        var workflowLogs = logs.Where(l => l.ContainsKey("WorkflowInstanceId")).ToList();
        Assert.IsTrue(workflowLogs.Count > 0);
        Assert.IsTrue(workflowLogs.Any(l => l["CorrelationId"]?.ToString() == request.CorrelationId));
    }

    [TestMethod] 
    public async Task WorkflowActivity_WithException_RecordsErrorTelemetry()
    {
        // Arrange
        var activity = _serviceProvider.GetRequiredService<TestFailingActivity>();
        var context = new WorkflowExecutionContext
        {
            WorkflowInstance = new WorkflowInstance { Id = Guid.NewGuid() },
            WorkflowDefinition = new WorkflowDefinition { Id = Guid.NewGuid() },
            CorrelationId = "test-error-123",
            Variables = new Dictionary<string, object>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => activity.ExecuteAsync(context, CancellationToken.None));

        // Verify error telemetry
        var traces = _testExporter.GetExportedTraces();
        var errorTrace = traces.FirstOrDefault(t => t.Status == "Error");
        Assert.IsNotNull(errorTrace);
        Assert.AreEqual("InvalidOperationException", errorTrace.GetAttribute("error.type"));

        var metrics = _testExporter.GetExportedMetrics();
        var durationMetric = metrics.FirstOrDefault(m => m.Name == "workflow_activity_execution_duration");
        Assert.IsNotNull(durationMetric);
        Assert.AreEqual("Failed", durationMetric.GetTag("status"));

        var logs = _testExporter.GetExportedLogs();
        var errorLogs = logs.Where(l => l["LogLevel"]?.ToString() == "Error").ToList();
        Assert.IsTrue(errorLogs.Count > 0);
        Assert.IsTrue(errorLogs.Any(l => l["Exception"]?.ToString().Contains("InvalidOperationException") == true));
    }

    [TestMethod]
    public async Task HealthCheck_TelemetryEnabled_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthCheck = JsonSerializer.Deserialize<HealthCheckResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.IsNotNull(healthCheck);
        Assert.AreEqual("Healthy", healthCheck.Status);
        Assert.IsTrue(healthCheck.Entries.ContainsKey("workflow_telemetry"));
        
        var telemetryHealth = healthCheck.Entries["workflow_telemetry"];
        Assert.AreEqual("Healthy", telemetryHealth.Status);
        Assert.IsTrue((bool)telemetryHealth.Data["telemetry_enabled"]);
        Assert.IsTrue((bool)telemetryHealth.Data["activity_source_status"]);
        Assert.IsTrue((bool)telemetryHealth.Data["meter_status"]);
    }

    [TestMethod]
    public async Task ConcurrentWorkflows_MaintainCorrelationContext()
    {
        // Arrange
        var workflowService = _serviceProvider.GetRequiredService<IWorkflowService>();
        var tasks = new List<Task<WorkflowResult>>();
        
        // Create multiple concurrent workflows with different correlation IDs
        for (int i = 0; i < 5; i++)
        {
            var correlationId = $"concurrent-test-{i}";
            var request = new StartWorkflowRequest
            {
                WorkflowInstanceId = Guid.NewGuid(),
                WorkflowDefinitionId = Guid.NewGuid(),
                InstanceName = $"Concurrent Workflow {i}",
                StartedBy = "concurrent-test",
                CorrelationId = correlationId
            };

            tasks.Add(workflowService.StartWorkflowAsync(request));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(5, results.Length);
        Assert.IsTrue(results.All(r => r != null));

        // Verify each workflow maintained its correlation context
        var traces = _testExporter.GetExportedTraces();
        for (int i = 0; i < 5; i++)
        {
            var expectedCorrelationId = $"concurrent-test-{i}";
            var workflowTraces = traces.Where(t => 
                t.GetAttribute("workflow.correlation.id") == expectedCorrelationId).ToList();
            
            Assert.IsTrue(workflowTraces.Count > 0, 
                $"No traces found for correlation ID {expectedCorrelationId}");
            
            // Verify all traces for this workflow have the same correlation ID
            Assert.IsTrue(workflowTraces.All(t => 
                t.GetAttribute("workflow.correlation.id") == expectedCorrelationId));
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _server?.Dispose();
        _testExporter?.Clear();
    }
}

/// <summary>
/// Test telemetry exporter for capturing telemetry data in integration tests.
/// </summary>
public class TestTelemetryExporter
{
    private readonly List<TestTrace> _traces = new();
    private readonly List<TestMetric> _metrics = new();
    private readonly List<Dictionary<string, object?>> _logs = new();
    private readonly object _lock = new();

    public void ExportTrace(TestTrace trace)
    {
        lock (_lock)
        {
            _traces.Add(trace);
        }
    }

    public void ExportMetric(TestMetric metric)
    {
        lock (_lock)
        {
            _metrics.Add(metric);
        }
    }

    public void ExportLog(Dictionary<string, object?> log)
    {
        lock (_lock)
        {
            _logs.Add(log);
        }
    }

    public List<TestTrace> GetExportedTraces()
    {
        lock (_lock)
        {
            return new List<TestTrace>(_traces);
        }
    }

    public List<TestMetric> GetExportedMetrics()
    {
        lock (_lock)
        {
            return new List<TestMetric>(_metrics);
        }
    }

    public List<Dictionary<string, object?>> GetExportedLogs()
    {
        lock (_lock)
        {
            return new List<Dictionary<string, object?>>(_logs);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _traces.Clear();
            _metrics.Clear();
            _logs.Clear();
        }
    }
}

public class TestTrace
{
    public string OperationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Attributes { get; set; } = new();
    public List<TestTraceEvent> Events { get; set; } = new();

    public object? GetAttribute(string key) => Attributes.TryGetValue(key, out var value) ? value : null;
}

public class TestMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();

    public string? GetTag(string key) => Tags.TryGetValue(key, out var value) ? value : null;
}

public class TestTraceEvent
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> Attributes { get; set; } = new();
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, HealthCheckEntry> Entries { get; set; } = new();
}

public class HealthCheckEntry
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Data { get; set; } = new();
}

#endregion

#region 6. Performance Monitoring Patterns

/// <summary>
/// Performance monitoring service with adaptive telemetry.
/// </summary>
public class WorkflowPerformanceMonitor : BackgroundService
{
    private readonly IWorkflowMetrics _metrics;
    private readonly IWorkflowRepository _repository;
    private readonly ILogger<WorkflowPerformanceMonitor> _logger;
    private readonly WorkflowTelemetryOptions _options;

    public WorkflowPerformanceMonitor(
        IWorkflowMetrics metrics,
        IWorkflowRepository repository,
        ILogger<WorkflowPerformanceMonitor> logger,
        IOptionsMonitor<WorkflowTelemetryOptions> options)
    {
        _metrics = metrics;
        _repository = repository;
        _logger = logger;
        _options = options.CurrentValue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting workflow performance monitoring");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectSystemMetricsAsync();
                await CollectWorkflowMetricsAsync();
                await CollectPerformanceMetricsAsync();
                
                // Monitor memory usage and adjust sampling if needed
                await MonitorMemoryUsageAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance monitoring collection");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Workflow performance monitoring stopped");
    }

    private async Task CollectSystemMetricsAsync()
    {
        // Active workflows by status
        var activeWorkflows = await _repository.GetActiveWorkflowCountAsync();
        _metrics.UpdateActiveWorkflowsCount(activeWorkflows);

        var pendingWorkflows = await _repository.GetPendingWorkflowCountAsync();
        _metrics.UpdatePendingActivitiesCount(pendingWorkflows);

        // Workflow distribution by type
        var workflowsByType = await _repository.GetWorkflowCountByTypeAsync();
        foreach (var (workflowType, count) in workflowsByType)
        {
            _metrics.UpdateActiveWorkflowsCount(count, workflowType);
        }
    }

    private async Task CollectWorkflowMetricsAsync()
    {
        // Average execution times by workflow type
        var performanceStats = await _repository.GetPerformanceStatisticsAsync(TimeSpan.FromHours(1));
        
        foreach (var stat in performanceStats)
        {
            _metrics.RecordPerformanceMetric(
                "workflow_avg_duration_1h",
                stat.WorkflowType,
                stat.AverageDuration,
                new KeyValuePair<string, object?>("percentile", "avg"),
                new KeyValuePair<string, object?>("time_window", "1h"));

            _metrics.RecordPerformanceMetric(
                "workflow_p95_duration_1h", 
                stat.WorkflowType,
                stat.P95Duration,
                new KeyValuePair<string, object?>("percentile", "p95"),
                new KeyValuePair<string, object?>("time_window", "1h"));
        }
    }

    private async Task CollectPerformanceMetricsAsync()
    {
        // System resource usage
        var process = Process.GetCurrentProcess();
        
        _metrics.RecordGaugeMetric(
            "system_memory_usage_bytes",
            process.WorkingSet64,
            new KeyValuePair<string, object?>("type", "working_set"));

        _metrics.RecordGaugeMetric(
            "system_cpu_usage_percent",
            process.TotalProcessorTime.TotalMilliseconds,
            new KeyValuePair<string, object?>("type", "total_processor_time"));

        // GC statistics
        var gcStats = GC.GetGCMemoryInfo();
        _metrics.RecordGaugeMetric(
            "system_gc_total_memory_bytes", 
            gcStats.TotalAvailableMemoryBytes,
            new KeyValuePair<string, object?>("gc_generation", "total"));

        // Thread pool statistics
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionThreads);
        
        _metrics.RecordGaugeMetric(
            "system_thread_pool_usage_percent",
            (double)(maxWorkerThreads - availableWorkerThreads) / maxWorkerThreads * 100,
            new KeyValuePair<string, object?>("type", "worker_threads"));
    }

    private async Task MonitorMemoryUsageAsync()
    {
        var currentMemory = GC.GetTotalMemory(false);
        var memoryThreshold = 500_000_000; // 500MB threshold
        
        if (currentMemory > memoryThreshold)
        {
            _logger.LogWarning(
                "High memory usage detected: {MemoryUsage} bytes. Consider reducing telemetry sampling rate.",
                currentMemory);

            // Record high memory usage metric
            _metrics.RecordCounterMetric(
                "system_memory_warnings_total",
                new KeyValuePair<string, object?>("reason", "high_usage"),
                new KeyValuePair<string, object?>("threshold_bytes", memoryThreshold));
        }
    }
}

/// <summary>
/// Adaptive sampling service that adjusts based on system load.
/// </summary>
public class AdaptiveSamplingService : BackgroundService
{
    private readonly IOptionsMonitor<WorkflowTelemetryOptions> _optionsMonitor;
    private readonly ILogger<AdaptiveSamplingService> _logger;
    private double _currentSamplingRate;
    private readonly object _samplingLock = new();

    public AdaptiveSamplingService(
        IOptionsMonitor<WorkflowTelemetryOptions> optionsMonitor,
        ILogger<AdaptiveSamplingService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _currentSamplingRate = optionsMonitor.CurrentValue.Sampling.TraceRatio;
    }

    public double CurrentSamplingRate
    {
        get
        {
            lock (_samplingLock)
            {
                return _currentSamplingRate;
            }
        }
        private set
        {
            lock (_samplingLock)
            {
                _currentSamplingRate = value;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting adaptive sampling service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AdjustSamplingRateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during adaptive sampling adjustment");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Adaptive sampling service stopped");
    }

    private async Task AdjustSamplingRateAsync()
    {
        var systemLoad = await CalculateSystemLoadAsync();
        var baseSamplingRate = _optionsMonitor.CurrentValue.Sampling.TraceRatio;
        var newSamplingRate = CalculateAdaptiveSamplingRate(baseSamplingRate, systemLoad);

        if (Math.Abs(newSamplingRate - CurrentSamplingRate) > 0.01) // Only adjust if change is significant
        {
            _logger.LogInformation(
                "Adjusting sampling rate from {OldRate:F3} to {NewRate:F3} based on system load {SystemLoad:F2}",
                CurrentSamplingRate,
                newSamplingRate,
                systemLoad);

            CurrentSamplingRate = newSamplingRate;
        }
    }

    private async Task<double> CalculateSystemLoadAsync()
    {
        // Simple system load calculation based on memory, CPU, and GC pressure
        var process = Process.GetCurrentProcess();
        var totalMemory = GC.GetTotalMemory(false);
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        
        // Memory pressure (0.0 to 1.0)
        var memoryPressure = Math.Min(1.0, (double)totalMemory / gcMemoryInfo.TotalAvailableMemoryBytes);
        
        // GC pressure based on generation 2 collections
        var gen2Collections = GC.CollectionCount(2);
        var gcPressure = Math.Min(1.0, gen2Collections / 100.0); // Normalize to 0-1 range
        
        // Thread pool pressure
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var _);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var _);
        var threadPoolPressure = 1.0 - ((double)availableWorkerThreads / maxWorkerThreads);
        
        // Combined system load (weighted average)
        var systemLoad = (memoryPressure * 0.5) + (gcPressure * 0.3) + (threadPoolPressure * 0.2);
        
        return Math.Min(1.0, systemLoad);
    }

    private double CalculateAdaptiveSamplingRate(double baseSamplingRate, double systemLoad)
    {
        // Reduce sampling rate under high system load
        var loadFactor = Math.Max(0.01, 1.0 - systemLoad); // Never go below 1% sampling
        var adaptiveSamplingRate = baseSamplingRate * loadFactor;
        
        // Apply minimum and maximum bounds
        return Math.Max(0.001, Math.Min(1.0, adaptiveSamplingRate));
    }
}

#endregion

#region Supporting Classes and Interfaces

// Supporting classes for the examples above
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}

public class ValidationError
{
    public string Field { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }

    public ValidationError(string field, string message, string type = "validation")
    {
        Field = field;
        Message = message;
        Type = type;
    }
}

public class ProcessingResult
{
    public int ProcessedItems { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object>? OutputVariables { get; set; }
}

public class BusinessValidationException : Exception
{
    public string ValidationType { get; }
    public string FieldName { get; }
    public string ErrorCode { get; }
    public bool IsRetriable { get; }

    public BusinessValidationException(string message, string validationType, string fieldName, string errorCode, bool isRetriable = false)
        : base(message)
    {
        ValidationType = validationType;
        FieldName = fieldName;
        ErrorCode = errorCode;
        IsRetriable = isRetriable;
    }
}

public class WorkflowValidationException : Exception
{
    public List<string>? ValidationErrors { get; }

    public WorkflowValidationException(string message, List<string>? validationErrors = null)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }
}

public class WorkflowNotFoundException : Exception
{
    public WorkflowNotFoundException(string message) : base(message) { }
}

// Placeholder interfaces and classes for compilation
public interface IBusinessService
{
    Task<ProcessingResult> ProcessEntityAsync(string? entityId, string? entityType, Dictionary<string, object> variables, CancellationToken cancellationToken);
}

public interface IWorkflowService
{
    Task<WorkflowResult> StartWorkflowAsync(StartWorkflowRequest request);
    Task<WorkflowResult> ResumeWorkflowAsync(ResumeWorkflowRequest request);
}

public interface IWorkflowEngine
{
    Task<WorkflowResult> StartAsync(StartWorkflowRequest request);
    Task<WorkflowResult> ResumeAsync(ResumeWorkflowRequest request);
}

public interface IWorkflowRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id);
    Task<bool> WorkflowDefinitionExistsAsync(Guid definitionId);
    Task<int> GetActiveWorkflowCountAsync();
    Task<int> GetPendingWorkflowCountAsync();
    Task<Dictionary<string, int>> GetWorkflowCountByTypeAsync();
    Task<List<WorkflowPerformanceStatistics>> GetPerformanceStatisticsAsync(TimeSpan timeWindow);
}

public class WorkflowPerformanceStatistics
{
    public string WorkflowType { get; set; } = string.Empty;
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan P95Duration { get; set; }
}

public class WorkflowResult
{
    public Guid ExecutionId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = new();
    public int InitialActivityCount { get; set; }
    public int ActivitiesResumed { get; set; }
}

public class StartWorkflowRequest
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object>? InitialVariables { get; set; }
    public bool AutoStart { get; set; } = true;
}

public class ResumeWorkflowRequest
{
    public Guid WorkflowInstanceId { get; set; }
    public string BookmarkName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object>? TriggerData { get; set; }
}

public class WorkflowInstance
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Version { get; set; } = "1.0";
}

public class WorkflowDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class WorkflowExecutionContext
{
    public WorkflowInstance WorkflowInstance { get; set; } = new();
    public WorkflowDefinition WorkflowDefinition { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public int RetryCount { get; set; }
    public List<WorkflowExecutionHistoryItem>? ExecutionHistory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowExecutionHistoryItem
{
    public string ActivityName { get; set; } = string.Empty;
}

public abstract class WorkflowActivityBase
{
    public abstract Task<ActivityResult> ExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken);
}

public class ActivityResult
{
    public ActivityResultStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan? RetryDelay { get; set; }
    public Dictionary<string, object>? OutputVariables { get; set; }
    public TimeSpan Duration { get; set; }
    public string? NextActivity { get; set; }

    public static ActivityResult Success(Dictionary<string, object>? outputVariables = null)
        => new() { Status = ActivityResultStatus.Success, OutputVariables = outputVariables };

    public static ActivityResult Failed(string errorMessage)
        => new() { Status = ActivityResultStatus.Failed, ErrorMessage = errorMessage };

    public static ActivityResult Retry(string errorMessage, TimeSpan retryDelay)
        => new() { Status = ActivityResultStatus.Retry, ErrorMessage = errorMessage, RetryDelay = retryDelay };
}

public enum ActivityResultStatus
{
    Success,
    Failed,
    Retry,
    Suspended
}

#endregion