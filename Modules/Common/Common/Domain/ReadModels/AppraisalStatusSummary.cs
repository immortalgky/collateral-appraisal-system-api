namespace Common.Domain.ReadModels;

public class AppraisalStatusSummary
{
    public string Status { get; set; } = default!;
    public int Count { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
