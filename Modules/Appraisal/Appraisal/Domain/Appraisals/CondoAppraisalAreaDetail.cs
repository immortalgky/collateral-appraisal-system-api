namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Area breakdown per room type for condo appraisals.
/// </summary>
public class CondoAppraisalAreaDetail : Entity<Guid>
{
    // Area Details
    public int? Sequence { get; set; }
    public string? AreaDescription { get; private set; } // Balcony, AirCondLedge, LivingRoom, Bedroom, etc.
    public decimal? AreaSize { get; private set; } // Size in Sq.m

    private CondoAppraisalAreaDetail()
    {
    }

    public static CondoAppraisalAreaDetail Create(
        int? sequence,
        string? areaDescription,
        decimal? areaSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaDescription);

        if (areaSize < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        return new CondoAppraisalAreaDetail
        {
            Id = Guid.CreateVersion7(),
            Sequence = sequence,
            AreaDescription = areaDescription,
            AreaSize = areaSize
        };
    }

    public void UpdateArea(int? sequence, string? description, decimal? size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (size < 0)
            throw new ArgumentException("AreaSize cannot be negative");
        Sequence = sequence;
        AreaDescription = description;
        AreaSize = size;
    }
}