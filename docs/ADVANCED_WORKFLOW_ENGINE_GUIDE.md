# Advanced Workflow Engine - Complete Guide

Welcome to the comprehensive guide for the Advanced Workflow Engine! This powerful system provides enterprise-grade workflow orchestration with resilience patterns, audit logging, custom assignment strategies, and action execution capabilities.

## 🏗️ Architecture Overview

The workflow engine is built with a modular architecture consisting of several key components:

```
Workflow Engine
├── Core Components
│   ├── IWorkflowEngine - Main engine interface
│   ├── WorkflowEngine - Core orchestration logic
│   ├── IFlowControlManager - Activity flow management
│   └── IWorkflowLifecycleManager - Workflow lifecycle operations
├── Activities
│   ├── TaskActivity - Human task activities
│   ├── StartActivity - Workflow initiation
│   ├── EndActivity - Workflow completion
│   ├── ForkActivity - Parallel execution
│   ├── JoinActivity - Parallel merge
│   ├── IfElseActivity - Conditional branching
│   └── SwitchActivity - Multi-branch decisions
├── Assignment System
│   ├── ICustomAssignmentService - Custom assignment logic
│   ├── Assignment Registry - Service discovery
│   └── Runtime Overrides - Dynamic assignment changes
├── Action Framework
│   ├── IWorkflowActionExecutor - Action orchestration
│   ├── Core Actions - Entity updates, audit, variables
│   ├── Communication Actions - Notifications, events, webhooks
│   └── ConditionalAction - Logic-based execution
├── Resilience & Monitoring
│   ├── IWorkflowResilienceService - Circuit breakers, retries
│   ├── IWorkflowDegradationService - Graceful fallbacks
│   └── IWorkflowAuditService - Comprehensive logging
└── Expression Engine
    ├── IExpressionEvaluator - Dynamic expression evaluation
    └── Expression Context - Variable and data access
```

## 🚀 Getting Started

### 1. Basic Workflow Definition

Define workflows using JSON schema:

```json
{
  "workflowId": "appraisal-review",
  "name": "Property Appraisal Review",
  "version": "1.0",
  "activities": [
    {
      "id": "start",
      "type": "StartActivity",
      "name": "Begin Review Process",
      "nextActivities": ["initial-review"]
    },
    {
      "id": "initial-review",
      "type": "TaskActivity",
      "name": "Initial Appraisal Review",
      "assignmentStrategies": ["supervisor", "manual"],
      "timeoutMinutes": 1440,
      "onStart": [
        {
          "type": "SendNotificationAction",
          "name": "notify-assignee",
          "configuration": {
            "template": "appraisal-assigned",
            "recipientExpression": "${assignee.email}"
          }
        }
      ],
      "onComplete": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "update-status",
          "configuration": {
            "entityType": "Appraisal",
            "status": "Under Review"
          }
        }
      ],
      "nextActivities": ["quality-check"]
    }
  ]
}
```

### 2. Starting a Workflow

```csharp
// Basic workflow start
var result = await workflowEngine.StartWorkflowAsync(
    "appraisal-review", 
    new { AppraisalId = 123, ClientId = 456 },
    cancellationToken);

// With runtime overrides
var overrides = new Dictionary<string, object>
{
    ["initial-review"] = new { AssigneeId = "specific-user-id" }
};

var resultWithOverrides = await workflowEngine.StartWorkflowAsync(
    "appraisal-review", 
    new { AppraisalId = 123, ClientId = 456 },
    overrides,
    cancellationToken);
```

### 3. Completing Activities

```csharp
// Complete with result data
await workflowEngine.CompleteActivityAsync(
    workflowInstanceId,
    "initial-review",
    new { Decision = "Approved", Comments = "Looks good!" },
    cancellationToken);

// Complete with assignment override for next activity
var nextOverrides = new Dictionary<string, object>
{
    ["quality-check"] = new { AssigneeId = "qa-specialist-id" }
};

await workflowEngine.CompleteActivityAsync(
    workflowInstanceId,
    "initial-review",
    new { Decision = "Approved" },
    nextOverrides,
    cancellationToken);
```

## 🎯 Assignment Strategies

The engine supports sophisticated assignment strategies with custom services:

### Built-in Strategies
- `supervisor` - Assigns to user's supervisor
- `manual` - Manual assignment required
- `previous-owner` - Assigns to previous task owner

### Custom Assignment Services

Implement `ICustomAssignmentService` for business-specific logic:

```csharp
public class ClientPreferenceAssignmentService : ICustomAssignmentService
{
    public string ServiceName => "client-preference";

    public async Task<CustomAssignmentResult> AssignAsync(
        AssignmentContext context, 
        CancellationToken cancellationToken = default)
    {
        // Get client's preferred appraiser
        var clientId = context.WorkflowVariables.GetValue<int>("ClientId");
        var preferredAssignee = await GetClientPreferredAppraiser(clientId);
        
        if (preferredAssignee?.IsAvailable == true)
        {
            return CustomAssignmentResult.Success(
                preferredAssignee.UserId, 
                $"Assigned to client's preferred appraiser: {preferredAssignee.Name}");
        }
        
        return CustomAssignmentResult.Skip("Preferred appraiser not available");
    }
}
```

Register custom services:

```csharp
// In AssignmentModule or Startup
services.AddScoped<ICustomAssignmentService, ClientPreferenceAssignmentService>();
services.AddScoped<ICustomAssignmentService, BusinessRulesAssignmentService>();
```

## ⚡ Action Framework

Execute actions at activity lifecycle events:

### Core Actions

```json
{
  "onStart": [
    {
      "type": "SetWorkflowVariableAction",
      "name": "set-start-time",
      "configuration": {
        "variableName": "ActivityStartTime",
        "value": "${now()}"
      }
    }
  ],
  "onComplete": [
    {
      "type": "UpdateEntityStatusAction",
      "name": "update-appraisal",
      "configuration": {
        "entityType": "Appraisal",
        "entityIdExpression": "${workflowVariables.AppraisalId}",
        "status": "Reviewed",
        "additionalData": {
          "reviewedBy": "${assignee.id}",
          "reviewedAt": "${now()}"
        }
      }
    }
  ]
}
```

### Communication Actions

```json
{
  "onComplete": [
    {
      "type": "SendNotificationAction",
      "name": "notify-stakeholders",
      "configuration": {
        "template": "review-completed",
        "recipientExpression": "${workflowVariables.StakeholderEmails}",
        "data": {
          "appraisalId": "${workflowVariables.AppraisalId}",
          "decision": "${activityOutput.Decision}"
        }
      }
    },
    {
      "type": "PublishEventAction",
      "name": "publish-completion",
      "configuration": {
        "eventType": "AppraisalReviewCompleted",
        "eventData": {
          "workflowInstanceId": "${workflowInstanceId}",
          "activityId": "${activityId}",
          "result": "${activityOutput}"
        }
      }
    }
  ]
}
```

### Conditional Actions

```json
{
  "onComplete": [
    {
      "type": "ConditionalAction",
      "name": "escalate-if-rejected",
      "condition": "${activityOutput.Decision == 'Rejected'}",
      "thenActions": [
        {
          "type": "SendNotificationAction",
          "name": "escalate-notification",
          "configuration": {
            "template": "escalation-required",
            "recipientExpression": "${supervisor.email}"
          }
        }
      ],
      "elseActions": [
        {
          "type": "CreateAuditEntryAction",
          "name": "log-approval",
          "configuration": {
            "category": "Operational",
            "action": "ApprovalGranted",
            "description": "Appraisal review approved by ${assignee.name}"
          }
        }
      ]
    }
  ]
}
```

## 🛡️ Resilience & Error Handling

The engine includes comprehensive resilience patterns:

### Circuit Breaker Protection

```csharp
// Automatic circuit breaker for external services
await resilienceService.ExecuteWithCircuitBreakerAsync(
    "external-api-calls",
    async () => await externalApiService.ValidateData(),
    cancellationToken);
```

### Retry Policies

```csharp
// Configurable retry with exponential backoff
await resilienceService.ExecuteWithRetryAsync(
    async () => await uncertainOperation(),
    retryCount: 3,
    baseDelay: TimeSpan.FromSeconds(1),
    cancellationToken);
```

### Graceful Degradation

```csharp
// Fallback when external services fail
var result = await degradationService.ExecuteWithFallbackAsync(
    primaryOperation: () => externalService.GetData(),
    fallbackOperation: () => localCache.GetData(),
    "external-data-service",
    cancellationToken);
```

## 📊 Audit & Monitoring

Comprehensive audit logging captures all workflow activities:

### Structured Logging

```csharp
// Automatic audit logging for all activities
await auditService.LogActivityEventAsync(
    context,
    ActivityAuditEventType.ActivityCompleted,
    WorkflowAuditSeverity.Information,
    $"Activity {context.ActivityId} completed successfully",
    userId: currentUser.Id,
    additionalData: new Dictionary<string, object>
    {
        ["duration"] = executionTime.TotalMilliseconds,
        ["result"] = activityResult
    });
```

### Performance Metrics

```csharp
// Performance tracking
await auditService.LogPerformanceMetricsAsync(
    workflowInstanceId,
    activityId,
    "ActivityExecution",
    duration,
    successful: true,
    metrics: new Dictionary<string, object>
    {
        ["memoryUsage"] = GC.GetTotalMemory(false),
        ["actionCount"] = executedActions.Count
    });
```

## 🔧 Configuration Examples

### Complete Activity Configuration

```json
{
  "id": "advanced-review",
  "type": "TaskActivity",
  "name": "Advanced Quality Review",
  "assignmentStrategies": [
    "client-preference",
    "business-rules", 
    "supervisor",
    "manual"
  ],
  "timeoutMinutes": 2880,
  "configuration": {
    "requiresApproval": true,
    "minimumExperience": 5,
    "certificationRequired": "Licensed Appraiser"
  },
  "onStart": [
    {
      "type": "SetWorkflowVariableAction",
      "name": "track-start",
      "configuration": {
        "variableName": "ReviewStartTime",
        "value": "${now()}"
      }
    },
    {
      "type": "SendNotificationAction", 
      "name": "notify-assignment",
      "configuration": {
        "template": "advanced-review-assigned",
        "recipientExpression": "${assignee.email}",
        "data": {
          "dueDate": "${addDays(now(), 2)}",
          "priority": "High"
        }
      }
    }
  ],
  "onComplete": [
    {
      "type": "ConditionalAction",
      "name": "handle-result",
      "condition": "${activityOutput.QualityScore >= 85}",
      "thenActions": [
        {
          "type": "UpdateEntityStatusAction",
          "name": "approve-appraisal",
          "configuration": {
            "entityType": "Appraisal",
            "status": "Approved",
            "additionalData": {
              "qualityScore": "${activityOutput.QualityScore}",
              "reviewedBy": "${assignee.id}"
            }
          }
        }
      ],
      "elseActions": [
        {
          "type": "PublishEventAction",
          "name": "trigger-rework",
          "configuration": {
            "eventType": "AppraisalReworkRequired",
            "eventData": {
              "reasons": "${activityOutput.Issues}",
              "qualityScore": "${activityOutput.QualityScore}"
            }
          }
        }
      ]
    }
  ],
  "onError": [
    {
      "type": "CreateAuditEntryAction",
      "name": "log-error",
      "configuration": {
        "category": "System",
        "action": "ActivityError",
        "description": "Advanced review activity failed: ${error.message}"
      }
    }
  ],
  "nextActivities": ["final-approval", "rework-required"]
}
```

## 🚀 Advanced Features

### Flow Control Activities

#### Fork/Join for Parallel Processing
```json
{
  "id": "parallel-reviews",
  "type": "ForkActivity",
  "name": "Parallel Quality Reviews",
  "branches": [
    {
      "name": "technical-review",
      "activities": ["technical-validation"]
    },
    {
      "name": "compliance-review", 
      "activities": ["compliance-check"]
    }
  ],
  "nextActivities": ["join-reviews"]
},
{
  "id": "join-reviews",
  "type": "JoinActivity",
  "name": "Merge Review Results",
  "waitForAll": true,
  "nextActivities": ["final-decision"]
}
```

#### Switch Activity for Multi-Path Logic
```json
{
  "id": "risk-assessment",
  "type": "SwitchActivity",
  "name": "Risk-Based Routing",
  "switchExpression": "${workflowVariables.RiskLevel}",
  "cases": [
    {
      "value": "Low",
      "activities": ["automated-approval"]
    },
    {
      "value": "Medium", 
      "activities": ["standard-review"]
    },
    {
      "value": "High",
      "activities": ["enhanced-review", "manager-approval"]
    }
  ],
  "defaultCase": {
    "activities": ["manual-assessment"]
  }
}
```

### Expression System

The engine supports a rich expression language:

```javascript
// Variable access
${workflowVariables.PropertyValue}
${activityOutput.Decision}
${assignee.email}

// Function calls  
${now()}
${addDays(now(), 7)}
${formatCurrency(workflowVariables.PropertyValue)}

// Conditional expressions
${workflowVariables.PropertyValue > 1000000 ? 'High Value' : 'Standard'}

// Collection operations
${workflowVariables.ReviewerEmails.join(', ')}
${workflowVariables.Issues.length > 0}
```

## 🔒 Security & Compliance

### Security Event Logging
```csharp
await auditService.LogSecurityEventAsync(
    workflowInstanceId,
    activityId,
    SecurityEventType.SensitiveDataAccessed,
    $"Financial data accessed during appraisal review",
    userId: currentUser.Id,
    securityContext: new Dictionary<string, object>
    {
        ["dataType"] = "Financial",
        ["accessLevel"] = "Confidential",
        ["justification"] = "Required for appraisal validation"
    });
```

### Audit Trail Categories
- System: Automated system actions
- User: User-initiated actions  
- Administrative: Admin configuration changes
- Security: Security-related events
- Compliance: Regulatory compliance actions
- Financial: Financial data access
- Operational: Business process events
- Technical: System performance and errors

## 📈 Performance Optimization

### Bulk Operations
```csharp
// Execute multiple actions efficiently
var actions = new List<IWorkflowAction> { /* actions */ };
var result = await actionExecutor.ExecuteBatchAsync(
    context, 
    actions, 
    cancellationToken);
```

### Caching Strategy
The engine automatically caches:
- Workflow definitions
- Assignment service results
- Expression evaluation results
- Configuration data

### Monitoring Metrics
- Activity execution duration
- Assignment resolution time
- Action execution success rates
- Circuit breaker status
- Memory and CPU utilization

## 🛠️ Migration Guide

### From Legacy System

1. **Update Activity Definitions**
   ```json
   // Old format
   {
     "assignmentStrategy": "supervisor"
   }
   
   // New format  
   {
     "assignmentStrategies": ["supervisor", "manual"]
   }
   ```

2. **Replace Custom Logic with Actions**
   ```csharp
   // Old approach - custom code in activities
   protected override async Task<ActivityResult> ExecuteInternalAsync(...)
   {
       await UpdateAppraisalStatus();
       await SendNotifications();
       return ActivityResult.Completed();
   }
   
   // New approach - declarative actions
   // Define in workflow JSON, no code changes needed
   ```

3. **Implement Custom Assignment Services**
   ```csharp
   // Replace hard-coded assignment logic
   public class LegacyAssignmentService : ICustomAssignmentService
   {
       // Wrap existing logic in new interface
   }
   ```

### Breaking Changes
- `assignmentStrategy` (string) → `assignmentStrategies` (string[])
- Custom activities must implement new lifecycle methods
- Audit logging moved to dedicated service
- Configuration format updates for actions

## 🎓 Best Practices

1. **Activity Design**
   - Keep activities focused on single responsibilities
   - Use descriptive names and IDs
   - Configure appropriate timeouts
   - Handle errors gracefully

2. **Assignment Strategies**
   - Order strategies by preference/priority
   - Always include "manual" as fallback
   - Implement custom services for complex logic
   - Test assignment scenarios thoroughly

3. **Action Configuration**
   - Use conditional actions for decision logic
   - Keep action configurations declarative
   - Test action execution in isolation
   - Monitor action performance

4. **Error Handling**
   - Configure onError actions for critical paths
   - Use circuit breakers for external calls
   - Implement graceful degradation
   - Log errors with sufficient context

5. **Performance**
   - Use parallel activities where appropriate
   - Monitor memory usage in long workflows
   - Cache frequently accessed data
   - Set reasonable timeouts

## 🔍 Troubleshooting

### Common Issues

**Assignment Failures**
```
Problem: Activities stuck without assignees
Solution: Check assignment strategy configuration and service availability
Debug: Review assignment audit logs
```

**Action Execution Errors** 
```
Problem: Actions failing during execution
Solution: Validate action configuration and dependencies
Debug: Check action-specific error logs and performance metrics
```

**Performance Degradation**
```
Problem: Slow workflow execution
Solution: Review circuit breaker status and optimize expressions
Debug: Analyze performance audit logs and system metrics
```

### Debug Tools

Enable detailed logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Assignment.Workflow": "Debug",
      "Assignment.Workflow.Engine": "Trace"
    }
  }
}
```

Monitor workflow state:
```csharp
var state = await workflowEngine.GetWorkflowStateAsync(instanceId);
var activities = await workflowEngine.GetActivityHistoryAsync(instanceId);
```

This comprehensive guide covers the full capabilities of your advanced workflow engine. The system provides enterprise-grade workflow orchestration with modern patterns for resilience, observability, and extensibility!