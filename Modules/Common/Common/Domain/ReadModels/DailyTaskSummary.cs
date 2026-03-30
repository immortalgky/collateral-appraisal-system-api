namespace Common.Domain.ReadModels;

public class DailyTaskSummary
{
    public DateOnly Date { get; set; }
    public string Username { get; set; } = default!;
    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Overdue { get; set; }
    public int Completed { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
