namespace Common.Domain.ReadModels;

public class DailyAppraisalCount
{
    public DateOnly Date { get; set; }
    public int CreatedCount { get; set; }
    public int CompletedCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
