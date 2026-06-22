namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Read-only entity representing a single asset (property) row from legacy migration data.
/// No write path — data is inserted by the migration process only.
/// </summary>
public class AssetSummary : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string PropertyType { get; private set; }
    public string? AssetDetail { get; private set; }
    public decimal? Area { get; private set; }
    public decimal? PricePerUnit { get; private set; }
    public decimal? EstimatedPrice { get; private set; }
    public decimal? CurrentPrice { get; private set; }
    public int GroupSet { get; private set; }
    public bool? IsPricesCurrent { get; private set; }

    // Private constructor for EF Core
    private AssetSummary() { }
}
