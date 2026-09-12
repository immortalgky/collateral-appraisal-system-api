namespace Appraisal.Domain.Projects;

public class ProjectModelAreaDetail : Entity<Guid>
{
    public int? Sequence { get; set; }
    public string? AreaDescription { get; private set; }
    public decimal? AreaSize { get; private set; }

    private ProjectModelAreaDetail()
    {
    }

    public static ProjectModelAreaDetail Create(
        int? sequence,
        string? areaDescription,
        decimal? areaSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaDescription);

        if (areaSize < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        return new ProjectModelAreaDetail
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
