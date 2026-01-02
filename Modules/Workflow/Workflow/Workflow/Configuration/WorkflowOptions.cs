namespace Workflow.Workflow.Configuration;

public class WorkflowOptions
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// Configuration for outbox event processing
    /// </summary>
    public OutboxProcessingOptions OutboxProcessing { get; set; } = new();

    /// <summary>
    /// Configuration for timer processing
    /// </summary>
    public TimerProcessingOptions TimerProcessing { get; set; } = new();

    /// <summary>
    /// Configuration for cleanup operations
    /// </summary>
    public CleanupOptions Cleanup { get; set; } = new();

    public void Validate()
    {
        OutboxProcessing?.Validate();
        TimerProcessing?.Validate();
        Cleanup?.Validate();
    }
}

public class OutboxProcessingOptions
{
    /// <summary>
    /// Number of outbox events to process in each batch
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Interval between processing runs
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of retry attempts for failed events
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for retrying failed events
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (BatchSize <= 0)
            throw new InvalidOperationException("BatchSize must be positive");
            
        if (ProcessingInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("ProcessingInterval must be positive");
            
        if (MaxRetryAttempts < 0)
            throw new InvalidOperationException("MaxRetryAttempts cannot be negative");
            
        if (RetryDelay < TimeSpan.Zero)
            throw new InvalidOperationException("RetryDelay cannot be negative");
    }
}

public class TimerProcessingOptions
{
    /// <summary>
    /// Interval between timer processing checks
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Number of timer bookmarks to process in each batch
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Threshold for identifying long-running workflows that may have timed out
    /// </summary>
    public TimeSpan TimeoutThreshold { get; set; } = TimeSpan.FromHours(24);

    public void Validate()
    {
        if (CheckInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("CheckInterval must be positive");
            
        if (BatchSize <= 0)
            throw new InvalidOperationException("BatchSize must be positive");
            
        if (TimeoutThreshold <= TimeSpan.Zero)
            throw new InvalidOperationException("TimeoutThreshold must be positive");
    }
}

public class CleanupOptions
{
    /// <summary>
    /// Interval between cleanup runs
    /// </summary>
    public TimeSpan RunInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long to retain completed workflows
    /// </summary>
    public TimeSpan CompletedWorkflowRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long to retain execution logs
    /// </summary>
    public TimeSpan ExecutionLogRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// How long to retain processed outbox events
    /// </summary>
    public TimeSpan ProcessedOutboxRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Number of records to clean up in each batch
    /// </summary>
    public int BatchSize { get; set; } = 100;

    public void Validate()
    {
        if (RunInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("RunInterval must be positive");
            
        if (CompletedWorkflowRetention <= TimeSpan.Zero)
            throw new InvalidOperationException("CompletedWorkflowRetention must be positive");
            
        if (ExecutionLogRetention <= TimeSpan.Zero)
            throw new InvalidOperationException("ExecutionLogRetention must be positive");
            
        if (ProcessedOutboxRetention <= TimeSpan.Zero)
            throw new InvalidOperationException("ProcessedOutboxRetention must be positive");
            
        if (BatchSize <= 0)
            throw new InvalidOperationException("BatchSize must be positive");
    }
}