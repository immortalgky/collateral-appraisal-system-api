namespace Workflow.Tasks.ValueObjects;

public record TaskStatus
{
    public string Code { get; }
    public static TaskStatus Assigned => new("Assigned"); // Created by saga
    public static TaskStatus InProgress => new("InProgress"); // User started working
    public static TaskStatus Completing => new("Completing"); // User submitted completion
    public static TaskStatus Completed => new("Completed"); // Moved to CompletedTask

    private TaskStatus(string code)
    {
        Code = code;
    }

    public override string ToString() => Code;
    public static implicit operator string(TaskStatus taskStatus) => taskStatus.Code;
}
