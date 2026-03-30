namespace Common.Domain.ReadModels;

public class TeamWorkloadSummary
{
    public string Username { get; set; } = default!;
    public string? TeamId { get; set; }
    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
