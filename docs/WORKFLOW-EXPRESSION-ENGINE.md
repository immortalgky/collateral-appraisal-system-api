# Workflow Expression Engine Guide

## Overview

The Workflow Expression Engine provides secure, performant C# expression evaluation for dynamic workflow logic. It uses Microsoft.CodeAnalysis.CSharp.Scripting with comprehensive security constraints and caching optimizations.

## Key Features

✅ **Security by Design**
- Timeout protection (configurable, default 5 seconds)
- Max recursion depth limit (50 levels)
- Whitelisted assemblies and namespaces only
- No reflection or dynamic code execution
- Safe variable scoping and isolation

✅ **Performance Optimized**
- Compiled script caching with memory optimization
- Concurrent dictionary for thread-safe access  
- Expression validation before evaluation
- Minimal memory footprint per evaluation

✅ **Developer Friendly**
- Familiar C# syntax and operators
- IntelliSense-ready context variables
- Rich error messages with line numbers
- Comprehensive built-in functions

## Expression Syntax

### Basic Expressions

```csharp
// Simple comparisons
Variables.Amount > 1000
Variables.Status == "Approved"
Variables.Priority == "High"

// Arithmetic operations
Variables.BaseAmount * 1.08 + Variables.Fee
Variables.Quantity * Variables.UnitPrice

// String operations
Variables.CustomerName.ToUpper().Contains("CORP")
Variables.Email.EndsWith("@company.com")

// Date operations
Variables.DueDate < DateTime.UtcNow.AddDays(7)
Variables.CreatedDate.DayOfWeek == DayOfWeek.Monday
```

### Complex Logic

```csharp
// Conditional expressions
Variables.Amount > 10000 ? "Manager" : "TeamLead"

// Boolean logic
Variables.Amount > 5000 && Variables.Category == "Equipment"
Variables.Status == "Pending" || Variables.Status == "Review"

// Pattern matching (C# 8+)
Variables.Risk switch
{
    "Low" => 1,
    "Medium" => 2, 
    "High" => 3,
    _ => 0
}

// LINQ expressions
Variables.Items.Cast<Dictionary<string, object>>()
    .Sum(item => (decimal)item["Amount"])

Variables.Approvers.Cast<string>()
    .Any(approver => approver.Contains("@management.com"))
```

## Available Variables

### Core Context Variables

```csharp
// Workflow variables (from workflow state)
Variables.RequestId       // string
Variables.Amount         // decimal
Variables.Status         // string
Variables.AssignedTo     // string

// Execution context
Context.WorkflowInstanceId    // Guid
Context.CurrentActivityId     // string
Context.CurrentUser          // string  
Context.ExecutionTime        // DateTime

// Activity input (from activity properties)
Input.TimeoutHours          // int
Input.EmailTemplate         // string
Input.RequiresApproval      // bool
```

### Built-in Functions

```csharp
// Date/time functions
Now()                    // Current UTC time
Today()                  // Current date (midnight UTC)
AddDays(date, days)      // Add days to date
FormatDate(date, format) // Format date as string

// Utility functions  
NewGuid()               // Generate new GUID
Random()                // Random double [0.0, 1.0)
Hash(text)              // SHA-256 hash of text
Base64Encode(text)      // Base64 encode text

// String functions
IsNullOrEmpty(text)     // Check if string is null/empty
Truncate(text, length)  // Truncate to max length
Slugify(text)           // Convert to URL-friendly slug

// Math functions  
Round(value, decimals)  // Round decimal value
Ceiling(value)          // Round up to integer
Floor(value)            // Round down to integer
Abs(value)             // Absolute value
```

## Usage Examples

### Activity Conditions

```csharp
// In IfElse activity
public class ApprovalRoutingActivity : IfElseActivity
{
    protected override string GetConditionExpression(ActivityContext context)
    {
        return @"
            Variables.Amount > 10000 || 
            Variables.Category == ""Sensitive"" || 
            Variables.RequestedBy.EndsWith(""@external.com"")
        ";
    }
}

// In Switch activity  
public class PriorityRoutingActivity : SwitchActivity
{
    protected override string GetSwitchExpression(ActivityContext context)
    {
        return @"
            Variables.Amount switch
            {
                > 50000 => ""Critical"",
                > 10000 => ""High"",  
                > 1000 => ""Normal"",
                _ => ""Low""
            }
        ";
    }
}
```

### Dynamic Assignment

```csharp
// Assign based on business rules
public class DynamicAssignmentActivity : WorkflowActivityBase
{
    private readonly IWorkflowExpressionService _expressionService;
    
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        // Define assignment logic as expression
        var assignmentExpression = @"
            Variables.Department switch
            {
                ""Finance"" => Variables.Amount > 5000 ? ""finance-manager@company.com"" : ""finance-team@company.com"",
                ""IT"" => ""it-manager@company.com"",
                ""HR"" => ""hr-team@company.com"", 
                _ => ""default-approver@company.com""
            }
        ";
        
        var expressionContext = new ExpressionContext
        {
            Variables = context.Variables,
            CurrentUser = context.UserId,
            WorkflowInstanceId = context.WorkflowInstanceId
        };
        
        var result = await _expressionService.EvaluateAsync<string>(
            assignmentExpression,
            expressionContext,
            cancellationToken);
            
        if (!result.IsSuccess)
        {
            return ActivityResult.Failed($"Assignment expression failed: {result.ErrorMessage}");
        }
        
        return ActivityResult.Completed(new Dictionary<string, object>
        {
            ["AssignedTo"] = result.Value,
            ["AssignmentReason"] = "Dynamic assignment based on department and amount"
        });
    }
}
```

### Validation Rules

```csharp
// Validate input data
public class ValidationActivity : WorkflowActivityBase
{
    protected override async Task<ActivityResult> OnExecuteAsync(
        ActivityContext context, 
        CancellationToken cancellationToken = default)
    {
        var validationRules = new[]
        {
            ("Amount > 0", "Amount must be positive"),
            ("!IsNullOrEmpty(Variables.CustomerName)", "Customer name is required"),
            ("Variables.Email.Contains(\"@\")", "Valid email address required"),
            ("Variables.DueDate > DateTime.UtcNow", "Due date must be in the future")
        };
        
        var errors = new List<string>();
        
        foreach (var (rule, message) in validationRules)
        {
            var result = await _expressionService.EvaluateAsync<bool>(
                rule, 
                new ExpressionContext { Variables = context.Variables },
                cancellationToken);
                
            if (result.IsSuccess && !result.Value)
            {
                errors.Add(message);
            }
        }
        
        if (errors.Any())
        {
            return ActivityResult.Failed($"Validation failed: {string.Join(", ", errors)}");
        }
        
        return ActivityResult.Completed();
    }
}
```

## Security Considerations

### Whitelisted Assemblies

Only these assemblies are available in expressions:

```csharp
- System (mscorlib)
- System.Linq  
- System.Collections.Generic
- System.Text
- System.Text.RegularExpressions
- System.Math
- DateTime/TimeSpan types
- Guid type
```

### Prohibited Operations

These operations are **NOT** allowed:

```csharp
❌ File I/O operations
❌ Network calls  
❌ Reflection (typeof, GetType, etc.)
❌ Assembly loading
❌ Process execution
❌ Environment access
❌ Threading operations
❌ Unsafe code
❌ Dynamic compilation
```

### Timeout Protection

All expressions have configurable timeouts:

```csharp
// Default timeout: 5 seconds
var result = await _expressionService.EvaluateAsync<bool>(
    expression, 
    context,
    CancellationToken.None);

// Custom timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await _expressionService.EvaluateAsync<bool>(
    expression,
    context, 
    cts.Token);
```

## Performance Best Practices

### Expression Caching

Scripts are automatically cached by expression text:

```csharp
// ✅ Good - Reuse identical expressions
var commonExpression = "Variables.Amount > 1000";

// These will reuse the same compiled script
await _expressionService.EvaluateAsync<bool>(commonExpression, context1);
await _expressionService.EvaluateAsync<bool>(commonExpression, context2);

// ❌ Bad - Dynamic expressions don't cache well
var dynamicExpression = $"Variables.Amount > {threshold}"; // Different each time
```

### Complex Expression Optimization

```csharp
// ❌ Bad - Multiple LINQ operations
Variables.Items.Cast<Dictionary<string, object>>()
    .Where(item => (string)item["Category"] == "Premium")
    .Where(item => (decimal)item["Amount"] > 1000)
    .Sum(item => (decimal)item["Amount"])

// ✅ Good - Combined operations  
Variables.Items.Cast<Dictionary<string, object>>()
    .Where(item => (string)item["Category"] == "Premium" && (decimal)item["Amount"] > 1000)
    .Sum(item => (decimal)item["Amount"])

// ✅ Better - Precomputed in activity
var premiumTotal = GetVariable<decimal>(context, "PremiumTotal");
premiumTotal > 1000
```

### Memory Management

```csharp
// Expression service handles memory automatically
// Script cache has size limits and LRU eviction
// No manual cleanup required

// Large objects should be avoided in expressions
❌ Variables.LargeArray.Length > 1000
✅ Variables.ArrayLength > 1000  // Precompute in activity
```

## Error Handling

### Validation Errors

```csharp
// Validate expression syntax before evaluation
var validation = _expressionService.ValidateExpression("Variables.Amount >");

if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
    {
        _logger.LogError("Expression validation error: {Error}", error.Message);
    }
}
```

### Runtime Errors

```csharp
var result = await _expressionService.EvaluateAsync<bool>(expression, context);

if (!result.IsSuccess)
{
    switch (result.ErrorType)
    {
        case ExpressionErrorType.Timeout:
            // Expression took too long
            return ActivityResult.Failed("Expression evaluation timeout");
            
        case ExpressionErrorType.Runtime:
            // Runtime error (null reference, type cast, etc.)
            _logger.LogWarning("Expression runtime error: {Error}", result.ErrorMessage);
            return ActivityResult.Failed($"Expression error: {result.ErrorMessage}");
            
        case ExpressionErrorType.Security:
            // Security violation (prohibited operation)
            _logger.LogError("Expression security violation: {Error}", result.ErrorMessage);
            return ActivityResult.Failed("Expression security violation");
            
        default:
            return ActivityResult.Failed("Unknown expression error");
    }
}
```

## Configuration

### Expression Service Configuration

```json
{
  "Workflow": {
    "Expressions": {
      "DefaultTimeoutSeconds": 5,
      "MaxCacheSize": 1000,
      "MaxRecursionDepth": 50,
      "EnableDebugging": false
    }
  }
}
```

### Registration in DI Container

```csharp
services.Configure<ExpressionOptions>(configuration.GetSection("Workflow:Expressions"));
services.AddScoped<IWorkflowExpressionService, WorkflowExpressionService>();
services.AddMemoryCache();
```

## Testing Expressions

### Unit Testing

```csharp
[TestClass]
public class ExpressionTests
{
    private IWorkflowExpressionService _expressionService;
    
    [TestInitialize]
    public void Setup()
    {
        var logger = new Mock<ILogger<WorkflowExpressionService>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        _expressionService = new WorkflowExpressionService(logger.Object, cache);
    }
    
    [TestMethod]
    public async Task EvaluateAsync_SimpleComparison_ReturnsCorrectResult()
    {
        // Arrange
        var context = new ExpressionContext
        {
            Variables = new Dictionary<string, object>
            {
                ["Amount"] = 1500m,
                ["Threshold"] = 1000m
            }
        };
        
        // Act
        var result = await _expressionService.EvaluateAsync<bool>(
            "Variables.Amount > Variables.Threshold", 
            context);
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Value);
    }
    
    [TestMethod]
    public async Task EvaluateAsync_ComplexExpression_ReturnsCorrectResult()
    {
        // Arrange
        var context = new ExpressionContext
        {
            Variables = new Dictionary<string, object>
            {
                ["Amount"] = 15000m,
                ["Category"] = "Equipment",
                ["Requester"] = "john.doe@company.com"
            }
        };
        
        // Act
        var result = await _expressionService.EvaluateAsync<string>(
            @"Variables.Amount switch
              {
                  > 50000 => ""CEO"",
                  > 10000 when Variables.Category == ""Equipment"" => ""IT-Director"",
                  > 5000 => ""Manager"", 
                  _ => ""TeamLead""
              }", 
            context);
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("IT-Director", result.Value);
    }
}
```

## Common Patterns

### Approval Matrix

```csharp
// Approval routing based on amount and department
@"Variables.Department switch
{
    ""Finance"" => Variables.Amount switch
    {
        > 100000 => ""cfo@company.com"",
        > 50000 => ""finance-director@company.com"",
        > 10000 => ""finance-manager@company.com"",
        _ => ""finance-team@company.com""
    },
    ""IT"" => Variables.Amount > 25000 ? ""cto@company.com"" : ""it-manager@company.com"",
    ""HR"" => ""hr-director@company.com"",
    _ => ""default-approver@company.com""
}"
```

### Business Hours Check

```csharp
// Check if current time is within business hours
@"Variables.ExecutionTime.DayOfWeek >= DayOfWeek.Monday && 
  Variables.ExecutionTime.DayOfWeek <= DayOfWeek.Friday &&
  Variables.ExecutionTime.Hour >= 9 && 
  Variables.ExecutionTime.Hour < 17"
```

### SLA Calculation

```csharp
// Calculate SLA deadline based on priority
@"Variables.Priority switch
{
    ""Critical"" => Variables.CreatedDate.AddHours(4),
    ""High"" => Variables.CreatedDate.AddHours(24),
    ""Normal"" => Variables.CreatedDate.AddDays(3),
    ""Low"" => Variables.CreatedDate.AddDays(7),
    _ => Variables.CreatedDate.AddDays(3)
}"
```

## Migration from Legacy Expressions

### From Simple String Templates

```csharp
// Old: String interpolation
$"Amount {amount} exceeds limit {limit}"

// New: Expression evaluation
await _expressionService.EvaluateAsync<string>(
    @"""Amount "" + Variables.Amount + "" exceeds limit "" + Variables.Limit",
    context);
```

### From Hardcoded Logic

```csharp
// Old: Hardcoded C# logic
if (amount > 10000 && department == "Finance")
    return "finance-manager@company.com";
else if (amount > 5000)
    return "team-lead@company.com";
else
    return "auto-approved";

// New: Expression-based logic
await _expressionService.EvaluateAsync<string>(
    @"Variables.Amount > 10000 && Variables.Department == ""Finance""
        ? ""finance-manager@company.com""
        : Variables.Amount > 5000 
            ? ""team-lead@company.com"" 
            : ""auto-approved""",
    context);
```

This expression engine provides the flexibility needed for dynamic workflow logic while maintaining security and performance for production use.