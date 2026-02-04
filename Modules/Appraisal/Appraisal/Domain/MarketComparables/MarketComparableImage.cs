namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Photos attached to market comparables.
/// Child entity of MarketComparable aggregate.
/// References documents via DocumentId (uploaded via Document module).
/// </summary>
public class MarketComparableImage : Entity<Guid>
{
    public Guid MarketComparableId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }

    private MarketComparableImage() { }

    internal static MarketComparableImage Create(
        Guid marketComparableId,
        int displaySequence,
        Guid documentId,
        string? title = null,
        string? description = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty", nameof(documentId));

        return new MarketComparableImage
        {
            // Id = Guid.NewGuid(),
            MarketComparableId = marketComparableId,
            DisplaySequence = displaySequence,
            DocumentId = documentId,
            Title = title,
            Description = description
        };
    }

    internal void UpdateDetails(string? title, string? description)
    {
        Title = title;
        Description = description;
    }

    internal void UpdateSequence(int newSequence)
    {
        DisplaySequence = newSequence;
    }
}
