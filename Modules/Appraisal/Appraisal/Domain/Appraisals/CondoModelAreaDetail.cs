namespace Appraisal.Domain.Appraisals;

public class CondoModelAreaDetail : Entity<Guid>
{
    public string? AreaDescription { get; private set; }
    public decimal? AreaSize { get; private set; }

    private CondoModelAreaDetail()
    {
    }

    public static CondoModelAreaDetail Create(
        string? areaDescription,
        decimal? areaSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaDescription);

        if (areaSize < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        return new CondoModelAreaDetail
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
