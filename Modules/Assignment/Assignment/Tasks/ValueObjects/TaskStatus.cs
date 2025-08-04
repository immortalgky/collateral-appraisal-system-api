namespace Assignment.Tasks.ValueObjects;

public record TaskStatus
{
    public string Code { get; }
    public static TaskStatus Assigned => new(nameof(Assigned).ToUpper()); // Created by saga
    public static TaskStatus InProgress => new(nameof(InProgress).ToUpper()); // User started working  
    public static TaskStatus Completing => new(nameof(Completing).ToUpper()); // User submitted completion
    public static TaskStatus Completed => new(nameof(Completed).ToUpper()); // Moved to CompletedTask

    private TaskStatus(string code)
    {
        Code = code;
    }

    public override string ToString() => Code;
    public static implicit operator string(TaskStatus taskStatus) => taskStatus.Code;
}