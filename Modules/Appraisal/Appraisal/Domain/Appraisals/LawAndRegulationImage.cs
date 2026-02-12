namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Images attached to law and regulation records.
/// </summary>
public class LawAndRegulationImage : Entity<Guid>
{
    public Guid LawAndRegulationId { get; private set; }

    // Image Details
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public string FileName { get; private set; } = null!;
    public string FilePath { get; private set; } = null!;

    private LawAndRegulationImage()
    {
    }

    public static LawAndRegulationImage Create(
        Guid lawAndRegulationId,
        int displaySequence,
        string fileName,
        string filePath,
        string? title = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new LawAndRegulationImage
        {
            Id = Guid.CreateVersion7(),
            LawAndRegulationId = lawAndRegulationId,
            DisplaySequence = displaySequence,
            FileName = fileName,
            FilePath = filePath,
            Title = title,
            Description = description
        };
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    public void SetMetadata(string? title, string? description)
    {
        Title = title;
        Description = description;
    }
}