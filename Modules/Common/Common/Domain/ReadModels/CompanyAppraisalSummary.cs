namespace Common.Domain.ReadModels;

public class CompanyAppraisalSummary
{
    public Guid CompanyId { get; set; }
    public DateOnly Date { get; set; }
    public string CompanyName { get; set; } = default!;
    public int AssignedCount { get; set; }
    public int CompletedCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
