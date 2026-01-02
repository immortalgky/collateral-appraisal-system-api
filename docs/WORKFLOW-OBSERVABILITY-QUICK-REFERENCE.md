# Workflow Observability Quick Reference

## Table of Contents

1. [Setup & Configuration](#setup--configuration)
2. [Key Interfaces](#key-interfaces)
3. [Common Patterns](#common-patterns)
4. [Configuration Snippets](#configuration-snippets)
5. [Health Checks](#health-checks)
6. [Troubleshooting](#troubleshooting)
7. [Development Commands](#development-commands)

## Setup & Configuration

### 1. Basic Service Registration
```csharp
// Program.cs
builder.Services.AddWorkflowTelemetry(builder.Configuration, builder.Environment);
builder.Services.AddWorkflowTelemetryServices();

// Add health checks
builder.Services.AddHealthChecks()
    .AddWorkflowTelemetry();
```

### 2. Minimal Configuration
```json
{
  "WorkflowTelemetry": {
    "Enabled": true,
    "ServiceName": "CollateralAppraisal.Workflow",
    "ServiceVersion": "1.0.0",
    "EnableConsoleExporter": true
  }
}
```

### 3. Dependency Injection
```csharp
public class MyActivity : WorkflowActivityBase
{
    private readonly IWorkflowLogger _logger;
    private readonly IWorkflowMetrics _metrics;
    private readonly IWorkflowTracing _tracing;

    public MyActivity(
        IWorkflowLogger logger,
        IWorkflowMetrics metrics,
        IWorkflowTracing tracing)
    {
        _logger = logger;
        _metrics = metrics;
        _tracing = tracing;
    }
}
```

## Key Interfaces

### IWorkflowLogger
```csharp
// Correlation context (ALWAYS use in activities)
using var scope = _logger.CreateActivityCorrelationScope(
    context.WorkflowInstance.Id, 
    "activity-id", 
    "ActivityType", 
    context.CorrelationId);

// Lifecycle logging
_logger.LogActivityStarting("activity-id", "ActivityType", workflowInstanceId);
_logger.LogActivityCompleted("activity-id", "ActivityType", workflowInstanceId, duration, status);
_logger.LogActivityFailed("activity-id", "ActivityType", workflowInstanceId, errorMessage, exception);
```

### IWorkflowMetrics
```csharp
// Counter metrics
_metrics.RecordActivityStarted("ActivityType", "activity-name", "WorkflowType");
_metrics.RecordActivityCompleted("ActivityType", "activity-name", "WorkflowType", "Success");

// Duration metrics (with stopwatch)
_metrics.RecordActivityDuration("ActivityType", "activity-name", "WorkflowType", stopwatch.Elapsed, "Success");

// Custom tags
var tags = new[] { new KeyValuePair<string, object?>("custom.tag", "value") };
_metrics.RecordWorkflowStarted("WorkflowType", "workflow-def-id", tags);
```

### IWorkflowTracing
```csharp
// Create spans (ALWAYS use using statement)
using var span = _tracing.CreateActivitySpan("operation-name", "ActivityType", workflowInstanceId, executionId);

// Fluent API for enrichment
span
    .SetWorkflowDefinitionId(workflowDefinitionId)
    .SetCorrelationId(correlationId)
    .SetAttribute("custom.attribute", "value")
    .AddEvent("operation.started");

// Completion
span.Complete(); // Success
span.Fail("Error message", exception); // Failure
```

## Common Patterns

### 1. Complete Activity Pattern
```csharp
public override async Task<ActivityResult> ExecuteAsync(
    WorkflowExecutionContext context, 
    CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    
    // 1. Create correlation scope
    using var scope = _logger.CreateActivityCorrelationScope(
        context.WorkflowInstance.Id, 
        "my-activity", 
        nameof(MyActivity),
        context.CorrelationId);
    
    // 2. Create tracing span  
    using var span = _tracing.CreateActivitySpan(
        "my-activity", 
        nameof(MyActivity), 
        context.WorkflowInstance.Id, 
        Guid.NewGuid());
    
    span
        .SetWorkflowDefinitionId(context.WorkflowDefinition.Id)
        .SetCorrelationId(context.CorrelationId);

    try
    {
        // 3. Log start and record metrics
        _logger.LogActivityStarting("my-activity", nameof(MyActivity), context.WorkflowInstance.Id);
        _metrics.RecordActivityStarted(nameof(MyActivity), "my-activity", "MyWorkflow");
        
        // 4. Your business logic
        var result = await DoBusinessLogicAsync(context, cancellationToken);
        
        stopwatch.Stop();
        
        // 5. Log success and record metrics
        _metrics.RecordActivityCompleted(nameof(MyActivity), "my-activity", "MyWorkflow", "Success");
        _metrics.RecordActivityDuration(nameof(MyActivity), "my-activity", "MyWorkflow", stopwatch.Elapsed, "Success");
        _logger.LogActivityCompleted("my-activity", nameof(MyActivity), context.WorkflowInstance.Id, stopwatch.Elapsed, ActivityResultStatus.Success);
        
        span.Complete();
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        
        // 6. Log error and record failure metrics
        _logger.LogActivityFailed("my-activity", nameof(MyActivity), context.WorkflowInstance.Id, ex.Message, ex);
        _metrics.RecordActivityDuration(nameof(MyActivity), "my-activity", "MyWorkflow", stopwatch.Elapsed, "Failed");
        
        span.RecordException(ex).Fail("Activity execution failed", ex);
        throw;
    }
}
```

### 2. Service Method Pattern
```csharp
public async Task<WorkflowResult> StartWorkflowAsync(StartWorkflowRequest request)
{
    using var span = _tracing.CreateWorkflowSpan(
        "workflow.start",
        request.WorkflowInstanceId,
        request.WorkflowDefinitionId,
        request.CorrelationId);
    
    span
        .SetAttribute("workflow.name", request.InstanceName)
        .SetAttribute("workflow.started_by", request.StartedBy);

    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        _logger.LogWorkflowStarting(request.WorkflowDefinitionId, request.InstanceName, request.StartedBy, request.CorrelationId);
        
        var result = await _engine.StartAsync(request);
        stopwatch.Stop();
        
        _metrics.RecordWorkflowDuration("GenericWorkflow", request.WorkflowDefinitionId.ToString(), stopwatch.Elapsed, "Success");
        _logger.LogWorkflowStarted(result.WorkflowInstance);
        
        span.Complete();
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogWorkflowFailed(new WorkflowInstance { Id = request.WorkflowInstanceId }, ex.Message, ex);
        span.RecordException(ex).Fail("Workflow start failed", ex);
        throw;
    }
}
```

### 3. External Call Pattern
```csharp
public async Task<HttpResponseMessage> CallExternalServiceAsync(string url, string method, object payload)
{
    using var span = _tracing.CreateExternalCallSpan("webhook", url, method, _currentWorkflowInstanceId);
    
    span
        .SetAttribute("http.url", url)
        .SetAttribute("http.method", method);

    var stopwatch = Stopwatch.StartNew();
    try
    {
        var response = await _httpClient.SendAsync(CreateRequest(url, method, payload));
        stopwatch.Stop();
        
        span
            .SetAttribute("http.status_code", (int)response.StatusCode)
            .SetAttribute("http.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            
        _metrics.RecordExternalCall(url, method, (int)response.StatusCode, stopwatch.Elapsed);
        
        if (response.IsSuccessStatusCode)
            span.Complete();
        else
            span.Fail($"HTTP {response.StatusCode}");
            
        return response;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        span.RecordException(ex).Fail("External call failed", ex);
        throw;
    }
}
```

## Configuration Snippets

### Development Environment
```json
{
  "WorkflowTelemetry": {
    "Enabled": true,
    "EnableConsoleExporter": true,
    "EnableOtlpExporter": false,
    "ServiceName": "CollateralAppraisal.Workflow",
    "Sampling": {
      "TraceRatio": 1.0,
      "SamplingStrategy": "AlwaysOn"
    }
  }
}
```

### Production Environment
```json
{
  "WorkflowTelemetry": {
    "Enabled": true,
    "EnableConsoleExporter": false,
    "EnableOtlpExporter": true,
    "OtlpEndpoint": "https://otel-collector.company.com:4317",
    "ServiceName": "CollateralAppraisal.Workflow",
    "ServiceVersion": "1.2.3",
    "Sampling": {
      "TraceRatio": 0.1,
      "SamplingStrategy": "TraceIdRatio"
    },
    "Performance": {
      "MaxQueueSize": 4096,
      "BatchExportSize": 1024,
      "ExportTimeoutSeconds": 30
    },
    "ResourceAttributes": {
      "environment": "production",
      "datacenter": "us-east-1"
    }
  }
}
```

### Docker Environment Variables
```yaml
environment:
  - WorkflowTelemetry__Enabled=true
  - WorkflowTelemetry__EnableOtlpExporter=true
  - WorkflowTelemetry__OtlpEndpoint=http://jaeger:14268
  - WorkflowTelemetry__ServiceName=CollateralAppraisal.Workflow
  - WorkflowTelemetry__Sampling__TraceRatio=0.1
```

## Health Checks

### Endpoint URLs
```bash
# Overall health
GET /health

# Telemetry-specific health
GET /health?service=workflow_telemetry

# Ready check
GET /health/ready
```

### Expected Responses

#### Healthy
```json
{
  "status": "Healthy",
  "entries": {
    "workflow_telemetry": {
      "data": {
        "telemetry_enabled": true,
        "tracing_enabled": true,
        "metrics_enabled": true,
        "console_exporter": true,
        "otlp_exporter": false,
        "activity_source_status": true,
        "meter_status": true
      },
      "status": "Healthy"
    }
  }
}
```

#### Unhealthy
```json
{
  "status": "Unhealthy",
  "entries": {
    "workflow_telemetry": {
      "data": {
        "telemetry_enabled": false,
        "error": "Configuration not found"
      },
      "status": "Unhealthy"
    }
  }
}
```

## Troubleshooting

### Issue: Telemetry Not Working

**Quick Check:**
```bash
# 1. Check health endpoint
curl http://localhost:8080/health | jq '.entries.workflow_telemetry'

# 2. Check configuration
curl http://localhost:8080/health | jq '.entries.workflow_telemetry.data.telemetry_enabled'
# Should return: true
```

**Solutions:**
1. Verify `WorkflowTelemetry:Enabled` is `true` in configuration
2. Check service registration in `Program.cs`
3. Verify environment-specific configuration files

### Issue: No Traces in Jaeger

**Quick Check:**
```bash
# Test OTLP endpoint connectivity
curl -X POST http://jaeger:14268/api/traces -H "Content-Type: application/json" -d '{}'

# Check application logs
docker logs your-app | grep -i "otlp\|export"
```

**Solutions:**
1. Verify `EnableOtlpExporter: true` and correct `OtlpEndpoint`
2. Check network connectivity to OTLP endpoint
3. Verify sampling configuration isn't dropping traces

### Issue: High Memory Usage

**Quick Check:**
```bash
# Monitor memory usage
docker stats your-app

# Check queue sizes
curl http://localhost:8080/metrics | grep workflow_queue_size
```

**Solutions:**
1. Reduce sampling rate: `"TraceRatio": 0.01`
2. Optimize batch sizes: `"BatchExportSize": 256`
3. Implement adaptive sampling in high-volume scenarios

### Issue: Performance Degradation

**Quick Diagnosis:**
```csharp
// Add to your activity for testing
var startTime = DateTime.UtcNow;
// ... your telemetry code ...
var telemetryOverhead = DateTime.UtcNow - startTime;
if (telemetryOverhead.TotalMilliseconds > 10)
    _logger.LogWarning("High telemetry overhead: {Overhead}ms", telemetryOverhead.TotalMilliseconds);
```

**Solutions:**
1. Use `LoggerMessage.Define` for high-frequency logs
2. Implement telemetry circuit breaker pattern
3. Consider disabling telemetry for health check endpoints

### Issue: Missing Correlation Context

**Quick Check:**
```csharp
// In your activity, log before and after scope creation
_logger.LogInformation("Before scope - should not have WorkflowInstanceId");

using var scope = _logger.CreateActivityCorrelationScope(workflowInstanceId, "activity", "Type");
_logger.LogInformation("Inside scope - should have WorkflowInstanceId");
```

**Solution:**
Always use correlation scopes in activities and ensure they're created at the beginning of async operations.

## Development Commands

### Local Development Setup
```bash
# Start with telemetry enabled
dotnet run --project Bootstrapper/Api --environment Development

# Check telemetry status
curl http://localhost:7111/health | jq '.entries.workflow_telemetry'

# View console telemetry output (if enabled)
dotnet run --project Bootstrapper/Api | grep -E "(WorkflowInstanceId|TraceId|SpanId)"
```

### Testing Commands
```bash
# Start a test workflow with correlation
curl -X POST http://localhost:7111/workflows \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: test-123" \
  -d '{
    "workflowDefinitionId": "test-workflow",
    "instanceName": "Test Instance"
  }'

# Check logs for correlation context
curl http://localhost:7111/health && echo "Correlation ID test-123 should appear in logs"
```

### Production Monitoring
```bash
# Check metrics endpoint
curl http://localhost:8080/metrics | grep workflow_

# Monitor specific metrics
curl http://localhost:8080/metrics | grep -E "(workflow_executions_total|workflow_active_instances)"

# Test OTLP connectivity
curl -v http://your-otlp-endpoint:4317/v1/traces
```

## Environment Variables Reference

| Variable | Description | Default |
|----------|-------------|---------|
| `WorkflowTelemetry__Enabled` | Enable/disable telemetry | `true` |
| `WorkflowTelemetry__ServiceName` | Service identifier | `CollateralAppraisal.Workflow` |
| `WorkflowTelemetry__ServiceVersion` | Service version | `1.0.0` |
| `WorkflowTelemetry__EnableConsoleExporter` | Console output | `false` |
| `WorkflowTelemetry__EnableOtlpExporter` | OTLP export | `false` |
| `WorkflowTelemetry__OtlpEndpoint` | OTLP endpoint URL | - |
| `WorkflowTelemetry__Sampling__TraceRatio` | Sampling percentage | `1.0` |

## Performance Guidelines

| Scenario | Recommended Settings |
|----------|---------------------|
| **Development** | `TraceRatio: 1.0`, `EnableConsoleExporter: true` |
| **Staging** | `TraceRatio: 0.5`, `EnableOtlpExporter: true` |
| **Production (Low Volume)** | `TraceRatio: 0.1`, `BatchExportSize: 512` |
| **Production (High Volume)** | `TraceRatio: 0.01`, `BatchExportSize: 2048`, Custom sampling |

## Security Considerations

- Never log sensitive data in telemetry attributes
- Sanitize URLs in external call traces (remove tokens/keys)
- Use correlation IDs instead of business identifiers when possible
- Configure appropriate retention policies for trace data
- Ensure OTLP endpoints use TLS in production