namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Area breakdown per room type for condo appraisals.
/// </summary>
public class CondoAppraisalAreaDetail : Entity<Guid>
{
    // Area Details
    public string? AreaDescription { get; private set; } // Balcony, AirCondLedge, LivingRoom, Bedroom, etc.
    public decimal? AreaSize { get; private set; } // Size in Sq.m

    private CondoAppraisalAreaDetail()
    {
    }

    public static CondoAppraisalAreaDetail Create(
        string? areaDescription,
        decimal? areaSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaDescription);

        if (areaSize < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        return new CondoAppraisalAreaDetail
        {
            Id = Guid.CreateVersion7(),
            AreaDescription = areaDescription,
            AreaSize = areaSize
        };
    }

    public void UpdateArea(string? description, decimal? size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (size < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        AreaDescription = description;
        AreaSize = size;
    }
}