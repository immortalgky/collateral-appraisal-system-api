namespace Assignment.Workflow.Schema;

public class WorkflowSchema
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public List<ActivityDefinition> Activities { get; set; } = new();
    public List<TransitionDefinition> Transitions { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public WorkflowMetadata Metadata { get; set; } = new();
}

public class ActivityDefinition
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Dictionary<string, object> Properties { get; set; } = new();
    public ActivityPosition Position { get; set; } = new();
    public List<string> RequiredRoles { get; set; } = new();
    public TimeSpan? TimeoutDuration { get; set; }
    public bool IsStartActivity { get; set; }
    public bool IsEndActivity { get; set; }
}

public class TransitionDefinition
{
    public string Id { get; set; } = default!;
    public string From { get; set; } = default!;
    public string To { get; set; } = default!;
    public string? Condition { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public TransitionType Type { get; set; } = TransitionType.Normal;
}

public class ActivityPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class WorkflowMetadata
{
    public string Author { get; set; } = default!;
    public DateTime CreatedDate { get; set; }
    public string Version { get; set; } = "1.0";
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

public enum TransitionType
{
    Normal,
    Conditional,
    Exception,
    Timeout
}

public static class ActivityTypes
{
    public const string TaskActivity = "TaskActivity";
    public const string DecisionActivity = "DecisionActivity";
    public const string ServiceActivity = "ServiceActivity";
    public const string TimerActivity = "TimerActivity";
    public const string NotificationActivity = "NotificationActivity";
    public const string StartActivity = "StartActivity";
    public const string EndActivity = "EndActivity";
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