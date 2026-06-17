namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Read-only entity representing an asset group summary row from legacy migration data.
/// Holds pre-aggregated pricing totals for one GroupSet within an appraisal book.
/// No write path — data is inserted by the migration process only.
/// Linked to AssetSummary rows via AppraisalId + GroupSet (no FK constraint in DB).
/// </summary>
public class AssetSummaryGroup : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public int GroupSet { get; private set; }
    public string? AssetGroupDetail { get; private set; }
    public decimal SumEstimatedPrice { get; private set; }
    public decimal RoundEstimatedPrice { get; private set; }
    public decimal SumCurrentPrice { get; private set; }
    public decimal RoundCurrentPrice { get; private set; }

    // Private constructor for EF Core
    private AssetSummaryGroup() { }
}
