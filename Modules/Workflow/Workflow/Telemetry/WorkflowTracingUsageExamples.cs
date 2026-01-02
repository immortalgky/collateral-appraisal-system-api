using System.Diagnostics;

namespace Workflow.Telemetry;

/// <summary>
/// Examples and usage patterns for WorkflowTracing distributed tracing functionality.
/// This class demonstrates how to use the tracing API in various workflow scenarios.
/// </summary>
public static class WorkflowTracingUsageExamples
{
    /// <summary>
    /// Example: Simple workflow operation tracing with automatic span management
    /// </summary>
    public static async Task ExampleWorkflowOperationTracing(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();
        var workflowDefinitionId = Guid.NewGuid();
        var correlationId = "REQ-12345";

        // Example 1: Simple workflow operation with automatic span management
        var result = await tracing.TraceWorkflowOperationAsync(
            WorkflowTelemetryConstants.ActivityNames.WorkflowStart,
            workflowInstanceId,
            workflowDefinitionId,
            async (span) =>
            {
                // Add additional context
                span.SetWorkflowStatus("Running")
                    .SetAttribute("user_id", "john.doe@example.com")
                    .SetAttribute("priority", "high");

                // Record workflow milestone
                span.RecordMilestone("schema_loaded", "Workflow schema loaded successfully");

                // Simulate workflow operation
                await Task.Delay(100);

                // Record timing information
                span.RecordTiming("schema_validation", TimeSpan.FromMilliseconds(50));

                return "Workflow started successfully";
            },
            correlationId);

        Console.WriteLine($"Workflow operation result: {result}");
    }

    /// <summary>
    /// Example: Activity execution tracing with error handling
    /// </summary>
    public static async Task ExampleActivityExecutionTracing(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();
        var activityExecutionId = Guid.NewGuid();

        try
        {
            await tracing.TraceActivityExecutionAsync(
                "UserApprovalActivity",
                "HumanTask",
                workflowInstanceId,
                activityExecutionId,
                async (span) =>
                {
                    // Add activity context
                    span.SetAttribute("assignee_id", "approver123")
                        .SetAttribute("due_date", DateTime.UtcNow.AddDays(1))
                        .SetAttribute("task_priority", "medium");

                    // Record activity milestone
                    span.RecordMilestone("task_assigned", "Task assigned to user");

                    // Simulate activity execution (could throw exception)
                    await SimulateActivityExecution();

                    span.RecordMilestone("task_completed", "Task completed by user");

                    return "Activity completed";
                });
        }
        catch (Exception ex)
        {
            // Exception is automatically recorded in the span
            Console.WriteLine($"Activity execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: External call tracing with HTTP response handling
    /// </summary>
    public static async Task ExampleExternalCallTracing(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();
        var targetUrl = "https://api.example.com/webhook";

        await tracing.TraceExternalCallAsync(
            "webhook",
            targetUrl,
            "POST",
            workflowInstanceId,
            async (span) =>
            {
                try
                {
                    // Simulate HTTP call
                    using var httpClient = new HttpClient();
                    var response = await httpClient.PostAsync(targetUrl, new StringContent("{}"));

                    // Record HTTP response details
                    span.WithHttpResponse((int)response.StatusCode, TimeSpan.FromMilliseconds(150));

                    return "Webhook call successful";
                }
                catch (HttpRequestException httpEx)
                {
                    // HTTP-specific error handling
                    span.WithHttpResponse(500, TimeSpan.FromMilliseconds(5000))
                        .RecordException(httpEx);

                    throw;
                }
            });
    }

    /// <summary>
    /// Example: Database operation tracing with transaction context
    /// </summary>
    public static async Task ExampleDatabaseOperationTracing(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();

        await tracing.TraceDatabaseOperationAsync(
            "save",
            "WorkflowInstance",
            workflowInstanceId,
            async (span) =>
            {
                // Add database context
                span.SetAttribute("db.table", "workflow_instances")
                    .SetAttribute("db.operation", "INSERT")
                    .SetAttribute("db.transaction_id", Guid.NewGuid().ToString());

                // Record database milestone
                span.RecordMilestone("transaction_started", "Database transaction started");

                // Simulate database operation
                await Task.Delay(20);

                span.RecordMilestone("data_saved", "Workflow instance saved to database");
                span.RecordTiming("db_save_duration", TimeSpan.FromMilliseconds(20));

                return "Database operation completed";
            });
    }

    /// <summary>
    /// Example: Manual span management with custom attributes and events
    /// </summary>
    public static async Task ExampleManualSpanManagement(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();
        var workflowDefinitionId = Guid.NewGuid();

        // Create manual span
        using var span = tracing.CreateWorkflowSpan(
            WorkflowTelemetryConstants.ActivityNames.WorkflowExecution,
            workflowInstanceId,
            workflowDefinitionId,
            "CORR-98765");

        try
        {
            // Add custom attributes
            span.SetAttribute("execution_mode", "step_by_step")
                .SetAttribute("retry_count", 0)
                .SetAttribute("max_execution_time", TimeSpan.FromMinutes(5).TotalMilliseconds);

            // Add custom event with attributes
            span.AddEvent("workflow_validation", new Dictionary<string, object>
            {
                ["validation_type"] = "schema",
                ["validation_result"] = "passed",
                ["validation_duration_ms"] = 25
            });

            // Simulate workflow steps
            await Task.Delay(100);

            // Record another milestone
            span.RecordMilestone("first_activity_completed", "Initial activity processing finished");

            // Set final status
            span.Complete();
        }
        catch (Exception ex)
        {
            // Manual error handling
            span.Fail("Workflow execution failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Example: Nested spans with workflow context propagation
    /// </summary>
    public static async Task ExampleNestedSpansWithContextPropagation(IWorkflowTracing tracing)
    {
        var workflowInstanceId = Guid.NewGuid();
        var correlationId = "NESTED-123";

        // Create workflow tracing scope for context propagation
        using var tracingScope = tracing.CreateWorkflowTracingScope(workflowInstanceId, correlationId);

        // Root workflow span
        using var workflowSpan = tracing.CreateWorkflowSpan(
            WorkflowTelemetryConstants.ActivityNames.WorkflowExecution,
            workflowInstanceId,
            Guid.NewGuid(),
            correlationId);

        workflowSpan.SetWorkflowStatus("Running");

        // Child activity span - inherits context automatically
        using var activitySpan = tracing.CreateActivitySpan(
            "ProcessingActivity",
            "BusinessLogic",
            workflowInstanceId,
            Guid.NewGuid());

        activitySpan.SetAttribute("processing_type", "data_validation");

        // Grandchild external call span
        using var externalSpan = tracing.CreateExternalCallSpan(
            "api_call",
            "https://validation.service.com/validate",
            "POST",
            workflowInstanceId);

        externalSpan.SetAttribute("service_name", "validation_service")
            .SetAttribute("api_version", "v2");

        // Simulate nested operations
        await Task.Delay(50);

        // Complete spans in reverse order (automatic with 'using' statements)
        externalSpan.Complete();
        activitySpan.Complete();
        workflowSpan.Complete();
    }

    /// <summary>
    /// Example: Retrieving trace context for external service calls
    /// </summary>
    public static void ExampleTraceContextRetrieval(IWorkflowTracing tracing)
    {
        // Get current trace context
        var traceId = tracing.GetCurrentTraceId();
        var spanId = tracing.GetCurrentSpanId();

        if (!string.IsNullOrEmpty(traceId))
        {
            Console.WriteLine($"Current Trace ID: {traceId}");
            Console.WriteLine($"Current Span ID: {spanId}");

            // Use trace context for external service calls
            var httpHeaders = new Dictionary<string, string>
            {
                ["traceparent"] = $"00-{traceId}-{spanId}-01",
                ["correlation-id"] = tracing.GetBaggage("correlation_id") ?? "unknown"
            };

            Console.WriteLine("HTTP headers for external service:");
            foreach (var header in httpHeaders)
            {
                Console.WriteLine($"  {header.Key}: {header.Value}");
            }
        }
    }

    /// <summary>
    /// Example: Using baggage for workflow context propagation
    /// </summary>
    public static void ExampleBaggageUsage(IWorkflowTracing tracing)
    {
        // Set workflow context in baggage
        tracing.SetBaggage("workflow_instance_id", Guid.NewGuid().ToString());
        tracing.SetBaggage("correlation_id", "BAGGAGE-456");
        tracing.SetBaggage("user_id", "jane.doe@example.com");
        tracing.SetBaggage("tenant_id", "tenant-123");

        // Retrieve baggage values
        var workflowId = tracing.GetBaggage("workflow_instance_id");
        var correlationId = tracing.GetBaggage("correlation_id");
        var userId = tracing.GetBaggage("user_id");

        Console.WriteLine($"Workflow Context from Baggage:");
        Console.WriteLine($"  Workflow ID: {workflowId}");
        Console.WriteLine($"  Correlation ID: {correlationId}");
        Console.WriteLine($"  User ID: {userId}");

        // Baggage is automatically propagated to child spans and external services
    }

    /// <summary>
    /// Simulates activity execution that may fail
    /// </summary>
    private static async Task SimulateActivityExecution()
    {
        await Task.Delay(50);

        // Simulate random failure for demonstration
        if (Random.Shared.NextDouble() < 0.1) // 10% chance of failure
        {
            throw new InvalidOperationException("Simulated activity execution failure");
        }
    }
}

/// <summary>
/// Integration example showing how WorkflowEngine uses distributed tracing
/// </summary>
public class WorkflowEngineTracingIntegrationExample
{
    private readonly IWorkflowTracing _tracing;

    public WorkflowEngineTracingIntegrationExample(IWorkflowTracing tracing)
    {
        _tracing = tracing;
    }

    /// <summary>
    /// Example of how StartWorkflowAsync integrates distributed tracing
    /// </summary>
    public async Task<string> StartWorkflowWithTracingExample(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy)
    {
        // This mirrors the actual implementation in WorkflowEngine.StartWorkflowAsync
        return await _tracing.TraceWorkflowOperationAsync(
            WorkflowTelemetryConstants.ActivityNames.WorkflowStart,
            Guid.Empty, // Will be updated when instance is created
            workflowDefinitionId,
            async (span) =>
            {
                try
                {
                    // Step 1: Load workflow schema
                    span.AddEvent("schema_loading_started");
                    await Task.Delay(10); // Simulate schema loading
                    span.AddEvent("schema_loaded");

                    // Step 2: Create workflow instance
                    var workflowInstanceId = Guid.NewGuid();
                    span.SetWorkflowInstanceId(workflowInstanceId); // Update span with actual instance ID

                    span.AddEvent("instance_created", new Dictionary<string, object>
                    {
                        ["instance_id"] = workflowInstanceId,
                        ["instance_name"] = instanceName,
                        ["started_by"] = startedBy
                    });

                    // Step 3: Execute first activity (with child span)
                    using var activitySpan = _tracing.CreateActivitySpan(
                        "StartActivity",
                        "Start",
                        workflowInstanceId,
                        Guid.NewGuid());

                    await Task.Delay(20); // Simulate activity execution
                    activitySpan.Complete();

                    span.AddEvent("workflow_started");
                    return "Workflow started successfully";
                }
                catch (Exception ex)
                {
                    span.RecordException(ex);
                    throw;
                }
            });
    }

    /// <summary>
    /// Example of external call tracing with detailed HTTP context
    /// </summary>
    public async Task<string> ExecuteWebhookWithTracing(
        string webhookUrl,
        Guid workflowInstanceId,
        object payload)
    {
        return await _tracing.TraceExternalCallAsync(
            "webhook",
            webhookUrl,
            "POST",
            workflowInstanceId,
            async (span) =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    // Add webhook context
                    span.SetAttribute("webhook.payload_size", System.Text.Json.JsonSerializer.Serialize(payload).Length)
                        .SetAttribute("webhook.retry_count", 0)
                        .SetAttribute("webhook.timeout_ms", 30000);

                    // Simulate HTTP call
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    var json = System.Text.Json.JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(webhookUrl, content);

                    stopwatch.Stop();

                    // Record response details
                    span.WithHttpResponse((int)response.StatusCode, stopwatch.Elapsed)
                        .SetAttribute("response.content_length", response.Content.Headers.ContentLength ?? 0);

                    if (response.IsSuccessStatusCode)
                    {
                        span.AddEvent("webhook_success", new Dictionary<string, object>
                        {
                            ["status_code"] = (int)response.StatusCode,
                            ["duration_ms"] = stopwatch.ElapsedMilliseconds
                        });

                        return "Webhook executed successfully";
                    }
                    else
                    {
                        var errorMessage = $"Webhook failed with status {response.StatusCode}";
                        span.Fail(errorMessage);
                        throw new HttpRequestException(errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    span.WithHttpResponse(0, stopwatch.Elapsed)
                        .RecordException(ex);
                    throw;
                }
            });
    }
}