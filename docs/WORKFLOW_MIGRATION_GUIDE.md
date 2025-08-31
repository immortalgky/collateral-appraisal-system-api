# Workflow Engine Migration Guide

This guide helps you migrate from the legacy workflow system to the new Advanced Workflow Engine with enhanced features and improved architecture.

## 🚀 Quick Migration Checklist

- [ ] Update workflow JSON definitions
- [ ] Replace custom assignment logic with services
- [ ] Configure lifecycle actions
- [ ] Update API endpoints
- [ ] Test assignment strategies
- [ ] Validate action execution
- [ ] Update monitoring/logging
- [ ] Deploy and monitor

## 📋 Breaking Changes

### 1. Assignment Strategy Format

**Before (Legacy)**
```json
{
  "id": "review-task",
  "type": "TaskActivity", 
  "assignmentStrategy": "supervisor"
}
```

**After (New)**  
```json
{
  "id": "review-task",
  "type": "TaskActivity",
  "assignmentStrategies": ["supervisor", "manual"]
}
```

**Migration Steps:**
1. Change `assignmentStrategy` to `assignmentStrategies`
2. Convert string value to array format
3. Add fallback strategies (recommended: always include "manual")

### 2. Custom Assignment Logic

**Before (Legacy)**
```csharp
// Custom logic embedded in activities
public class CustomTaskActivity : TaskActivity
{
    protected override async Task<string> ResolveAssigneeAsync(...)
    {
        // Custom assignment logic here
        return await GetClientPreferredAppraiser();
    }
}
```

**After (New)**
```csharp
// Dedicated assignment service
public class ClientPreferenceAssignmentService : ICustomAssignmentService
{
    public string ServiceName => "client-preference";
    
    public async Task<CustomAssignmentResult> AssignAsync(
        AssignmentContext context, 
        CancellationToken cancellationToken = default)
    {
        var preferredAssignee = await GetClientPreferredAppraiser();
        return CustomAssignmentResult.Success(preferredAssignee);
    }
}
```

**Migration Steps:**
1. Extract assignment logic into `ICustomAssignmentService` implementations
2. Register services in DI container
3. Update workflow definitions to use service names
4. Remove custom activity classes if they only contained assignment logic

### 3. Lifecycle Actions

**Before (Legacy)**
```csharp
// Custom code in activity methods
protected override async Task<ActivityResult> ExecuteInternalAsync(...)
{
    // Send notification
    await notificationService.SendAsync(...);
    
    // Update entity
    await entityService.UpdateStatusAsync(...);
    
    // Log audit
    await auditService.LogAsync(...);
    
    return ActivityResult.Completed(result);
}
```

**After (New)**
```json
{
  "onStart": [
    {
      "type": "SendNotificationAction",
      "name": "notify-assignee",
      "configuration": {
        "template": "task-assigned",
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
    },
    {
      "type": "CreateAuditEntryAction",
      "name": "log-completion",
      "configuration": {
        "category": "Operational",
        "action": "TaskCompleted"
      }
    }
  ]
}
```

**Migration Steps:**
1. Identify lifecycle code in existing activities
2. Convert to declarative action configurations
3. Remove custom code from activity implementations
4. Test action execution

## 🔧 Step-by-Step Migration

### Step 1: Update Workflow Definitions

1. **Convert Assignment Strategy Format**
   ```bash
   # Find all workflow JSON files
   find . -name "*.json" -type f | grep -E "(workflow|activity)"
   ```

   Update each file:
   ```json
   // Change this
   "assignmentStrategy": "supervisor"
   
   // To this  
   "assignmentStrategies": ["supervisor", "manual"]
   ```

2. **Add Action Configurations**
   
   Identify common patterns and convert:
   ```json
   {
     "id": "existing-activity",
     "type": "TaskActivity",
     "assignmentStrategies": ["supervisor"],
     "onStart": [
       {
         "type": "SendNotificationAction",
         "name": "notify-start", 
         "configuration": {
           "template": "activity-started",
           "recipientExpression": "${assignee.email}"
         }
       }
     ],
     "onComplete": [
       {
         "type": "UpdateEntityStatusAction",
         "name": "update-entity",
         "configuration": {
           "entityType": "YourEntity",
           "status": "Processed"
         }
       }
     ]
   }
   ```

### Step 2: Implement Custom Assignment Services

1. **Create Service Implementations**
   ```csharp
   // Example: Convert existing assignment logic
   public class ExistingBusinessRulesAssignmentService : ICustomAssignmentService
   {
       public string ServiceName => "existing-business-rules";
       
       public async Task<CustomAssignmentResult> AssignAsync(
           AssignmentContext context, 
           CancellationToken cancellationToken = default)
       {
           // Port your existing assignment logic here
           var assignee = await YourExistingLogic(context);
           
           if (assignee != null)
           {
               return CustomAssignmentResult.Success(
                   assignee.Id, 
                   $"Assigned via business rules to {assignee.Name}");
           }
           
           return CustomAssignmentResult.Skip("No suitable assignee found");
       }
   }
   ```

2. **Register Services**
   ```csharp
   // In your module or startup configuration
   services.AddScoped<ICustomAssignmentService, ExistingBusinessRulesAssignmentService>();
   ```

3. **Update Workflow Definitions**
   ```json
   {
     "assignmentStrategies": [
       "existing-business-rules",
       "supervisor", 
       "manual"
     ]
   }
   ```

### Step 3: Migrate API Endpoints

**Before (Legacy)**
```csharp
[HttpPost("start")]
public async Task<IActionResult> StartWorkflow(StartWorkflowRequest request)
{
    var result = await workflowEngine.StartAsync(
        request.WorkflowId, 
        request.Data);
    return Ok(result);
}
```

**After (New)**
```csharp
[HttpPost("start")]  
public async Task<IActionResult> StartWorkflow(StartWorkflowRequest request)
{
    var result = await workflowEngine.StartWorkflowAsync(
        request.WorkflowId,
        request.Data,
        request.AssignmentOverrides, // New parameter
        HttpContext.RequestAborted);
    return Ok(result);
}
```

Update request models:
```csharp
public class StartWorkflowRequest
{
    public string WorkflowId { get; set; } = default!;
    public object Data { get; set; } = default!;
    public Dictionary<string, object>? AssignmentOverrides { get; set; } // New
}
```

### Step 4: Update Tests

1. **Test Assignment Strategy Migration**
   ```csharp
   [Test]
   public async Task Should_Handle_Multiple_Assignment_Strategies()
   {
       // Arrange
       var workflowDef = new WorkflowDefinition
       {
           Activities = new[]
           {
               new ActivityDefinition
               {
                   Id = "test-activity",
                   AssignmentStrategies = new[] { "custom-service", "supervisor", "manual" }
               }
           }
       };
       
       // Act & Assert
       var result = await workflowEngine.StartWorkflowAsync("test", new { });
       result.Should().NotBeNull();
   }
   ```

2. **Test Action Execution**
   ```csharp
   [Test] 
   public async Task Should_Execute_Lifecycle_Actions()
   {
       // Setup activity with actions
       var activity = CreateActivityWithActions();
       
       // Execute activity
       var result = await activity.ExecuteAsync(context);
       
       // Verify actions were executed
       mockActionExecutor.Verify(x => x.ExecuteLifecycleActionsAsync(...));
   }
   ```

## 🔍 Common Migration Issues

### Issue 1: Assignment Strategy Not Found

**Error:** `Assignment strategy 'old-strategy' not recognized`

**Solution:**
1. Check if strategy name matches a registered service
2. Ensure custom services are properly registered in DI
3. Add fallback strategies

**Fix:**
```json
{
  "assignmentStrategies": [
    "your-custom-service", // Ensure this matches ServiceName
    "supervisor",          // Built-in fallback
    "manual"              // Always include as final fallback
  ]
}
```

### Issue 2: Action Configuration Errors

**Error:** `Action type 'CustomAction' not supported`

**Solution:**
1. Use only supported action types
2. Check action configuration syntax
3. Validate expression syntax

**Fix:**
```json
{
  "onComplete": [
    {
      "type": "UpdateEntityStatusAction", // Use supported types
      "name": "update-status",
      "configuration": {
        "entityType": "MyEntity",
        "status": "Completed"
      }
    }
  ]
}
```

### Issue 3: Expression Evaluation Errors

**Error:** `Expression '${invalid.property}' evaluation failed`

**Solution:**
1. Check expression syntax
2. Ensure referenced variables exist
3. Use proper expression functions

**Fix:**
```json
{
  "configuration": {
    "recipientExpression": "${assignee.email}",  // Correct
    "value": "${workflowVariables.StartTime}"    // Access workflow data
  }
}
```

## ✅ Testing Your Migration

### 1. Workflow Definition Validation
```bash
# Test workflow JSON parsing
dotnet test --filter "Category=WorkflowDefinition"
```

### 2. Assignment Strategy Testing
```bash  
# Test assignment resolution
dotnet test --filter "Category=Assignment"
```

### 3. Action Execution Testing
```bash
# Test action framework
dotnet test --filter "Category=Actions" 
```

### 4. End-to-End Testing
```bash
# Full workflow execution
dotnet test --filter "Category=Integration"
```

## 📊 Migration Verification

### Pre-Migration Checklist
- [ ] Document existing workflow behaviors
- [ ] Identify all custom assignment logic
- [ ] List all lifecycle operations
- [ ] Note external service dependencies
- [ ] Backup existing workflow definitions

### Post-Migration Validation
- [ ] All workflows start successfully
- [ ] Assignment strategies resolve correctly
- [ ] Actions execute as expected
- [ ] Error handling works properly
- [ ] Performance is acceptable
- [ ] Monitoring/logging captures events
- [ ] External integrations function correctly

### Performance Comparison
```csharp
// Monitor key metrics before and after migration
- Workflow start time
- Activity assignment time  
- Action execution duration
- Overall workflow completion time
- System resource usage
```

## 🚀 Rollback Strategy

If issues arise during migration:

1. **Immediate Rollback**
   - Restore previous workflow definitions
   - Redeploy previous version
   - Monitor for stability

2. **Gradual Migration**
   - Migrate workflows incrementally
   - Run new and old systems in parallel
   - Route traffic based on workflow ID

3. **Feature Flags**
   ```csharp
   if (featureFlags.IsEnabled("NewWorkflowEngine"))
   {
       return await newWorkflowEngine.StartAsync(...);
   }
   return await legacyWorkflowEngine.StartAsync(...);
   ```

## 🎯 Success Metrics

Track these metrics to verify successful migration:

- **Functionality:** All workflows execute correctly
- **Performance:** Response times within acceptable ranges  
- **Reliability:** Error rates remain low or improve
- **Maintainability:** Reduced custom code, improved configurability
- **Monitoring:** Better visibility into workflow execution

## 🤝 Getting Help

If you encounter issues during migration:

1. Check the troubleshooting section in the main guide
2. Review error logs and audit trails
3. Test individual components in isolation
4. Reach out to the development team with specific error details

Remember: Migration is a journey, not a destination. Take it step by step, test thoroughly, and don't hesitate to ask for help when needed!