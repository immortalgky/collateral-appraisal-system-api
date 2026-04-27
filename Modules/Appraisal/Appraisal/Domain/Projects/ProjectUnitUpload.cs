namespace Appraisal.Domain.Projects;

/// <summary>
/// Tracks a CSV upload batch for project units (replaces CondoUnitUpload + VillageUnitUpload).
/// FK is ProjectId (not AppraisalId).
/// </summary>
public class ProjectUnitUpload : Entity<Guid>
{
    public Guid ProjectId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }
    public bool IsUsed { get; private set; }
    public Guid? DocumentId { get; private set; }

    private ProjectUnitUpload()
    {
    }

    public static ProjectUnitUpload Create(Guid projectId, string fileName, Guid? documentId = null)
    {
        return new ProjectUnitUpload
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
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
