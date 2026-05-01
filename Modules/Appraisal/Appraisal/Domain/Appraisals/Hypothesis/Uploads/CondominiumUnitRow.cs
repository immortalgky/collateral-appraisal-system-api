namespace Appraisal.Domain.Appraisals.Hypothesis.Uploads;

/// <summary>
/// A single parsed row from a Condominium Excel upload.
/// FSD columns: Floor No, Building, Apt No, Model Type, Usable Area (SqM), Selling Price.
/// </summary>
public class CondominiumUnitRow : Entity<Guid>
{
    public Guid UploadId { get; private set; }
    public Guid HypothesisAnalysisId { get; private set; }
    public int SequenceNumber { get; private set; }

    public int? FloorNo { get; private set; }
    public string? Building { get; private set; }
    public string? AptNo { get; private set; }
    public string? ModelType { get; private set; }
    public decimal? UsableAreaSqM { get; private set; }
    public decimal? SellingPrice { get; private set; }

    private CondominiumUnitRow() { }

    public static CondominiumUnitRow Create(
        Guid uploadId,
        Guid hypothesisAnalysisId,
        int sequenceNumber,
        int? floorNo,
        string? building,
        string? aptNo,
        string? modelType,
        decimal? usableAreaSqM,
        decimal? sellingPrice)
    {
        return new CondominiumUnitRow
        {
            Id = Guid.CreateVersion7(),
            UploadId = uploadId,
            HypothesisAnalysisId = hypothesisAnalysisId,
            SequenceNumber = sequenceNumber,
            FloorNo = floorNo,
            Building = building,
            AptNo = aptNo,
            ModelType = modelType,
            UsableAreaSqM = usableAreaSqM,
            SellingPrice = sellingPrice
        };
    }
}
