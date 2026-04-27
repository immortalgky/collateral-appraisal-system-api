namespace Appraisal.Domain.Projects;

public class ProjectModelAreaDetail : Entity<Guid>
{
    public string? AreaDescription { get; private set; }
    public decimal? AreaSize { get; private set; }

    private ProjectModelAreaDetail()
    {
    }

    public static ProjectModelAreaDetail Create(
        string? areaDescription,
        decimal? areaSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaDescription);

        if (areaSize < 0)
            throw new ArgumentException("AreaSize cannot be negative");

        return new ProjectModelAreaDetail
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
