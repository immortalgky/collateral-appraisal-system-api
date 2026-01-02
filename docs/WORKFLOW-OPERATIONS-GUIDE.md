# Workflow Engine Operations Guide

## Production Deployment

### Configuration

#### Database Connection
```json
{
  "ConnectionStrings": {
    "WorkflowDatabase": "Server=workflow-db.prod.com;Database=WorkflowEngine;Trusted_Connection=false;User ID=workflow_user;Password=${DB_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

#### Background Services Configuration
```json
{
  "WorkflowEngine": {
    "OutboxProcessor": {
      "BatchSize": 50,
      "ProcessingInterval": "00:00:10",
      "MaxRetryAttempts": 5,
      "RetryDelayMultiplier": 2.0,
      "MaxRetryDelay": "00:00:30"
    },
    "TimerService": {
      "BatchSize": 100,
      "ProcessingInterval": "00:00:30",
      "LongRunningThreshold": "1.00:00:00"
    },
    "CleanupService": {
      "ProcessingInterval": "02:00:00",
      "CompletedWorkflowRetention": "90.00:00:00",
      "ExecutionLogRetention": "365.00:00:00",
      "OutboxEventRetention": "7.00:00:00"
    }
  }
}
```

#### Resilience Configuration
```json
{
  "Resilience": {
    "DatabaseOperations": {
      "Retry": {
        "MaxAttempts": 3,
        "DelayBase": "00:00:00.100",
        "DelayMultiplier": 2.0
      },
      "Timeout": "00:00:30"
    },
    "ExternalCalls": {
      "Retry": {
        "MaxAttempts": 3,
        "DelayBase": "00:00:01",
        "DelayMultiplier": 1.5
      },
      "CircuitBreaker": {
        "FailureThreshold": 0.5,
        "MinimumThroughput": 10,
        "SamplingDuration": "00:01:00",
        "BreakDuration": "00:00:30"
      },
      "Timeout": "00:02:00"
    }
  }
}
```

#### Logging Configuration
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Workflow": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/workflow/workflow-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq.logging.prod.com:5341",
          "apiKey": "${SEQ_API_KEY}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "WorkflowEngine",
      "Environment": "Production"
    }
  }
}
```

### Environment Variables

```bash
# Database
DB_PASSWORD=secure_password_here
DB_CONNECTION_TIMEOUT=30

# External Services
EXTERNAL_API_KEY=api_key_here
WEBHOOK_SECRET=webhook_secret_here

# Logging
SEQ_API_KEY=seq_api_key_here

# Performance
ASPNETCORE_ENVIRONMENT=Production
DOTNET_GCSERVER=1
DOTNET_GCConserveMemory=5
```

### Docker Deployment

#### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Create non-root user
RUN groupadd -r workflow && useradd -r -g workflow workflow
RUN mkdir -p /var/log/workflow && chown workflow:workflow /var/log/workflow

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Bootstrapper/Api/Api.csproj", "Bootstrapper/Api/"]
COPY ["Modules/Workflow/Workflow/Workflow.csproj", "Modules/Workflow/Workflow/"]
COPY ["Shared/Shared/Shared.csproj", "Shared/Shared/"]
RUN dotnet restore "Bootstrapper/Api/Api.csproj"

# Copy source and build
COPY . .
RUN dotnet build "Bootstrapper/Api/Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Bootstrapper/Api/Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Switch to non-root user
USER workflow

ENTRYPOINT ["dotnet", "Api.dll"]
```

#### Docker Compose for Production
```yaml
version: '3.8'

services:
  workflow-api:
    build: .
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__WorkflowDatabase=${DB_CONNECTION}
      - Serilog__WriteTo__1__Args__serverUrl=${SEQ_URL}
      - DB_PASSWORD=${DB_PASSWORD}
    depends_on:
      - workflow-db
      - seq
    volumes:
      - workflow-logs:/var/log/workflow
    restart: unless-stopped
    deploy:
      resources:
        limits:
          memory: 2G
          cpus: '1.0'
        reservations:
          memory: 512M
          cpus: '0.5'

  workflow-db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${DB_SA_PASSWORD}
      - MSSQL_PID=Standard
    ports:
      - "1433:1433"
    volumes:
      - workflow-db-data:/var/opt/mssql
    restart: unless-stopped

  seq:
    image: datalust/seq:latest
    environment:
      - ACCEPT_EULA=Y
      - SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
    ports:
      - "5341:80"
    volumes:
      - seq-data:/data
    restart: unless-stopped

volumes:
  workflow-db-data:
  seq-data:
  workflow-logs:
```

### Kubernetes Deployment

#### Deployment Manifest
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: workflow-api
  namespace: workflow
spec:
  replicas: 3
  selector:
    matchLabels:
      app: workflow-api
  template:
    metadata:
      labels:
        app: workflow-api
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: workflow-api
        image: workflow-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__WorkflowDatabase
          valueFrom:
            secretKeyRef:
              name: workflow-secrets
              key: db-connection
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
        volumeMounts:
        - name: workflow-logs
          mountPath: /var/log/workflow
      volumes:
      - name: workflow-logs
        emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: workflow-api-service
  namespace: workflow
spec:
  selector:
    app: workflow-api
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
  type: ClusterIP
```

## Monitoring and Observability

### Health Checks

The application provides several health check endpoints:

```http
GET /health          # Overall health
GET /health/ready    # Readiness check
GET /health/live     # Liveness check
```

Health check responses:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0056789"
    },
    "outbox-processor": {
      "status": "Healthy", 
      "duration": "00:00:00.0001234"
    },
    "external-api-circuit-breaker": {
      "status": "Degraded",
      "duration": "00:00:00.0000567",
      "description": "Circuit breaker is half-open"
    }
  }
}
```

### Metrics Collection

#### Prometheus Metrics
```csharp
// Configure in Program.cs
builder.Services.AddMetrics()
    .AddPrometheusExporter()
    .AddMeter("WorkflowEngine");

// Custom metrics
public static readonly Counter<long> WorkflowsStarted = 
    meter.CreateCounter<long>("workflow_instances_started_total");

public static readonly Histogram<double> ActivityExecutionDuration = 
    meter.CreateHistogram<double>("workflow_activity_execution_duration");

public static readonly Gauge<int> ActiveWorkflows = 
    meter.CreateGauge<int>("workflow_instances_active");
```

Key metrics to monitor:
```promql
# Workflow throughput
rate(workflow_instances_started_total[5m])
rate(workflow_instances_completed_total[5m])

# Workflow latency
histogram_quantile(0.95, workflow_execution_duration_bucket)

# Background service health
workflow_outbox_events_pending
workflow_outbox_processing_lag_seconds

# Error rates
rate(workflow_activities_failed_total[5m]) / rate(workflow_activities_executed_total[5m])

# Resource utilization
workflow_active_instances
workflow_database_connection_pool_active
```

### Alerting Rules

#### Critical Alerts
```yaml
# High error rate
- alert: WorkflowHighErrorRate
  expr: rate(workflow_activities_failed_total[5m]) / rate(workflow_activities_executed_total[5m]) > 0.1
  for: 2m
  labels:
    severity: critical
  annotations:
    summary: "High workflow error rate: {{ $value | humanizePercentage }}"

# Outbox processing lag
- alert: WorkflowOutboxLag
  expr: workflow_outbox_processing_lag_seconds > 300
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "Workflow outbox processing lag: {{ $value }}s"

# Database connectivity
- alert: WorkflowDatabaseDown
  expr: up{job="workflow-api"} == 0
  for: 1m
  labels:
    severity: critical
  annotations:
    summary: "Workflow database is unreachable"
```

### Distributed Tracing

#### OpenTelemetry Configuration
```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddEntityFrameworkCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddSource("WorkflowEngine")
               .SetSampler(new TraceIdRatioBasedSampler(0.1)) // Sample 10%
               .AddJaegerExporter();
    });

// Activity instrumentation in workflow engine
using var activity = WorkflowTracer.StartActivity("ExecuteActivity");
activity?.SetTag("workflow.id", workflowId);
activity?.SetTag("activity.id", activityId);
activity?.SetTag("activity.type", activityType);
```

## Performance Tuning

### Database Optimization

#### Connection Pooling
```json
{
  "ConnectionStrings": {
    "WorkflowDatabase": "Server=...;Min Pool Size=10;Max Pool Size=100;Pooling=true;Connection Timeout=30;Command Timeout=60"
  }
}
```

#### Index Optimization
```sql
-- Critical indexes for performance
CREATE INDEX IX_WorkflowInstance_Status_CreatedOn 
ON WorkflowInstance (Status, CreatedOn) 
INCLUDE (Id, CurrentActivityId);

CREATE INDEX IX_WorkflowBookmark_Processing 
ON WorkflowBookmark (WorkflowInstanceId, ActivityId, IsConsumed)
WHERE IsConsumed = 0;

CREATE INDEX IX_WorkflowOutbox_Processing 
ON WorkflowOutbox (Status, NextAttemptAt)
WHERE Status IN (0, 1); -- Pending, Processing

-- Partition large tables by date
CREATE PARTITION SCHEME WorkflowExecutionLogPartitionScheme
AS PARTITION WorkflowExecutionLogPartitionFunction
TO ([PRIMARY], [Q1_2024], [Q2_2024], [Q3_2024], [Q4_2024]);
```

#### Query Performance
```csharp
// ✅ Efficient queries
var activeWorkflows = await context.WorkflowInstances
    .Where(w => w.Status == WorkflowStatus.Running)
    .Select(w => new { w.Id, w.CurrentActivityId })
    .Take(100)
    .ToListAsync();

// ❌ Avoid - N+1 problems
var workflows = await context.WorkflowInstances.ToListAsync();
foreach (var workflow in workflows)
{
    var bookmarks = await context.WorkflowBookmarks
        .Where(b => b.WorkflowInstanceId == workflow.Id)
        .ToListAsync(); // N+1!
}
```

### Memory Management

#### Configuration
```bash
# .NET GC settings
export DOTNET_GCSERVER=1                    # Server GC
export DOTNET_GCConserveMemory=5           # Memory conservation
export DOTNET_GCHeapCount=4                # GC heap count
export DOTNET_GCHighMemPercent=90          # High memory threshold
```

#### Object Pooling
```csharp
// Use object pooling for frequently allocated objects
services.AddSingleton<ObjectPool<StringBuilder>>(provider =>
{
    var policy = new DefaultPooledObjectPolicy<StringBuilder>();
    return new DefaultObjectPool<StringBuilder>(policy);
});

// In workflow activities
var sb = _stringBuilderPool.Get();
try
{
    sb.AppendLine("Workflow processing...");
    return sb.ToString();
}
finally
{
    _stringBuilderPool.Return(sb);
}
```

### Caching Strategies

#### Workflow Definition Caching
```csharp
services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000;
    options.CompactionPercentage = 0.25;
});

public async Task<WorkflowDefinition> GetWorkflowDefinitionAsync(Guid id)
{
    return await _cache.GetOrCreateAsync($"workflow_def_{id}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
        entry.Size = 1;
        return await _repository.GetWorkflowDefinitionAsync(id);
    });
}
```

#### Redis Distributed Caching
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis.prod.com:6379";
    options.InstanceName = "WorkflowEngine";
});

// Cache workflow state for quick lookups
await _distributedCache.SetStringAsync(
    $"workflow_state_{workflowId}",
    JsonSerializer.Serialize(workflowState),
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    }
);
```

## Troubleshooting

### Common Issues

#### High Outbox Event Lag
**Symptoms**: Events taking longer than expected to process
```bash
# Check outbox queue size
kubectl exec -it workflow-api-pod -- \
  curl -s http://localhost/health | jq '.entries."outbox-processor"'

# Check database for stuck events
SELECT Status, COUNT(*), MIN(CreatedOn), MAX(CreatedOn)
FROM WorkflowOutbox 
GROUP BY Status;
```

**Solutions**:
1. Increase batch size: `"BatchSize": 100`
2. Decrease processing interval: `"ProcessingInterval": "00:00:05"`
3. Scale horizontally (multiple instances)
4. Check external service availability

#### Workflow Stuck in Suspended State
**Symptoms**: Workflows not resuming after external events
```sql
-- Find suspended workflows with no active bookmarks
SELECT wi.Id, wi.CurrentActivityId, wi.Status, wi.UpdatedOn
FROM WorkflowInstance wi
LEFT JOIN WorkflowBookmark wb ON wi.Id = wb.WorkflowInstanceId 
    AND wb.IsConsumed = 0
WHERE wi.Status = 1 -- Suspended
  AND wb.Id IS NULL
  AND wi.UpdatedOn < DATEADD(HOUR, -1, GETUTCDATE());
```

**Solutions**:
1. Check for missing bookmarks
2. Verify external systems are sending callbacks
3. Review activity logic for bookmark creation
4. Manual workflow cancellation if stuck permanently

#### Database Deadlocks
**Symptoms**: `DeadlockException` in logs
```sql
-- Check deadlock frequency
SELECT 
    database_name,
    wait_type,
    waiting_tasks_count,
    wait_time_ms,
    max_wait_time_ms,
    signal_wait_time_ms
FROM sys.dm_os_wait_stats
WHERE wait_type LIKE '%LOCK%'
ORDER BY wait_time_ms DESC;
```

**Solutions**:
1. Review transaction scopes (keep them small)
2. Use consistent lock ordering
3. Add appropriate indexes
4. Consider READ_COMMITTED_SNAPSHOT isolation

#### Memory Leaks
**Symptoms**: Increasing memory usage over time
```bash
# Monitor memory usage
kubectl top pod workflow-api-pod

# Check GC metrics
curl http://localhost/metrics | grep dotnet_gc
```

**Solutions**:
1. Review object disposal patterns
2. Check for event handler leaks
3. Use memory profilers (dotMemory, PerfView)
4. Implement proper IDisposable patterns

### Debug Mode Configuration

For production debugging (use carefully):
```json
{
  "Logging": {
    "LogLevel": {
      "Workflow.Workflow.Engine": "Debug",
      "Workflow.Workflow.Services": "Debug"
    }
  },
  "WorkflowEngine": {
    "EnableDetailedLogging": true,
    "LogActivityInputOutput": true,
    "LogStateTransitions": true
  }
}
```

### Log Analysis

#### Common Log Patterns
```bash
# Find workflow failures
grep "ENGINE: Critical error" /var/log/workflow/workflow-*.log

# Find slow activities (>5 seconds)
grep "Successfully completed execution.*[5-9][0-9][0-9][0-9]ms" /var/log/workflow/workflow-*.log

# Find concurrency conflicts
grep "Concurrency conflict" /var/log/workflow/workflow-*.log

# Find external call failures
grep "External.*failed" /var/log/workflow/workflow-*.log
```

#### Structured Log Queries (Seq)
```sql
-- Find all workflow failures in the last hour
SELECT *
FROM Events
WHERE @Timestamp > Now() - 1h
  AND @Level = 'Error'
  AND @MessageTemplate LIKE '%workflow%'
ORDER BY @Timestamp DESC

-- Activity performance analysis
SELECT ActivityType, AVG(Duration), COUNT(*)
FROM Events
WHERE @MessageTemplate LIKE '%activity execution%'
  AND @Timestamp > Now() - 24h
GROUP BY ActivityType
ORDER BY AVG(Duration) DESC
```

## Security

### Network Security
- Use HTTPS/TLS 1.3 for all external communications
- Implement API rate limiting and DDoS protection
- Use VPNs or private networks for database connections
- Regular security scanning of container images

### Application Security
```csharp
// Input validation
services.AddFluentValidation(fv => 
    fv.RegisterValidatorsFromAssemblyContaining<StartWorkflowCommandValidator>());

// CORS configuration
services.AddCors(options =>
{
    options.AddPolicy("Production", builder =>
        builder.WithOrigins("https://workflow-ui.prod.com")
               .AllowedHeaders("Content-Type", "Authorization")
               .AllowedMethods("GET", "POST", "PUT", "DELETE"));
});

// Authentication
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.prod.com";
        options.Audience = "workflow-api";
        options.RequireHttpsMetadata = true;
    });
```

### Data Protection
- Encrypt sensitive data at rest
- Use Azure Key Vault or AWS Secrets Manager for secrets
- Implement proper audit logging
- Regular security assessments and penetration testing

This operations guide provides comprehensive guidance for deploying, monitoring, and maintaining the Workflow Engine in production environments. For development guidance, see [WORKFLOW-DEVELOPER-GUIDE.md](./WORKFLOW-DEVELOPER-GUIDE.md). For architectural details, see [WORKFLOW-ARCHITECTURE.md](./WORKFLOW-ARCHITECTURE.md).