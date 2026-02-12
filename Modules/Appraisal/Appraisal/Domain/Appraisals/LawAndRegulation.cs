namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Legal and regulatory information per appraisal.
/// </summary>
public class LawAndRegulation : Entity<Guid>
{
    private readonly List<LawAndRegulationImage> _images = [];
    public IReadOnlyList<LawAndRegulationImage> Images => _images.AsReadOnly();

    public Guid AppraisalId { get; private set; }

    // Regulation Details
    public string HeaderCode { get; private set; } = null!; // Regulation category code
    public string? Remark { get; private set; }

    private LawAndRegulation()
    {
    }

    public static LawAndRegulation Create(
        Guid appraisalId,
        string headerCode,
        string? remark = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerCode);

        return new LawAndRegulation
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            HeaderCode = headerCode,
            Remark = remark
        };
    }

    public LawAndRegulationImage AddImage(
        int displaySequence,
        string fileName,
        string filePath,
        string? title = null,
        string? description = null)
    {
        var image = LawAndRegulationImage.Create(
            Id, displaySequence, fileName, filePath, title, description);
        _images.Add(image);
        return image;
    }

    public void SetRemark(string? remark)
    {
        Remark = remark;
    }

    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image != null) _images.Remove(image);
    }
}