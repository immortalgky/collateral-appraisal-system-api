namespace Common.Domain.ReadModels;

public class RequestStatusSummary
{
    public string Status { get; set; } = default!;
    public int Count { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
