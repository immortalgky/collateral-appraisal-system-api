namespace Workflow.Workflow.Configuration;

public class WorkflowResilienceOptions
{
    public const string SectionName = "WorkflowResilience";

    /// <summary>
    /// Retry policy configuration for workflow operations
    /// </summary>
    public RetryPolicyOptions Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration for external dependencies
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Timeout configuration for various operations
    /// </summary>
    public TimeoutOptions Timeout { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();
    
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
    /// <summary>
    /// Maximum number of retry attempts for workflow operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries (exponential backoff)
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Jitter factor to add randomness to retry delays
    /// </summary>
    public double Jitter { get; set; } = 0.1;
    
    public void Validate()
    {
        if (MaxRetryAttempts < 0)
            throw new InvalidOperationException("MaxRetryAttempts cannot be negative");
            
        if (BaseDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("BaseDelay must be positive");
            
        if (MaxDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("MaxDelay must be positive");
            
        if (MaxDelay < BaseDelay)
            throw new InvalidOperationException("MaxDelay must be greater than or equal to BaseDelay");
            
        if (Jitter < 0.0 || Jitter > 1.0)
            throw new InvalidOperationException("Jitter must be between 0.0 and 1.0");
    }
}

public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time to keep the circuit open before attempting to close it
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Minimum number of requests in the closed state before the circuit can open
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Success rate threshold to close the circuit from half-open state
    /// </summary>
    public double SuccessThreshold { get; set; } = 0.8;
    
    public void Validate()
    {
        if (FailureThreshold <= 0)
            throw new InvalidOperationException("FailureThreshold must be positive");
            
        if (BreakDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("BreakDuration must be positive");
            
        if (MinimumThroughput <= 0)
            throw new InvalidOperationException("MinimumThroughput must be positive");
            
        if (SuccessThreshold < 0.0 || SuccessThreshold > 1.0)
            throw new InvalidOperationException("SuccessThreshold must be between 0.0 and 1.0");
    }
}

public class TimeoutOptions
{
    /// <summary>
    /// Timeout for database operations
    /// </summary>
    public TimeSpan DatabaseOperation { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for external HTTP calls
    /// </summary>
    public TimeSpan ExternalHttpCall { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Timeout for workflow activity execution
    /// </summary>
    public TimeSpan ActivityExecution { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for workflow instance startup
    /// </summary>
    public TimeSpan WorkflowStartup { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for workflow resumption
    /// </summary>
    public TimeSpan WorkflowResume { get; set; } = TimeSpan.FromSeconds(15);
    
    public void Validate()
    {
        if (DatabaseOperation <= TimeSpan.Zero)
            throw new InvalidOperationException("DatabaseOperation timeout must be positive");
            
        if (ExternalHttpCall <= TimeSpan.Zero)
            throw new InvalidOperationException("ExternalHttpCall timeout must be positive");
            
        if (ActivityExecution <= TimeSpan.Zero)
            throw new InvalidOperationException("ActivityExecution timeout must be positive");
            
        if (WorkflowStartup <= TimeSpan.Zero)
            throw new InvalidOperationException("WorkflowStartup timeout must be positive");
            
        if (WorkflowResume <= TimeSpan.Zero)
            throw new InvalidOperationException("WorkflowResume timeout must be positive");
    }
}

public class RateLimitOptions
{
    /// <summary>
    /// Maximum number of workflow starts per window
    /// </summary>
    public int WorkflowStartsPerWindow { get; set; } = 100;

    /// <summary>
    /// Rate limit window duration
    /// </summary>
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum concurrent workflow executions
    /// </summary>
    public int MaxConcurrentWorkflows { get; set; } = 50;
    
    public void Validate()
    {
        if (WorkflowStartsPerWindow <= 0)
            throw new InvalidOperationException("WorkflowStartsPerWindow must be positive");
            
        if (WindowDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("WindowDuration must be positive");
            
        if (MaxConcurrentWorkflows <= 0)
            throw new InvalidOperationException("MaxConcurrentWorkflows must be positive");
    }
}