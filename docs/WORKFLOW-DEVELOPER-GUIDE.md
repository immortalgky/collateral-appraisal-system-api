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

### 3. Multi-Step Workflow Orchestration

Using the WorkflowOrchestrator for complex workflows:

```csharp
[HttpPost("execute-complete-workflow")]
public async Task<IActionResult> ExecuteCompleteWorkflow([FromBody] ExecuteWorkflowRequest request)
{
    var result = await _workflowOrchestrator.ExecuteCompleteWorkflowAsync(
        workflowInstanceId: request.WorkflowId,
        maxSteps: 50, // Prevent infinite loops
        cancellationToken: HttpContext.RequestAborted
    );
    
    return result.Status switch
    {
        WorkflowExecutionStatus.Completed => Ok(new { Status = "Completed", Result = result }),
        WorkflowExecutionStatus.Suspended => Accepted(new { Status = "Suspended", BookmarkKey = result.BookmarkKey }),
        WorkflowExecutionStatus.Failed => BadRequest(new { Status = "Failed", Error = result.ErrorMessage }),
        _ => Ok(new { Status = result.Status.ToString(), Result = result })
    };
}
```

### 4. Expression Engine Usage

Evaluating dynamic workflow conditions:

```csharp
public class ConditionalActivity : WorkflowActivityBase
{
    private readonly IWorkflowExpressionService _expressionService;
    
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        // Get the condition expression from activity properties
        var conditionExpr = GetProperty<string>(context, "condition");
        
        // Create expression context with available variables
        var exprContext = new ExpressionContext
        {
            Variables = context.Variables,
            Input = context.Properties,
            Context = new Dictionary<string, object>
            {
                ["UserId"] = context.UserId,
                ["ActivityId"] = context.ActivityId,
                ["Timestamp"] = DateTime.UtcNow
            }
        };
        
        // Evaluate the condition
        var result = await _expressionService.EvaluateAsync<bool>(
            conditionExpr, 
            exprContext, 
            cancellationToken);
            
        if (!result.IsSuccess)
        {
            return ActivityResult.Failed($"Expression evaluation failed: {result.ErrorMessage}");
        }
        
        return ActivityResult.Completed(new Dictionary<string, object>
        {
            ["ConditionResult"] = result.Value,
            ["EvaluatedAt"] = DateTime.UtcNow
        });
    }
}
```

## Creating Custom Activities

### Activity Patterns in Your Codebase

There are **two main patterns** for creating workflow activities:

#### 1. **Automated Activities** (WorkflowActivityBase Pattern)
For activities that complete immediately without user interaction:

```csharp
public class EmailNotificationActivity : WorkflowActivityBase
{
    private readonly ILogger<EmailNotificationActivity> _logger;
    
    public EmailNotificationActivity(ILogger<EmailNotificationActivity> logger)
    {
        _logger = logger;
    }
    
    public override string ActivityType => "EmailNotification";
    public override string Name => "Email Notification";
    public override string Description => "Sends an email notification to specified recipients";
    
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get activity properties using built-in helper methods
            var recipient = GetProperty<string>(context, "recipient");
            var subject = GetProperty<string>(context, "subject");
            var body = GetProperty<string>(context, "body");
            
            // Get workflow variables for personalization
            var requestId = GetVariable<string>(context, "RequestId", "Unknown");
            var userName = GetVariable<string>(context, "UserName", "User");
            
            // Validate required inputs
            if (string.IsNullOrEmpty(recipient))
            {
                return ActivityResult.Failed("Recipient email is required");
            }
            
            // TODO: Replace with actual email service
            await Task.Delay(100, cancellationToken); // Simulate sending
            
            _logger.LogInformation("Email sent to {Recipient} for workflow {WorkflowId}", 
                recipient, context.WorkflowInstance.Id);
            
            // Return completed immediately
            return ActivityResult.Completed(new Dictionary<string, object>
            {
                ["EmailSentAt"] = DateTime.UtcNow,
                ["EmailSentTo"] = recipient,
                ["EmailStatus"] = "Sent"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for workflow {WorkflowId}", context.WorkflowInstance.Id);
            return ActivityResult.Failed($"Email sending failed: {ex.Message}");
        }
    }
}
```

#### 2. **Human Task Activities** (Direct IWorkflowActivity Pattern)
For activities that require human interaction and must wait (like TaskActivity):

```csharp
public class ManagerApprovalActivity : IWorkflowActivity
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly ILogger<ManagerApprovalActivity> _logger;
    
    public ManagerApprovalActivity(
        IWorkflowBookmarkService bookmarkService,
        ILogger<ManagerApprovalActivity> logger)
    {
        _bookmarkService = bookmarkService;
        _logger = logger;
    }
    
    public string ActivityType => "ManagerApproval";
    public string Name => "Manager Approval";
    public string Description => "Requires manager approval for workflow continuation";
    
    /// <summary>
    /// Creates bookmark and returns Pending to wait for manager approval
    /// </summary>
    public async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get properties from activity configuration
            var managerId = GetProperty<string>(context, "managerId");
            var approvalMessage = GetProperty<string>(context, "approvalMessage", "Manager approval required");
            var dueHours = GetProperty<int>(context, "dueHours", 24);
            
            // Get variables from workflow context
            var requestAmount = GetVariable<decimal>(context, "RequestAmount", 0);
            var requestId = GetVariable<string>(context, "RequestId", "Unknown");
            
            // Validate required data
            if (string.IsNullOrEmpty(managerId))
            {
                return ActivityResult.Failed("Manager ID is required for approval");
            }
            
            // Create bookmark for user action - this pauses the workflow
            var bookmarkKey = $"approval_{context.WorkflowInstance.Id}_{Guid.NewGuid():N}";
            var dueAt = DateTime.UtcNow.AddHours(dueHours);
            
            await _bookmarkService.CreateUserActionBookmarkAsync(
                context.WorkflowInstance.Id,
                context.ActivityId,
                bookmarkKey,
                context.WorkflowInstance.CorrelationId,
                System.Text.Json.JsonSerializer.Serialize(new { requestId, requestAmount, approvalMessage }),
                cancellationToken
            );
            
            _logger.LogInformation("Created approval task for manager {ManagerId}, due at {DueAt}", managerId, dueAt);
            
            // Return pending - workflow pauses here
            return ActivityResult.Pending(new Dictionary<string, object>
            {
                ["BookmarkKey"] = bookmarkKey,
                ["AssignedTo"] = managerId,
                ["DueAt"] = dueAt,
                ["RequestAmount"] = requestAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create approval task");
            return ActivityResult.Failed($"Failed to create approval task: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Processes approval decision when bookmark is consumed
    /// </summary>
    public async Task<ActivityResult> ResumeAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get approval decision from resume input
            var approved = GetResumeValue<bool>(resumeInput, "approved", false);
            var comments = GetResumeValue<string>(resumeInput, "comments", "");
            var approvedBy = GetResumeValue<string>(resumeInput, "approvedBy", "Unknown");
            
            _logger.LogInformation("Approval decision: {Decision} by {ApprovedBy}", 
                approved ? "Approved" : "Rejected", approvedBy);
            
            return ActivityResult.Completed(new Dictionary<string, object>
            {
                ["Approved"] = approved,
                ["ApprovalDecision"] = approved ? "approved" : "rejected", 
                ["decision"] = approved ? "approved" : "rejected", // For workflow transitions
                ["ApprovalComments"] = comments,
                ["ApprovedBy"] = approvedBy,
                ["ApprovedAt"] = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process approval");
            return ActivityResult.Failed($"Failed to process approval: {ex.Message}");
        }
    }
    
    public Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // Validate activity configuration
        var errors = new List<string>();
        if (string.IsNullOrEmpty(GetProperty<string>(context, "managerId")))
            errors.Add("Manager ID is required");
            
        return Task.FromResult(errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success());
    }
    
    // Helper methods for accessing context data
    private T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!) =>
        context.Properties.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
        
    private T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!) =>
        context.Variables.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
        
    private T GetResumeValue<T>(Dictionary<string, object> resumeInput, string key, T defaultValue = default!) =>
        resumeInput.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
}
```

### 3. External API Activity with Resilience

Activities that need to make external API calls should use the two-phase external call pattern with resilience protection.

**Implementation:** `ExternalApiActivity.cs`

```csharp
public class ExternalApiActivity : WorkflowActivityBase
{
    private readonly HttpClient _httpClient;
    private readonly ITwoPhaseExternalCallService _externalCallService;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly ILogger<ExternalApiActivity> _logger;

    public ExternalApiActivity(
        HttpClient httpClient,
        ITwoPhaseExternalCallService externalCallService,
        IWorkflowResilienceService resilienceService,
        ILogger<ExternalApiActivity> logger)
    {
        _httpClient = httpClient;
        _externalCallService = externalCallService;
        _resilienceService = resilienceService;
        _logger = logger;
    }

    public override string ActivityType => "ExternalApi";
    public override string Name => "External API Call";
    public override string Description => "Makes HTTP calls to external APIs with two-phase reliability pattern";

    protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get activity properties
            var apiUrl = GetProperty<string>(context, "apiUrl");
            var httpMethod = GetProperty<string>(context, "httpMethod", "POST");
            var requestPayload = GetProperty<object>(context, "requestPayload");
            var headers = GetProperty<Dictionary<string, string>>(context, "headers", new Dictionary<string, string>());
            var timeoutSeconds = GetProperty<int>(context, "timeoutSeconds", 30);

            // Validate required inputs
            if (string.IsNullOrEmpty(apiUrl))
            {
                return ActivityResult.Failed("API URL is required");
            }

            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            {
                return ActivityResult.Failed("API URL must be a valid absolute URL");
            }

            _logger.LogInformation("Making {HttpMethod} request to {ApiUrl} for workflow {WorkflowId}", 
                httpMethod, apiUrl, context.WorkflowInstance.Id);

            // Phase 1: Record external call intent within transaction
            var externalCall = await _externalCallService.RecordExternalCallIntentAsync(
                context.WorkflowInstance.Id,
                context.ActivityId,
                ExternalCallType.Http,
                apiUrl,
                httpMethod,
                System.Text.Json.JsonSerializer.Serialize(requestPayload),
                headers,
                cancellationToken
            );

            // Phase 2: Execute external call with resilience protection
            var response = await _resilienceService.ExecuteExternalCallAsync(async ct =>
            {
                using var request = new HttpRequestMessage(new HttpMethod(httpMethod), apiUrl);

                // Add headers
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // Add request body for POST/PUT
                if (requestPayload != null && (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) || 
                                              httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(requestPayload);
                    request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                }

                // Set timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var httpResponse = await _httpClient.SendAsync(request, timeoutCts.Token);
                var responseContent = await httpResponse.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API call failed with status {httpResponse.StatusCode}: {responseContent}");
                }

                return new
                {
                    StatusCode = (int)httpResponse.StatusCode,
                    Content = responseContent,
                    Headers = httpResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                };

            }, "external-api", cancellationToken);

            // Complete the external call record
            await _externalCallService.CompleteExternalCallAsync(
                externalCall.Id,
                System.Text.Json.JsonSerializer.Serialize(response),
                DateTime.UtcNow - externalCall.CreatedOn,
                cancellationToken
            );

            // Return success with API response data
            return ActivityResult.Completed(new Dictionary<string, object>
            {
                ["ApiResponse"] = response.Content,
                ["StatusCode"] = response.StatusCode,
                ["ResponseHeaders"] = response.Headers,
                ["CallCompletedAt"] = DateTime.UtcNow,
                ["CallDuration"] = DateTime.UtcNow - externalCall.CreatedOn,
                ["ExternalCallId"] = externalCall.Id
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for workflow {WorkflowId}: {Error}", 
                context.WorkflowInstance.Id, ex.Message);
            return ActivityResult.Failed($"API call failed: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "API call timed out for workflow {WorkflowId}", context.WorkflowInstance.Id);
            return ActivityResult.Failed("API call timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during API call for workflow {WorkflowId}", context.WorkflowInstance.Id);
            return ActivityResult.Failed($"Unexpected error during API call: {ex.Message}");
        }
    }

    public override Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var apiUrl = GetProperty<string>(context, "apiUrl");
        if (string.IsNullOrEmpty(apiUrl))
        {
            errors.Add("API URL is required for ExternalApiActivity");
        }
        else if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            errors.Add("API URL must be a valid absolute URL");
        }
        else if (uri.Scheme != "https" && uri.Scheme != "http")
        {
            errors.Add("API URL must use HTTP or HTTPS protocol");
        }

        var httpMethod = GetProperty<string>(context, "httpMethod", "POST");
        var allowedMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };
        if (!allowedMethods.Contains(httpMethod.ToUpperInvariant()))
        {
            errors.Add($"HTTP method must be one of: {string.Join(", ", allowedMethods)}");
        }

        var timeoutSeconds = GetProperty<int>(context, "timeoutSeconds", 30);
        if (timeoutSeconds <= 0 || timeoutSeconds > 300) // Max 5 minutes
        {
            errors.Add("Timeout must be between 1 and 300 seconds");
        }

        return Task.FromResult(errors.Any()
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success());
    }
}
```

**Key Features:**
- **Two-Phase Pattern**: Records intent first, then executes call
- **Resilience Protection**: Uses `IWorkflowResilienceService` for retry/circuit breaker
- **Comprehensive Validation**: URL format, HTTP methods, timeouts
- **Rich Output Data**: Returns response, headers, timing information
- **Error Handling**: Different handling for HTTP errors vs timeouts vs general exceptions

**Configuration Example:**
```json
{
  "activityId": "call-scoring-api",
  "activityType": "ExternalApi",
  "properties": {
    "apiUrl": "https://scoring-service.example.com/api/score",
    "httpMethod": "POST",
    "requestPayload": {
      "loanAmount": "${LoanAmount}",
      "creditScore": "${CreditScore}",
      "collateralValue": "${CollateralValue}"
    },
    "headers": {
      "Authorization": "Bearer ${ApiToken}",
      "Content-Type": "application/json"
    },
    "timeoutSeconds": 60
  }
}
```

## Working with Workflow Variables

### Variable Access in Activities

Activities have access to two types of data: **Properties** (activity configuration) and **Variables** (workflow state).

#### Activity Properties (Configuration)

```csharp
protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    // Get activity properties with type safety and defaults
    var recipient = GetProperty<string>(context, "recipient");
    var subject = GetProperty<string>(context, "subject", "Default Subject");
    var timeoutSeconds = GetProperty<int>(context, "timeoutSeconds", 30);
    var headers = GetProperty<Dictionary<string, string>>(context, "headers", new Dictionary<string, string>());
    
    // Properties come from activity configuration in workflow definition
    // Example: { "activityType": "EmailNotification", "properties": { "recipient": "user@example.com" } }
    
    return ActivityResult.Completed(outputData);
}
```

#### Workflow Variables (State)

```csharp
protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    // Get workflow variables - shared state across activities
    var requestId = GetVariable<string>(context, "RequestId", "Unknown");
    var loanAmount = GetVariable<decimal>(context, "LoanAmount", 0);
    var userName = GetVariable<string>(context, "UserName", "User");
    var submittedAt = GetVariable<DateTime?>(context, "SubmittedAt");
    
    // Variables are set by previous activities or workflow initialization
    // They represent the current workflow state
    
    return ActivityResult.Completed(outputData);
}

// Helper methods in WorkflowActivityBase
private T GetProperty<T>(ActivityContext context, string key, T defaultValue = default!)
{
    if (context.Properties.TryGetValue(key, out var value) && value is T typedValue)
        return typedValue;
    return defaultValue;
}

private T GetVariable<T>(ActivityContext context, string key, T defaultValue = default!)
{
    if (context.Variables.TryGetValue(key, out var value) && value is T typedValue)
        return typedValue;
    return defaultValue;
}
```

#### Setting Output Variables

```csharp
protected override async Task<ActivityResult> OnExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
{
    // Process activity logic...
    
    // Set output variables for next activities
    return ActivityResult.Completed(new Dictionary<string, object>
    {
        ["ProcessedAt"] = DateTime.UtcNow,
        ["ProcessedBy"] = ActivityType,
        ["EmailSentTo"] = recipient,
        ["EmailStatus"] = "Sent",
        ["NewCalculatedValue"] = amount * 1.05m // Example calculation
    });
}
```

#### Variable vs Property Usage

- **Properties**: Static configuration defined in workflow schema
  - Email recipients, API endpoints, timeout values
  - Set once in workflow definition, don't change during execution
  - Use `GetProperty<T>(context, key, defaultValue)`

- **Variables**: Dynamic workflow state
  - User data, calculated values, previous activity outputs
  - Change as workflow progresses through activities
  - Use `GetVariable<T>(context, key, defaultValue)`

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

## Advanced Bookmark Patterns

### Claim and Lease Mechanism

The advanced bookmark system supports atomic claim/lease for concurrent processing:

```csharp
public class BookmarkClaimService
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    
    public async Task<ClaimedBookmark?> ClaimNextAvailableTaskAsync(
        string claimedBy,
        BookmarkType taskType = BookmarkType.UserAction,
        TimeSpan leaseDuration = default,
        CancellationToken cancellationToken = default)
    {
        leaseDuration = leaseDuration == default ? TimeSpan.FromMinutes(30) : leaseDuration;
        
        // Atomically claim an available bookmark
        var claimedBookmark = await _bookmarkService.ClaimNextAvailableAsync(
            taskType,
            claimedBy, 
            leaseDuration,
            cancellationToken);
            
        if (claimedBookmark == null)
        {
            return null; // No tasks available
        }
        
        return new ClaimedBookmark
        {
            WorkflowId = claimedBookmark.WorkflowInstanceId,
            ActivityId = claimedBookmark.ActivityId,
            BookmarkKey = claimedBookmark.Key,
            LeaseExpiresAt = claimedBookmark.LeaseExpiresAt,
            Payload = claimedBookmark.Payload
        };
    }
}
```

### Timer Bookmarks

Working with scheduled and recurring tasks:

```csharp
public class TimerBasedActivity : WorkflowActivityBase
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var delayMinutes = GetProperty<int>(context, "delayMinutes", 60);
        var dueAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        
        // Create timer bookmark
        await _bookmarkService.CreateBookmarkAsync(
            context.WorkflowInstanceId,
            context.ActivityId,
            BookmarkType.Timer,
            key: $"timer-{context.ActivityId}-{context.WorkflowInstanceId}",
            dueAt: dueAt,
            payload: JsonSerializer.Serialize(new { DelayMinutes = delayMinutes }),
            cancellationToken: cancellationToken
        );
        
        // Suspend workflow until timer expires
        return ActivityResult.Suspended(new Dictionary<string, object>
        {
            ["ScheduledFor"] = dueAt,
            ["TimerSet"] = true
        });
    }
}
```

### External Message Bookmarks

Handling webhook callbacks and external events:

```csharp
public class WebhookActivity : WorkflowActivityBase
{
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly IWebhookRegistrationService _webhookService;
    
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var webhookUrl = GetProperty<string>(context, "webhookUrl");
        var correlationId = context.Variables.GetValueOrDefault("CorrelationId")?.ToString() 
                           ?? Guid.NewGuid().ToString();
        
        // Register webhook with external system
        await _webhookService.RegisterWebhookAsync(
            webhookUrl,
            correlationId,
            callbackUrl: $"/api/workflows/webhook/{correlationId}",
            cancellationToken);
        
        // Create external message bookmark
        await _bookmarkService.CreateBookmarkAsync(
            context.WorkflowInstanceId,
            context.ActivityId,
            BookmarkType.ExternalMessage,
            key: correlationId,
            correlationId: correlationId,
            payload: JsonSerializer.Serialize(new { 
                WebhookUrl = webhookUrl,
                RegisteredAt = DateTime.UtcNow 
            }),
            cancellationToken: cancellationToken
        );
        
        return ActivityResult.Suspended(new Dictionary<string, object>
        {
            ["WebhookRegistered"] = true,
            ["CorrelationId"] = correlationId
        });
    }
}

// Webhook controller endpoint
[HttpPost("api/workflows/webhook/{correlationId}")]
public async Task<IActionResult> HandleWebhook(
    string correlationId,
    [FromBody] object webhookPayload)
{
    var command = new CompleteActivityCommand(
        WorkflowInstanceId: Guid.Empty, // Will be resolved by correlation
        ActivityId: string.Empty,       // Will be resolved by bookmark
        CompletedBy: "external-system",
        OutputData: new Dictionary<string, object>
        {
            ["WebhookPayload"] = webhookPayload,
            ["ReceivedAt"] = DateTime.UtcNow
        },
        BookmarkKey: correlationId
    );
    
    var result = await _mediator.Send(command);
    return result.Success ? Ok() : BadRequest(result.ErrorMessage);
}
```

### Approval Workflows with Escalation

Complex human task patterns:

```csharp
public class EscalatingApprovalActivity : HumanTaskActivityBase
{
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var amount = GetVariable<decimal>(context, "Amount");
        var escalationHours = GetProperty<int>(context, "escalationHours", 24);
        
        // Determine approval level
        var approvers = amount switch
        {
            < 1000 => new[] { "team-lead@company.com" },
            < 10000 => new[] { "manager@company.com" },
            _ => new[] { "director@company.com", "finance@company.com" }
        };
        
        // Create approval bookmark
        var bookmarkKey = $"approval-{context.WorkflowInstanceId}-{context.ActivityId}";
        await CreateHumanTaskBookmarkAsync(
            context,
            BookmarkType.Approval,
            bookmarkKey,
            assignees: approvers,
            dueAt: DateTime.UtcNow.AddHours(escalationHours),
            cancellationToken: cancellationToken
        );
        
        // Create escalation timer bookmark
        var escalationKey = $"escalation-{bookmarkKey}";
        await _bookmarkService.CreateBookmarkAsync(
            context.WorkflowInstanceId,
            $"{context.ActivityId}-escalation",
            BookmarkType.Timer,
            key: escalationKey,
            dueAt: DateTime.UtcNow.AddHours(escalationHours),
            payload: JsonSerializer.Serialize(new { 
                OriginalBookmarkKey = bookmarkKey,
                EscalationLevel = 1 
            }),
            cancellationToken: cancellationToken
        );
        
        return ActivityResult.Suspended(new Dictionary<string, object>
        {
            ["PendingApproval"] = true,
            ["Approvers"] = approvers,
            ["EscalationScheduled"] = DateTime.UtcNow.AddHours(escalationHours)
        });
    }
}
```

### Manual Intervention Bookmarks

For admin actions and error recovery:

```csharp
public class ManualInterventionActivity : WorkflowActivityBase
{
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var errorDetails = GetVariable<string>(context, "ErrorDetails");
        var interventionReason = GetProperty<string>(context, "reason");
        
        // Create manual intervention bookmark
        await _bookmarkService.CreateBookmarkAsync(
            context.WorkflowInstanceId,
            context.ActivityId,
            BookmarkType.ManualIntervention,
            key: $"intervention-{context.WorkflowInstanceId}",
            payload: JsonSerializer.Serialize(new {
                Reason = interventionReason,
                ErrorDetails = errorDetails,
                RequestedAt = DateTime.UtcNow,
                RequiredRole = "workflow-admin"
            }),
            cancellationToken: cancellationToken
        );
        
        // Send alert to administrators
        await _notificationService.NotifyAdministratorsAsync(
            $"Manual intervention required for workflow {context.WorkflowInstanceId}",
            $"Reason: {interventionReason}\nError: {errorDetails}",
            priority: NotificationPriority.High,
            cancellationToken: cancellationToken
        );
        
        return ActivityResult.Suspended(new Dictionary<string, object>
        {
            ["InterventionRequested"] = true,
            ["RequestedAt"] = DateTime.UtcNow,
            ["Reason"] = interventionReason
        });
    }
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