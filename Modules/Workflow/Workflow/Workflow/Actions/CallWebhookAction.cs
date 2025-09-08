using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Resilience;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Workflow.Workflow.Actions;

/// <summary>
/// Action that calls external webhooks during workflow execution
/// Enables integration with external systems and third-party services
/// </summary>
public class CallWebhookAction : WorkflowActionBase
{
    private readonly HttpClient _httpClient;
    private readonly IWebhookSecurityService _securityService;
    private readonly IWorkflowResilienceService _resilienceService;

    public CallWebhookAction(
        HttpClient httpClient, 
        IWebhookSecurityService securityService, 
        IWorkflowResilienceService resilienceService,
        ILogger<CallWebhookAction> logger) : base(logger)
    {
        _httpClient = httpClient;
        _securityService = securityService;
        _resilienceService = resilienceService;
    }

    public override string ActionType => "CallWebhook";
    public override string Name => "Call Webhook";
    public override string Description => "Calls external webhooks with workflow data for third-party integrations";

    protected override async Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var url = GetParameter<string>(actionParameters, "url");
        var method = GetParameter<string>(actionParameters, "method", "POST");
        var payload = GetParameter<Dictionary<string, object>>(actionParameters, "payload", new Dictionary<string, object>());
        var headers = GetParameter<Dictionary<string, string>>(actionParameters, "headers", new Dictionary<string, string>());
        var timeoutSeconds = GetParameter<int>(actionParameters, "timeoutSeconds", 30);
        var includeWorkflowContext = GetParameter<bool>(actionParameters, "includeWorkflowContext", true);
        var includeWorkflowVariables = GetParameter<bool>(actionParameters, "includeWorkflowVariables", false);
        var authMethod = GetParameter<string>(actionParameters, "authMethod", "None");
        var secretName = GetParameter<string>(actionParameters, "secretName");
        var retryCount = GetParameter<int>(actionParameters, "retryCount", 3);
        var expectedStatusCodes = GetParameter<List<int>>(actionParameters, "expectedStatusCodes", new List<int> { 200, 201, 202 });

        Logger.LogDebug("Calling webhook {Method} {Url} for activity {ActivityId}",
            method.ToUpper(), url, context.ActivityId);

        try
        {
            // Resolve URL expressions
            var resolvedUrl = ResolveVariableExpressions(url, context);

            // Validate and parse URL
            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            {
                return ActionExecutionResult.Failed($"Invalid webhook URL: {resolvedUrl}");
            }

            // Prepare payload with workflow context
            var finalPayload = PrepareWebhookPayload(payload, context, includeWorkflowContext, includeWorkflowVariables);

            // Create HTTP request
            var request = new HttpRequestMessage(new HttpMethod(method.ToUpper()), uri);

            // Add headers
            foreach (var header in headers)
            {
                var resolvedHeaderValue = ResolveVariableExpressions(header.Value, context);
                
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(finalPayload),
                        Encoding.UTF8,
                        resolvedHeaderValue);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, resolvedHeaderValue);
                }
            }

            // Set default content type if not specified and we have a payload
            if (finalPayload.Any() && !headers.ContainsKey("Content-Type"))
            {
                var jsonContent = JsonSerializer.Serialize(finalPayload);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }

            // Add authentication
            await AddAuthenticationAsync(request, authMethod, secretName, cancellationToken);

            // Execute webhook with resilience patterns (circuit breaker, retry, timeout)
            var webhookPolicy = new ResiliencePolicy
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxRetryAttempts = retryCount,
                    BaseDelay = TimeSpan.FromSeconds(1),
                    BackoffStrategy = BackoffStrategy.ExponentialWithJitter
                },
                TimeoutPolicy = new TimeoutPolicy
                {
                    OperationTimeout = TimeSpan.FromSeconds(timeoutSeconds),
                    TimeoutStrategy = TimeoutStrategy.Pessimistic
                },
                CircuitBreakerPolicy = CircuitBreakerPolicy.Sensitive
            };

            var operationName = $"Webhook.{new Uri(resolvedUrl).Host}";
            var (response, statusCode, responseContent) = await _resilienceService.ExecuteExternalServiceCallAsync(
                operationName,
                async ct => await ExecuteWebhookCallAsync(request, expectedStatusCodes, ct),
                fallbackProvider: ex => (null, System.Net.HttpStatusCode.ServiceUnavailable, $"Webhook call failed: {ex.Message}"),
                policy: webhookPolicy,
                cancellationToken: cancellationToken);

            var isSuccess = response != null && expectedStatusCodes.Contains((int)statusCode);

            if (!isSuccess)
            {
                var errorMessage = $"Webhook call failed. Status: {statusCode}, Response: {responseContent}";
                Logger.LogError("Webhook call failed for activity {ActivityId}: {StatusCode} - {Response}",
                    context.ActivityId, statusCode, responseContent);
                
                return ActionExecutionResult.Failed(errorMessage);
            }

            var resultMessage = $"Webhook call successful: {method.ToUpper()} {resolvedUrl} returned {statusCode}";
            var outputData = new Dictionary<string, object>
            {
                ["url"] = resolvedUrl,
                ["method"] = method.ToUpper(),
                ["statusCode"] = (int)statusCode,
                ["response"] = responseContent,
                ["responseLength"] = responseContent.Length,
                ["completedAt"] = DateTime.UtcNow,
                ["success"] = true
            };

            // Try to parse JSON response for easier access
            try
            {
                if (!string.IsNullOrEmpty(responseContent) && responseContent.Trim().StartsWith("{"))
                {
                    var parsedResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                    if (parsedResponse != null)
                    {
                        outputData["responseData"] = parsedResponse;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not parse webhook response as JSON");
                // Not a critical error - response content is still available as string
            }

            Logger.LogInformation("Successfully called webhook {Method} {Url} for activity {ActivityId} - Status: {StatusCode}",
                method.ToUpper(), resolvedUrl, context.ActivityId, statusCode);

            return ActionExecutionResult.Success(resultMessage, outputData);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error calling webhook: {ex.Message}";
            Logger.LogError(ex, "Error calling webhook for activity {ActivityId}",
                context.ActivityId);
            
            return ActionExecutionResult.Failed(errorMessage);
        }
    }

    public override Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required parameters
        ValidateRequiredParameter(actionParameters, "url", errors);

        var url = GetParameter<string>(actionParameters, "url");
        var method = GetParameter<string>(actionParameters, "method", "POST");
        var timeoutSeconds = GetParameter<int>(actionParameters, "timeoutSeconds", 30);
        var retryCount = GetParameter<int>(actionParameters, "retryCount", 3);
        var authMethod = GetParameter<string>(actionParameters, "authMethod", "None");

        // Validate URL format
        if (!string.IsNullOrEmpty(url) && !url.Contains("${"))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                errors.Add($"Invalid URL format: {url}");
            }
            else if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                errors.Add($"URL must use HTTP or HTTPS protocol: {url}");
            }
            else if (uri.Scheme == "http")
            {
                warnings.Add($"Using HTTP instead of HTTPS may expose sensitive data: {url}");
            }
        }

        // Validate HTTP method
        var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
        if (!validMethods.Contains(method.ToUpper()))
        {
            errors.Add($"Invalid HTTP method '{method}'. Valid methods: {string.Join(", ", validMethods)}");
        }

        // Validate timeout
        if (timeoutSeconds <= 0 || timeoutSeconds > 300)
        {
            warnings.Add($"Timeout of {timeoutSeconds} seconds may be too short or too long. Recommended range: 5-60 seconds.");
        }

        // Validate retry count
        if (retryCount < 0 || retryCount > 10)
        {
            warnings.Add($"Retry count of {retryCount} may be excessive. Recommended range: 0-5.");
        }

        // Validate authentication method
        var validAuthMethods = new[] { "None", "Bearer", "Basic", "ApiKey", "Signature" };
        if (!validAuthMethods.Contains(authMethod))
        {
            errors.Add($"Invalid authentication method '{authMethod}'. Valid methods: {string.Join(", ", validAuthMethods)}");
        }

        // Warn about authentication requirements
        if (authMethod != "None" && string.IsNullOrEmpty(GetParameter<string>(actionParameters, "secretName")))
        {
            warnings.Add($"Authentication method '{authMethod}' specified but no secret name provided. Webhook may fail authentication.");
        }

        return Task.FromResult(errors.Any() ? 
            ActionValidationResult.Invalid(errors, warnings) : 
            ActionValidationResult.Valid(warnings));
    }

    private Dictionary<string, object> PrepareWebhookPayload(
        Dictionary<string, object> payload,
        ActivityContext context,
        bool includeWorkflowContext,
        bool includeWorkflowVariables)
    {
        var finalPayload = new Dictionary<string, object>();

        // Add original payload with expression resolution
        foreach (var kvp in payload)
        {
            if (kvp.Value is string stringValue)
            {
                finalPayload[kvp.Key] = ResolveVariableExpressions(stringValue, context);
            }
            else
            {
                finalPayload[kvp.Key] = kvp.Value;
            }
        }

        // Add workflow context if requested
        if (includeWorkflowContext)
        {
            finalPayload["workflowContext"] = new Dictionary<string, object>
            {
                ["instanceId"] = context.WorkflowInstanceId,
                ["activityId"] = context.ActivityId,
                ["assignee"] = context.CurrentAssignee ?? "",
                ["timestamp"] = DateTime.UtcNow
            };
        }

        // Add workflow variables if requested (filter sensitive data)
        if (includeWorkflowVariables)
        {
            var filteredVariables = context.Variables
                .Where(kvp => !IsSensitiveVariable(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            finalPayload["workflowVariables"] = filteredVariables;
        }

        return finalPayload;
    }

    private async Task AddAuthenticationAsync(HttpRequestMessage request, string authMethod, string? secretName, CancellationToken cancellationToken)
    {
        if (authMethod == "None" || string.IsNullOrEmpty(secretName))
            return;

        try
        {
            var authConfig = await _securityService.GetWebhookAuthConfigAsync(secretName, cancellationToken);
            
            switch (authMethod.ToLower())
            {
                case "bearer":
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authConfig.Token);
                    break;
                
                case "basic":
                    var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
                    break;
                
                case "apikey":
                    request.Headers.Add(authConfig.HeaderName ?? "X-API-Key", authConfig.ApiKey);
                    break;
                
                case "signature":
                    var signature = await _securityService.GenerateWebhookSignatureAsync(
                        request.Content?.ReadAsStringAsync(cancellationToken).Result ?? "", 
                        authConfig.Secret ?? "",
                        cancellationToken);
                    request.Headers.Add(authConfig.HeaderName ?? "X-Signature", signature);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to add authentication for webhook. Secret: {SecretName}", secretName);
        }
    }

    /// <summary>
    /// Executes a single webhook call with resilience handling
    /// </summary>
    private async Task<(HttpResponseMessage? Response, System.Net.HttpStatusCode StatusCode, string Content)> ExecuteWebhookCallAsync(
        HttpRequestMessage request, 
        List<int> expectedStatusCodes, 
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken) : "";
            
            // Consider the call successful if it matches expected status codes
            if (expectedStatusCodes.Contains((int)response.StatusCode))
            {
                return (response, response.StatusCode, content);
            }
            
            // Throw exception for unexpected status codes to trigger retry
            throw new HttpRequestException($"Webhook returned unexpected status code: {response.StatusCode}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException("Webhook call timed out", ex);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Webhook call failed with exception");
            throw;
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        if (original.Content != null)
        {
            var content = await original.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, Encoding.UTF8, original.Content.Headers.ContentType?.MediaType ?? "application/json");
            
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        
        return clone;
    }

    private static bool IsSensitiveVariable(string variableName)
    {
        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "credential",
            "ssn", "social", "credit", "bank", "account", "pin"
        };

        return sensitivePatterns.Any(pattern => 
            variableName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Interface for webhook security service - manages authentication and secrets
/// </summary>
public interface IWebhookSecurityService
{
    /// <summary>
    /// Gets webhook authentication configuration
    /// </summary>
    /// <param name="secretName">Name of the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication configuration</returns>
    Task<WebhookAuthConfig> GetWebhookAuthConfigAsync(string secretName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates webhook signature for request authentication
    /// </summary>
    /// <param name="payload">Request payload</param>
    /// <param name="secret">Signing secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated signature</returns>
    Task<string> GenerateWebhookSignatureAsync(string payload, string secret, CancellationToken cancellationToken = default);
}

/// <summary>
/// Webhook authentication configuration
/// </summary>
public class WebhookAuthConfig
{
    public string? Token { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? ApiKey { get; init; }
    public string? Secret { get; init; }
    public string? HeaderName { get; init; }
}