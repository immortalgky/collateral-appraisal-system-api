namespace Appraisal.Domain.Appraisals;

public class VillageUnitUpload : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }
    public bool IsUsed { get; private set; }
    public Guid? DocumentId { get; private set; }

    private VillageUnitUpload()
    {
    }

    public static VillageUnitUpload Create(Guid appraisalId, string fileName, Guid? documentId = null)
    {
        return new VillageUnitUpload
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            FileName = fileName,
            UploadedAt = DateTime.Now,
            IsUsed = false,
            DocumentId = documentId
        };
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }

    public void MarkAsUnused()
    {
        IsUsed = false;
    }
}
