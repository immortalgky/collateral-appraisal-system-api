# Workflow Engine Developer Guide

## Quick Start

### 1. Basic Workflow Execution

```csharp
// Inject the workflow service
public class MyController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    
    public MyController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }
    
    [HttpPost("start-approval")]
    public async Task<IActionResult> StartApprovalWorkflow([FromBody] ApprovalRequest request)
    {
        var result = await _workflowService.StartWorkflowAsync(
            workflowDefinitionId: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            instanceName: $"Approval-{request.RequestId}",
            startedBy: User.Identity.Name,
            initialVariables: new Dictionary<string, object>
            {
                ["RequestId"] = request.RequestId,
                ["Amount"] = request.Amount,
                ["RequiresManagerApproval"] = request.Amount > 10000
            },
            correlationId: request.RequestId.ToString()
        );
        
        return Ok(new { WorkflowId = result.WorkflowInstance?.Id, Status = result.Status });
    }
}
```

### 2. Resuming Workflows (Activity Completion)

```csharp
[HttpPost("complete-activity")]
public async Task<IActionResult> CompleteActivity([FromBody] CompleteActivityRequest request)
{
    var command = new CompleteActivityCommand(
        WorkflowInstanceId: request.WorkflowId,
        ActivityId: request.ActivityId,
        CompletedBy: User.Identity.Name,
        OutputData: request.OutputData,
        BookmarkKey: request.BookmarkKey // Optional - for idempotent operations
    );
    
    var result = await _mediator.Send(command);
    
    return Ok(new 
    { 
        Success = result.Success,
        NextActivity = result.NextActivityId,
        WorkflowCompleted = result.WorkflowCompleted
    });
}
```

## Creating Custom Activities

### 1. Basic Activity Structure

```csharp
public class EmailNotificationActivity : WorkflowActivity
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailNotificationActivity> _logger;
    
    public EmailNotificationActivity(IEmailService emailService, ILogger<EmailNotificationActivity> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }
    
    public override async Task<ActivityResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get inputs from workflow variables
            var recipient = context.GetVariable<string>("RecipientEmail");
            var subject = context.GetVariable<string>("EmailSubject");
            var body = context.GetVariable<string>("EmailBody");
            
            // Validate required inputs
            if (string.IsNullOrEmpty(recipient))
            {
                return ActivityResult.Failed("RecipientEmail is required");
            }
            
            // Send email
            await _emailService.SendEmailAsync(recipient, subject, body, cancellationToken);
            
            _logger.LogInformation("Email sent to {Recipient} for workflow {WorkflowId}", 
                recipient, context.WorkflowInstanceId);
            
            // Return success with output data
            return ActivityResult.Completed(new Dictionary<string, object>
            {
                ["EmailSentAt"] = DateTime.UtcNow,
                ["EmailSentTo"] = recipient
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for workflow {WorkflowId}", context.WorkflowInstanceId);
            return ActivityResult.Failed($"Email sending failed: {ex.Message}");
        }
    }
}
```

### 2. Long-Running Activity (Human Task)

```csharp
public class ManagerApprovalActivity : WorkflowActivity
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly INotificationService _notificationService;
    
    public ManagerApprovalActivity(
        IWorkflowBookmarkService bookmarkService,
        INotificationService notificationService)
    {
        _bookmarkService = bookmarkService;
        _notificationService = notificationService;
    }
    
    public override async Task<ActivityResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var managerId = context.GetVariable<string>("ManagerId");
        var requestAmount = context.GetVariable<decimal>("RequestAmount");
        
        // Send notification to manager
        await _notificationService.NotifyManagerAsync(managerId, 
            $"Approval required for request of ${requestAmount:N2}", cancellationToken);
        
        // Create bookmark for user action - this will pause the workflow
        var bookmarkKey = $"approval_{context.WorkflowInstanceId}_{Guid.NewGuid():N}";
        
        await _bookmarkService.CreateUserActionBookmarkAsync(
            context.WorkflowInstanceId,
            context.ActivityDefinition.Id,
            bookmarkKey,
            context.WorkflowInstance.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(new { RequestAmount = requestAmount }),
            cancellationToken
        );
        
        // Return pending - workflow will pause here
        return ActivityResult.Pending(new Dictionary<string, object>
        {
            ["BookmarkKey"] = bookmarkKey,
            ["NotificationSentAt"] = DateTime.UtcNow,
            ["AssignedTo"] = managerId
        });
    }
    
    public override async Task<ActivityResult> ResumeAsync(
        ActivityContext context, 
        Dictionary<string, object> resumeInput, 
        CancellationToken cancellationToken = default)
    {
        // This method is called when the bookmark is consumed
        var approved = resumeInput.GetValueOrDefault("Approved", false);
        var comments = resumeInput.GetValueOrDefault("Comments", string.Empty);
        var approvedBy = resumeInput.GetValueOrDefault("ApprovedBy", "Unknown");
        
        return ActivityResult.Completed(new Dictionary<string, object>
        {
            ["Approved"] = approved,
            ["ApprovalComments"] = comments,
            ["ApprovedBy"] = approvedBy,
            ["ApprovedAt"] = DateTime.UtcNow
        });
    }
}
```

### 3. External API Activity with Resilience

```csharp
public class ExternalApiCallActivity : WorkflowActivity
{
    private readonly HttpClient _httpClient;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly ITwoPhaseExternalCallService _externalCallService;
    
    public ExternalApiCallActivity(
        HttpClient httpClient,
        IWorkflowResilienceService resilienceService,
        ITwoPhaseExternalCallService externalCallService)
    {
        _httpClient = httpClient;
        _resilienceService = resilienceService;
        _externalCallService = externalCallService;
    }
    
    public override async Task<ActivityResult> ExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var apiEndpoint = context.GetVariable<string>("ApiEndpoint");
        var requestPayload = context.GetVariable<object>("RequestPayload");
        
        try
        {
            // Use two-phase pattern for external calls
            var externalCall = await _externalCallService.RecordExternalCallIntentAsync(
                context.WorkflowInstanceId,
                context.ActivityDefinition.Id,
                ExternalCallType.Http,
                apiEndpoint,
                "POST",
                System.Text.Json.JsonSerializer.Serialize(requestPayload),
                cancellationToken
            );
            
            // Execute with resilience protection
            var response = await _resilienceService.ExecuteExternalCallAsync(async ct =>
            {
                var httpResponse = await _httpClient.PostAsJsonAsync(apiEndpoint, requestPayload, ct);
                httpResponse.EnsureSuccessStatusCode();
                var responseContent = await httpResponse.Content.ReadAsStringAsync(ct);
                return responseContent;
            }, "external-api", cancellationToken);
            
            // Mark external call as completed
            await _externalCallService.CompleteExternalCallAsync(
                externalCall.Id, 
                ExternalCallStatus.Completed,
                response,
                cancellationToken
            );
            
            return ActivityResult.Completed(new Dictionary<string, object>
            {
                ["ApiResponse"] = response,
                ["CallCompletedAt"] = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return ActivityResult.Failed($"External API call failed: {ex.Message}");
        }
    }
}
```

## Working with Workflow Variables

### Variable Access in Activities

```csharp
public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    // Type-safe variable access
    var userId = context.GetVariable<string>("UserId");
    var amount = context.GetVariable<decimal>("Amount");
    var dueDate = context.GetVariable<DateTime?>("DueDate");
    
    // Variable with default value
    var priority = context.GetVariable("Priority", "Normal");
    
    // Check if variable exists
    if (context.HasVariable("OptionalField"))
    {
        var optionalValue = context.GetVariable<string>("OptionalField");
    }
    
    // Set output variables
    return ActivityResult.Completed(new Dictionary<string, object>
    {
        ["ProcessedAt"] = DateTime.UtcNow,
        ["ProcessedBy"] = "EmailActivity",
        ["NewCalculatedValue"] = amount * 1.05m
    });
}
```

### Dynamic Variable Updates

```csharp
public class CalculationActivity : WorkflowActivity
{
    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        var baseAmount = context.GetVariable<decimal>("BaseAmount");
        var taxRate = context.GetVariable<decimal>("TaxRate", 0.08m);
        var discount = context.GetVariable<decimal>("Discount", 0.0m);
        
        // Perform calculations
        var discountedAmount = baseAmount - (baseAmount * discount);
        var taxAmount = discountedAmount * taxRate;
        var totalAmount = discountedAmount + taxAmount;
        
        // Return calculated values as output variables
        return ActivityResult.Completed(new Dictionary<string, object>
        {
            ["DiscountedAmount"] = discountedAmount,
            ["TaxAmount"] = taxAmount,
            ["TotalAmount"] = totalAmount,
            ["CalculatedAt"] = DateTime.UtcNow
        });
    }
}
```

## Command and Query Patterns

### Using MediatR Commands

```csharp
// Command to start workflow
public sealed record StartApprovalWorkflowCommand(
    string RequestId,
    decimal Amount,
    string RequestedBy,
    Dictionary<string, object>? AdditionalData = null
) : IRequest<StartWorkflowResult>;

// Command handler
public class StartApprovalWorkflowCommandHandler : IRequestHandler<StartApprovalWorkflowCommand, StartWorkflowResult>
{
    private readonly IWorkflowService _workflowService;
    
    public StartApprovalWorkflowCommandHandler(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }
    
    public async Task<StartWorkflowResult> Handle(StartApprovalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object>
        {
            ["RequestId"] = request.RequestId,
            ["Amount"] = request.Amount,
            ["RequestedBy"] = request.RequestedBy,
            ["RequiresManagerApproval"] = request.Amount > 10000
        };
        
        if (request.AdditionalData != null)
        {
            foreach (var kvp in request.AdditionalData)
            {
                variables[kvp.Key] = kvp.Value;
            }
        }
        
        return await _workflowService.StartWorkflowAsync(
            workflowDefinitionId: ApprovalWorkflows.StandardApproval,
            instanceName: $"Approval-{request.RequestId}",
            startedBy: request.RequestedBy,
            initialVariables: variables,
            correlationId: request.RequestId,
            cancellationToken: cancellationToken
        );
    }
}
```

### Querying Workflow State

```csharp
// Query to get workflow status
public sealed record GetWorkflowStatusQuery(Guid WorkflowInstanceId) : IRequest<WorkflowStatusResponse>;

public class GetWorkflowStatusQueryHandler : IRequestHandler<GetWorkflowStatusQuery, WorkflowStatusResponse>
{
    private readonly IWorkflowInstanceRepository _repository;
    private readonly IWorkflowBookmarkService _bookmarkService;
    
    public async Task<WorkflowStatusResponse> Handle(GetWorkflowStatusQuery request, CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (workflow == null)
        {
            return WorkflowStatusResponse.NotFound();
        }
        
        var activeBookmarks = await _bookmarkService.GetActiveBookmarksAsync(
            request.WorkflowInstanceId, cancellationToken);
        
        return new WorkflowStatusResponse
        {
            WorkflowId = workflow.Id,
            Status = workflow.Status,
            CurrentActivity = workflow.CurrentActivityId,
            Variables = workflow.Variables,
            ActiveBookmarks = activeBookmarks.Select(b => new BookmarkInfo
            {
                Id = b.Id,
                Type = b.Type,
                Key = b.Key,
                DueAt = b.DueAt
            }).ToList()
        };
    }
}
```

## Testing Strategies

### Unit Testing Activities

```csharp
[TestClass]
public class EmailNotificationActivityTests
{
    private Mock<IEmailService> _emailService;
    private Mock<ILogger<EmailNotificationActivity>> _logger;
    private EmailNotificationActivity _activity;
    
    [TestInitialize]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<EmailNotificationActivity>>();
        _activity = new EmailNotificationActivity(_emailService.Object, _logger.Object);
    }
    
    [TestMethod]
    public async Task ExecuteAsync_WithValidInputs_SendsEmail()
    {
        // Arrange
        var context = new ActivityContext(
            workflowInstanceId: Guid.NewGuid(),
            activityDefinition: new ActivityDefinition { Id = "email-test" },
            variables: new Dictionary<string, object>
            {
                ["RecipientEmail"] = "test@example.com",
                ["EmailSubject"] = "Test Subject",
                ["EmailBody"] = "Test Body"
            }
        );
        
        _emailService.Setup(x => x.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _activity.ExecuteAsync(context);
        
        // Assert
        Assert.AreEqual(ActivityResultStatus.Completed, result.Status);
        _emailService.Verify(x => x.SendEmailAsync(
            "test@example.com", "Test Subject", "Test Body", It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsTrue(result.OutputData.ContainsKey("EmailSentAt"));
    }
    
    [TestMethod]
    public async Task ExecuteAsync_WithMissingRecipient_ReturnsFailure()
    {
        // Arrange
        var context = new ActivityContext(
            workflowInstanceId: Guid.NewGuid(),
            activityDefinition: new ActivityDefinition { Id = "email-test" },
            variables: new Dictionary<string, object>
            {
                ["EmailSubject"] = "Test Subject"
            }
        );
        
        // Act
        var result = await _activity.ExecuteAsync(context);
        
        // Assert
        Assert.AreEqual(ActivityResultStatus.Failed, result.Status);
        Assert.AreEqual("RecipientEmail is required", result.ErrorMessage);
    }
}
```

### Integration Testing Workflows

```csharp
[TestClass]
public class ApprovalWorkflowIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    
    [TestInitialize]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace external services with test doubles
                    services.AddScoped<IEmailService, TestEmailService>();
                    services.AddScoped<INotificationService, TestNotificationService>();
                });
            });
        _client = _factory.CreateClient();
    }
    
    [TestMethod]
    public async Task ApprovalWorkflow_SmallAmount_CompletesAutomatically()
    {
        // Arrange
        var request = new StartApprovalWorkflowRequest
        {
            RequestId = "TEST-001",
            Amount = 500m,
            RequestedBy = "john.doe@company.com"
        };
        
        // Act - Start workflow
        var response = await _client.PostAsJsonAsync("/api/workflows/start-approval", request);
        var startResult = await response.Content.ReadFromJsonAsync<StartWorkflowResponse>();
        
        // Wait for completion (small amounts auto-approve)
        await WaitForWorkflowCompletion(startResult.WorkflowId);
        
        // Assert - Check final status
        var statusResponse = await _client.GetAsync($"/api/workflows/{startResult.WorkflowId}/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<WorkflowStatusResponse>();
        
        Assert.AreEqual(WorkflowStatus.Completed, status.Status);
        Assert.AreEqual(true, status.Variables["AutoApproved"]);
    }
    
    [TestMethod]
    public async Task ApprovalWorkflow_LargeAmount_RequiresManagerApproval()
    {
        // Arrange
        var request = new StartApprovalWorkflowRequest
        {
            RequestId = "TEST-002", 
            Amount = 15000m,
            RequestedBy = "john.doe@company.com"
        };
        
        // Act - Start workflow
        var response = await _client.PostAsJsonAsync("/api/workflows/start-approval", request);
        var startResult = await response.Content.ReadFromJsonAsync<StartWorkflowResponse>();
        
        // Wait for workflow to reach pending state
        await WaitForWorkflowStatus(startResult.WorkflowId, WorkflowStatus.Suspended);
        
        // Get active bookmarks
        var bookmarksResponse = await _client.GetAsync($"/api/workflows/{startResult.WorkflowId}/bookmarks");
        var bookmarks = await bookmarksResponse.Content.ReadFromJsonAsync<List<BookmarkInfo>>();
        var approvalBookmark = bookmarks.First(b => b.Type == BookmarkType.UserAction);
        
        // Complete the approval
        var completionRequest = new CompleteActivityRequest
        {
            WorkflowId = startResult.WorkflowId,
            ActivityId = "manager-approval",
            BookmarkKey = approvalBookmark.Key,
            OutputData = new Dictionary<string, object>
            {
                ["Approved"] = true,
                ["Comments"] = "Approved for valid business reason",
                ["ApprovedBy"] = "manager@company.com"
            }
        };
        
        await _client.PostAsJsonAsync("/api/workflows/complete-activity", completionRequest);
        
        // Wait for completion
        await WaitForWorkflowCompletion(startResult.WorkflowId);
        
        // Assert
        var statusResponse = await _client.GetAsync($"/api/workflows/{startResult.WorkflowId}/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<WorkflowStatusResponse>();
        
        Assert.AreEqual(WorkflowStatus.Completed, status.Status);
        Assert.AreEqual(true, status.Variables["Approved"]);
        Assert.AreEqual("manager@company.com", status.Variables["ApprovedBy"]);
    }
}
```

## Error Handling Best Practices

### Activity Error Handling

```csharp
public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    try
    {
        // Business logic here
        var result = await SomeBusinessOperation(context, cancellationToken);
        return ActivityResult.Completed(result);
    }
    catch (ArgumentException ex)
    {
        // Invalid input - don't retry
        return ActivityResult.Failed($"Invalid input: {ex.Message}");
    }
    catch (HttpRequestException ex) when (ex.Message.Contains("timeout"))
    {
        // Transient error - will be retried by resilience service
        throw new TransientException("HTTP timeout occurred", ex);
    }
    catch (DbUpdateConcurrencyException)
    {
        // Concurrency conflict - will be retried automatically
        throw;
    }
    catch (Exception ex)
    {
        // Unknown error - log and fail
        _logger.LogError(ex, "Unexpected error in {ActivityType} for workflow {WorkflowId}", 
            GetType().Name, context.WorkflowInstanceId);
        return ActivityResult.Failed($"Unexpected error: {ex.GetType().Name}");
    }
}
```

### Custom Exception Types

```csharp
// Transient errors that should be retried
public class TransientException : Exception
{
    public TransientException(string message) : base(message) { }
    public TransientException(string message, Exception innerException) : base(message, innerException) { }
}

// Business rule violations that should not be retried
public class BusinessRuleException : Exception
{
    public string RuleCode { get; }
    
    public BusinessRuleException(string ruleCode, string message) : base(message)
    {
        RuleCode = ruleCode;
    }
}

// Usage in activities
if (amount > maxAllowed)
{
    throw new BusinessRuleException("AMOUNT_EXCEEDS_LIMIT", 
        $"Amount {amount:C} exceeds maximum allowed {maxAllowed:C}");
}
```

## Performance Optimization

### Efficient Variable Access

```csharp
// ❌ Bad - Multiple dictionary lookups
public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    if (context.Variables.ContainsKey("UserId") && 
        context.Variables.ContainsKey("Amount") && 
        context.Variables.ContainsKey("Category"))
    {
        var userId = (string)context.Variables["UserId"];
        var amount = (decimal)context.Variables["Amount"];  
        var category = (string)context.Variables["Category"];
        // ... rest of logic
    }
}

// ✅ Good - Single lookup with validation
public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    var userId = context.GetVariable<string>("UserId");
    var amount = context.GetVariable<decimal>("Amount");
    var category = context.GetVariable<string>("Category");
    
    // Validate all required inputs upfront
    if (string.IsNullOrEmpty(userId))
        return ActivityResult.Failed("UserId is required");
    if (amount <= 0)
        return ActivityResult.Failed("Amount must be positive");
    if (string.IsNullOrEmpty(category))
        return ActivityResult.Failed("Category is required");
    
    // ... business logic
}
```

### Database Query Optimization

```csharp
// ❌ Bad - N+1 query problem
public async Task<List<WorkflowSummary>> GetUserWorkflowsAsync(string userId)
{
    var workflows = await _repository.GetByAssigneeAsync(userId);
    var summaries = new List<WorkflowSummary>();
    
    foreach (var workflow in workflows)
    {
        var bookmarks = await _bookmarkService.GetActiveBookmarksAsync(workflow.Id); // N+1!
        summaries.Add(new WorkflowSummary 
        { 
            Workflow = workflow, 
            ActiveBookmarks = bookmarks 
        });
    }
    
    return summaries;
}

// ✅ Good - Single query with includes
public async Task<List<WorkflowSummary>> GetUserWorkflowsAsync(string userId)
{
    var workflows = await _context.WorkflowInstances
        .Where(w => w.CurrentAssignee == userId)
        .Include(w => w.Bookmarks.Where(b => !b.IsConsumed))
        .ToListAsync();
    
    return workflows.Select(w => new WorkflowSummary
    {
        Workflow = w,
        ActiveBookmarks = w.Bookmarks
    }).ToList();
}
```

## API Reference

### Core Endpoints

```http
POST /api/workflows/start
Content-Type: application/json

{
  "workflowDefinitionId": "guid",
  "instanceName": "string",
  "startedBy": "string",
  "initialVariables": {},
  "correlationId": "string",
  "assignmentOverrides": {}
}
```

```http
POST /api/workflows/{workflowId}/complete-activity
Content-Type: application/json

{
  "activityId": "string",
  "completedBy": "string", 
  "outputData": {},
  "bookmarkKey": "string"
}
```

```http
GET /api/workflows/{workflowId}
Accept: application/json

Response:
{
  "id": "guid",
  "status": "Running|Suspended|Completed|Failed|Cancelled",
  "currentActivity": "string",
  "variables": {},
  "activeBookmarks": []
}
```

```http
POST /api/workflows/{workflowId}/cancel
Content-Type: application/json

{
  "cancelledBy": "string",
  "reason": "string"
}
```

### Advanced Endpoints

```http
GET /api/workflows/user/{userId}/tasks
Accept: application/json

Response:
{
  "tasks": [
    {
      "workflowId": "guid",
      "activityId": "string", 
      "bookmarkKey": "string",
      "assignedAt": "datetime",
      "dueAt": "datetime?",
      "priority": "High|Normal|Low"
    }
  ]
}
```

```http
GET /api/workflows/{workflowId}/history
Accept: application/json

Response:
{
  "executionLog": [
    {
      "timestamp": "datetime",
      "event": "Started|ActivityCompleted|Failed",
      "activityId": "string",
      "details": {}
    }
  ]
}
```

This guide provides the essential knowledge needed to develop, test, and maintain workflows in the system. For architectural details, see [WORKFLOW-ARCHITECTURE.md](./WORKFLOW-ARCHITECTURE.md). For production deployment and operations, see [WORKFLOW-OPERATIONS-GUIDE.md](./WORKFLOW-OPERATIONS-GUIDE.md).