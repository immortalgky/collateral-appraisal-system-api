# Workflow Configuration Reference
## Complete Configuration Guide for Workflow Module

### Table of Contents
1. [Configuration Overview](#configuration-overview)
2. [WorkflowResilience Configuration](#workflowresilience-configuration)
3. [Database Configuration](#database-configuration)
4. [Background Services Configuration](#background-services-configuration)
5. [Activity Type Configuration](#activity-type-configuration)
6. [Environment-Specific Settings](#environment-specific-settings)
7. [Performance Tuning](#performance-tuning)

---

## Configuration Overview

The Workflow Module uses a hierarchical configuration system with validation and environment-specific overrides.

### Configuration Structure

```
appsettings.json
├── ConnectionStrings
│   └── Database (Required)
├── WorkflowResilience (Optional, has defaults)
│   ├── Retry
│   ├── CircuitBreaker  
│   ├── Timeout
│   └── RateLimit
├── MockSupervisor (Development only)
└── Logging (Standard ASP.NET Core)
```

### Configuration Hierarchy

1. **appsettings.json** (Base configuration)
2. **appsettings.{Environment}.json** (Environment overrides)
3. **Environment Variables** (Highest priority)
4. **User Secrets** (Development only)

---

## WorkflowResilience Configuration

### Complete Configuration Example

```json
{
  "WorkflowResilience": {
    "Retry": {
      "MaxRetryAttempts": 3,
      "BaseDelay": "00:00:02",
      "MaxDelay": "00:00:30",
      "Jitter": 0.1
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "BreakDuration": "00:01:00",
      "MinimumThroughput": 10,
      "SuccessThreshold": 0.8
    },
    "Timeout": {
      "DatabaseOperation": "00:00:30",
      "ExternalHttpCall": "00:02:00",
      "ActivityExecution": "00:05:00",
      "WorkflowStartup": "00:00:30",
      "WorkflowResume": "00:00:15"
    },
    "RateLimit": {
      "WorkflowStartsPerWindow": 100,
      "WindowDuration": "00:01:00",
      "MaxConcurrentWorkflows": 50
    }
  }
}
```

### Retry Policy Configuration

**Purpose**: Controls retry behavior for transient failures

```json
{
  "WorkflowResilience": {
    "Retry": {
      "MaxRetryAttempts": 3,        // Max retry attempts (0-10)
      "BaseDelay": "00:00:02",      // Initial delay between retries
      "MaxDelay": "00:00:30",       // Maximum delay (exponential backoff cap)
      "Jitter": 0.1                 // Randomization factor (0.0-1.0)
    }
  }
}
```

**Configuration Details**:

| Property | Type | Default | Range | Description |
|----------|------|---------|--------|-------------|
| `MaxRetryAttempts` | int | 3 | 0-10 | Maximum number of retry attempts before giving up |
| `BaseDelay` | TimeSpan | 2s | 1s-30s | Initial delay before first retry |
| `MaxDelay` | TimeSpan | 30s | 5s-300s | Maximum delay (exponential backoff cap) |
| `Jitter` | double | 0.1 | 0.0-1.0 | Jitter factor to prevent thundering herd |

**Exponential Backoff Formula**:
```
delay = min(BaseDelay * 2^attempt, MaxDelay) * (1 ± Jitter)
```

**Example Retry Sequence**:
- Attempt 1: 2s ± 10% = 1.8s - 2.2s
- Attempt 2: 4s ± 10% = 3.6s - 4.4s  
- Attempt 3: 8s ± 10% = 7.2s - 8.8s

### Circuit Breaker Configuration

**Purpose**: Protects external dependencies from cascading failures

```json
{
  "WorkflowResilience": {
    "CircuitBreaker": {
      "FailureThreshold": 5,        // Failures before opening circuit
      "BreakDuration": "00:01:00",  // How long to keep circuit open
      "MinimumThroughput": 10,      // Min requests before circuit can open
      "SuccessThreshold": 0.8       // Success rate to close circuit
    }
  }
}
```

**Configuration Details**:

| Property | Type | Default | Range | Description |
|----------|------|---------|--------|-------------|
| `FailureThreshold` | int | 5 | 1-20 | Consecutive failures before opening circuit |
| `BreakDuration` | TimeSpan | 1m | 10s-10m | Time to keep circuit open |
| `MinimumThroughput` | int | 10 | 5-100 | Minimum requests before circuit evaluation |
| `SuccessThreshold` | double | 0.8 | 0.5-1.0 | Success rate required to close circuit |

**Circuit States**:
- **Closed**: Normal operation, tracking failures
- **Open**: All requests fail fast, no calls made
- **Half-Open**: Limited requests allowed to test service recovery

### Timeout Configuration

**Purpose**: Prevents operations from hanging indefinitely

```json
{
  "WorkflowResilience": {
    "Timeout": {
      "DatabaseOperation": "00:00:30",    // Database query timeout
      "ExternalHttpCall": "00:02:00",     // External API call timeout
      "ActivityExecution": "00:05:00",    // Single activity timeout
      "WorkflowStartup": "00:00:30",      // Workflow initialization timeout
      "WorkflowResume": "00:00:15"        // Workflow resume timeout
    }
  }
}
```

**Configuration Details**:

| Property | Type | Default | Recommended | Description |
|----------|------|---------|-------------|-------------|
| `DatabaseOperation` | TimeSpan | 30s | 15s-60s | Timeout for database queries |
| `ExternalHttpCall` | TimeSpan | 2m | 30s-5m | Timeout for external API calls |
| `ActivityExecution` | TimeSpan | 5m | 1m-30m | Timeout for individual activity execution |
| `WorkflowStartup` | TimeSpan | 30s | 15s-60s | Timeout for workflow initialization |
| `WorkflowResume` | TimeSpan | 15s | 10s-30s | Timeout for workflow resumption |

### Rate Limiting Configuration

**Purpose**: Controls workflow creation rate to prevent system overload

```json
{
  "WorkflowResilience": {
    "RateLimit": {
      "WorkflowStartsPerWindow": 100,     // Max workflow starts per window
      "WindowDuration": "00:01:00",       // Rate limiting window
      "MaxConcurrentWorkflows": 50        // Max concurrent executions
    }
  }
}
```

**Configuration Details**:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `WorkflowStartsPerWindow` | int | 100 | Maximum workflow starts within time window |
| `WindowDuration` | TimeSpan | 1m | Time window for rate limiting |
| `MaxConcurrentWorkflows` | int | 50 | Maximum concurrent workflow executions |

---

## Database Configuration

### Connection String Configuration

```json
{
  "ConnectionStrings": {
    "Database": "Server=localhost;Database=WorkflowDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

### SQL Server Configuration Options

**Local Development**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=(localdb)\\mssqllocaldb;Database=WorkflowDb;Trusted_Connection=true;"
  }
}
```

**Production with Connection Pooling**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=prodserver;Database=WorkflowDb;User Id=workflowuser;Password=secretpass;MultipleActiveResultSets=true;TrustServerCertificate=true;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;"
  }
}
```

**Azure SQL Database**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=WorkflowDb;Persist Security Info=False;User ID=workflowuser;Password=secretpass;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

### Entity Framework Configuration

The module uses separate migration assemblies:

```csharp
// Workflow DbContext
services.AddDbContext<WorkflowDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
        sqlOptions.MigrationsAssembly("Workflow");
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "workflow");
    });
});

// Saga DbContext  
services.AddDbContext<AppraisalSagaDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("Workflow");
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
    });
});
```

---

## Background Services Configuration

### OutboxDispatcherService Configuration

**Processing Intervals**:
```csharp
// Built-in configuration (in service)
private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);
private const int BatchSize = 50;
private const int MaxRetryAttempts = 5;
```

**Custom Configuration** (future enhancement):
```json
{
  "BackgroundServices": {
    "OutboxDispatcher": {
      "ProcessingInterval": "00:00:30",
      "BatchSize": 50,
      "MaxRetryAttempts": 5,
      "DeadLetterThreshold": 10
    }
  }
}
```

### WorkflowTimerService Configuration

**Timer Processing**:
```csharp
// Built-in configuration
private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(1);
private readonly TimeSpan _workflowTimeout = TimeSpan.FromDays(30);
```

**Custom Configuration** (future enhancement):
```json
{
  "BackgroundServices": {
    "WorkflowTimer": {
      "ProcessingInterval": "00:01:00",
      "WorkflowTimeout": "30.00:00:00",
      "TimerAccuracy": "00:00:30"
    }
  }
}
```

### WorkflowCleanupService Configuration

**Cleanup Intervals**:
```csharp
// Built-in configuration
private readonly TimeSpan _processingInterval = TimeSpan.FromHours(24);
private readonly TimeSpan _completedWorkflowRetention = TimeSpan.FromDays(90);
private readonly TimeSpan _executionLogRetention = TimeSpan.FromDays(365);
```

**Custom Configuration** (future enhancement):
```json
{
  "BackgroundServices": {
    "WorkflowCleanup": {
      "ProcessingInterval": "1.00:00:00",
      "CompletedWorkflowRetentionDays": 90,
      "ExecutionLogRetentionDays": 365,
      "OutboxEventRetentionDays": 30
    }
  }
}
```

---

## Activity Type Configuration

### Built-in Activity Types

The system includes these activity types:

```csharp
public static class ActivityTypes
{
    public const string TaskActivity = "TaskActivity";
    public const string IfElseActivity = "IfElseActivity";
    public const string SwitchActivity = "SwitchActivity";
    public const string ServiceActivity = "ServiceActivity";
    public const string TimerActivity = "TimerActivity";
    public const string NotificationActivity = "NotificationActivity";
    public const string StartActivity = "StartActivity";
    public const string EndActivity = "EndActivity";
    public const string ForkActivity = "ForkActivity";
    public const string JoinActivity = "JoinActivity";
}

public static class AppraisalActivityTypes
{
    public const string RequestSubmission = "RequestSubmission";
    public const string AdminReview = "AdminReview";
    public const string StaffAssignment = "StaffAssignment";
    public const string AppraisalWork = "AppraisalWork";
    public const string CheckerReview = "CheckerReview";
    public const string VerifierReview = "VerifierReview";
    public const string CommitteeReview = "CommitteeReview";
}
```

### Activity Property Definitions

**TaskActivity Configuration**:
```json
{
  "type": "TaskActivity",
  "properties": {
    "assignee": "user@example.com",
    "dueDate": "2024-12-31T23:59:59Z",
    "priority": "High",
    "instructions": "Complete the task",
    "allowReassignment": true,
    "requiredFields": ["comments", "status"]
  }
}
```

**IfElseActivity Configuration**:
```json
{
  "type": "IfElseActivity",
  "properties": {
    "condition": "AppraisalValue > 500000",
    "trueBranch": "senior-review",
    "falseBranch": "standard-approval",
    "evaluateOnStart": true
  }
}
```

**TimerActivity Configuration**:
```json
{
  "type": "TimerActivity",
  "properties": {
    "duration": "24:00:00",
    "startImmediately": true,
    "cancelOnWorkflowComplete": true
  }
}
```

### Custom Activity Registration

```csharp
// Register custom activity in WorkflowActivityFactory
public IWorkflowActivity CreateActivity(string activityType)
{
    return activityType switch
    {
        ActivityTypes.TaskActivity => _serviceProvider.GetRequiredService<TaskActivity>(),
        "CustomAppraisal" => _serviceProvider.GetRequiredService<CustomAppraisalActivity>(),
        "ExternalValidation" => _serviceProvider.GetRequiredService<ExternalValidationActivity>(),
        _ => throw new ArgumentException($"Unknown activity type: {activityType}")
    };
}

// Register in DI container
services.AddScoped<CustomAppraisalActivity>();
```

---

## Environment-Specific Settings

### Development Configuration

**appsettings.Development.json**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=(localdb)\\mssqllocaldb;Database=WorkflowDb_Dev;Trusted_Connection=true;"
  },
  "WorkflowResilience": {
    "Retry": {
      "MaxRetryAttempts": 1,
      "BaseDelay": "00:00:01",
      "MaxDelay": "00:00:05"
    },
    "Timeout": {
      "DatabaseOperation": "00:01:00",
      "ExternalHttpCall": "00:03:00"
    },
    "RateLimit": {
      "WorkflowStartsPerWindow": 1000,
      "MaxConcurrentWorkflows": 100
    }
  },
  "MockSupervisor": {
    "Enabled": true,
    "SupervisorUserId": "supervisor@dev.com",
    "SupervisorName": "Dev Supervisor"
  },
  "Logging": {
    "LogLevel": {
      "Workflow": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### Staging Configuration

**appsettings.Staging.json**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=staging-sql;Database=WorkflowDb_Staging;User Id=workflowuser;Password=stagingpass;"
  },
  "WorkflowResilience": {
    "Retry": {
      "MaxRetryAttempts": 2,
      "BaseDelay": "00:00:01",
      "MaxDelay": "00:00:15"
    },
    "Timeout": {
      "ActivityExecution": "00:10:00"
    }
  },
  "Logging": {
    "LogLevel": {
      "Workflow": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Production Configuration

**appsettings.Production.json**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=prod-sql;Database=WorkflowDb;User Id=workflowuser;Password=prodpass;Max Pool Size=200;"
  },
  "WorkflowResilience": {
    "Retry": {
      "MaxRetryAttempts": 5,
      "BaseDelay": "00:00:03",
      "MaxDelay": "00:01:00",
      "Jitter": 0.2
    },
    "CircuitBreaker": {
      "FailureThreshold": 10,
      "BreakDuration": "00:05:00",
      "MinimumThroughput": 20
    },
    "Timeout": {
      "DatabaseOperation": "00:00:45",
      "ExternalHttpCall": "00:02:30",
      "ActivityExecution": "00:30:00"
    },
    "RateLimit": {
      "WorkflowStartsPerWindow": 500,
      "WindowDuration": "00:01:00",
      "MaxConcurrentWorkflows": 200
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Workflow": "Information",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  }
}
```

### Environment Variables

**For sensitive data**:
```bash
# Connection string
export ConnectionStrings__Database="Server=prod;Database=WorkflowDb;User Id=user;Password=secret;"

# Specific resilience settings
export WorkflowResilience__Retry__MaxRetryAttempts=3
export WorkflowResilience__Timeout__DatabaseOperation="00:00:30"

# Rate limiting
export WorkflowResilience__RateLimit__WorkflowStartsPerWindow=100
```

---

## Performance Tuning

### Database Performance

**Connection Pool Settings**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=server;Database=WorkflowDb;User Id=user;Password=pass;Max Pool Size=100;Min Pool Size=10;Connection Timeout=30;Command Timeout=120;"
  }
}
```

**Index Optimization**:
```sql
-- Key indexes for performance
CREATE INDEX IX_WorkflowInstance_Status ON WorkflowInstance(Status);
CREATE INDEX IX_WorkflowInstance_CorrelationId ON WorkflowInstance(CorrelationId);
CREATE INDEX IX_WorkflowBookmark_DueAt ON WorkflowBookmark(DueAt) WHERE IsConsumed = 0;
CREATE INDEX IX_WorkflowOutbox_Status_NextAttemptAt ON WorkflowOutbox(Status, NextAttemptAt);
CREATE INDEX IX_WorkflowExecutionLog_WorkflowInstanceId_At ON WorkflowExecutionLog(WorkflowInstanceId, At);
```

### Memory and CPU Settings

**High-Throughput Configuration**:
```json
{
  "WorkflowResilience": {
    "RateLimit": {
      "WorkflowStartsPerWindow": 1000,
      "WindowDuration": "00:01:00",
      "MaxConcurrentWorkflows": 500
    },
    "Timeout": {
      "DatabaseOperation": "00:00:15",
      "ActivityExecution": "00:15:00"
    }
  }
}
```

**Background Service Tuning**:
```csharp
// Reduce processing intervals for high throughput
private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10); // OutboxDispatcher
private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30); // WorkflowTimer
```

### Monitoring Configuration

**Application Insights Integration**:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key"
  },
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Workflow": "Information"
      }
    }
  }
}
```

**Health Checks**:
```csharp
services.AddHealthChecks()
    .AddDbContext<WorkflowDbContext>()
    .AddCheck<WorkflowEngineHealthCheck>("workflow-engine")
    .AddCheck<OutboxDispatcherHealthCheck>("outbox-dispatcher");
```

---

## Configuration Validation

### Built-in Validation

All configuration classes implement validation:

```csharp
public class WorkflowResilienceOptions
{
    public void Validate()
    {
        Retry?.Validate();
        CircuitBreaker?.Validate();
        Timeout?.Validate();
        RateLimit?.Validate();
    }
}

public class RetryPolicyOptions
{
    public void Validate()
    {
        if (MaxRetryAttempts < 0)
            throw new InvalidOperationException("MaxRetryAttempts cannot be negative");
            
        if (BaseDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("BaseDelay must be positive");
    }
}
```

### Configuration Testing

```csharp
[Fact]
public void Configuration_Should_Be_Valid()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();
        
    // Act & Assert
    var options = configuration.GetSection(WorkflowResilienceOptions.SectionName)
        .Get<WorkflowResilienceOptions>();
        
    Assert.DoesNotThrow(() => options.Validate());
}
```

---

## Configuration Best Practices

### 1. Environment Separation
- Use different databases for each environment
- Adjust timeouts based on environment performance
- Use appropriate retry counts (fewer in dev, more in prod)

### 2. Security
- Never store secrets in appsettings.json
- Use Azure Key Vault, AWS Secrets Manager, or User Secrets for sensitive data
- Rotate connection string passwords regularly

### 3. Performance
- Start with conservative timeout values and adjust based on monitoring
- Monitor circuit breaker metrics to tune thresholds
- Adjust background service intervals based on workload

### 4. Monitoring
- Enable structured logging with correlation IDs
- Set up alerts for circuit breaker trips
- Monitor outbox processing lag

### 5. Maintenance
- Regularly review and adjust retention periods
- Monitor database growth and adjust cleanup schedules
- Update configuration based on production metrics

This comprehensive configuration reference covers all aspects of configuring the Workflow Module for different environments and use cases.