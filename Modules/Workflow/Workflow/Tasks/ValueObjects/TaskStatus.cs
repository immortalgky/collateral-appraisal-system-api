namespace Workflow.Tasks.ValueObjects;

public record TaskStatus
{
    public string Code { get; }
    public static TaskStatus Assigned => new("ASSIGNED"); // Created by saga
    public static TaskStatus InProgress => new("IN_PROGRESS"); // User started working
    public static TaskStatus Completing => new("COMPLETING"); // User submitted completion
    public static TaskStatus Completed => new("COMPLETED"); // Moved to CompletedTask

    private TaskStatus(string code)
    {
        Code = code;
    }

    public override string ToString() => Code;
    public static implicit operator string(TaskStatus taskStatus) => taskStatus.Code;
}