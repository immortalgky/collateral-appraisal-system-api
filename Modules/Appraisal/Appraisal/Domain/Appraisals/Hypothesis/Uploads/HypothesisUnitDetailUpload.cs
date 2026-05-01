namespace Appraisal.Domain.Appraisals.Hypothesis.Uploads;

/// <summary>
/// Tracks each Excel upload for a Hypothesis analysis.
/// Only one upload per analysis can be IsActive = true at a time.
/// </summary>
public class HypothesisUnitDetailUpload : Entity<Guid>
{
    public Guid HypothesisAnalysisId { get; private set; }
    public string FileName { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }
    public bool IsActive { get; private set; }
    public int RowCount { get; private set; }

    private HypothesisUnitDetailUpload() { }

    public static HypothesisUnitDetailUpload Create(
        Guid hypothesisAnalysisId,
        string fileName,
        DateTime uploadedAt,
        int rowCount)
    {
        return new HypothesisUnitDetailUpload
        {
            Id = Guid.CreateVersion7(),
            HypothesisAnalysisId = hypothesisAnalysisId,
            FileName = fileName,
            UploadedAt = uploadedAt,
            RowCount = rowCount,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
