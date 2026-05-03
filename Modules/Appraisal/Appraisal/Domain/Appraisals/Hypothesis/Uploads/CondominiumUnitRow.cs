namespace Appraisal.Domain.Appraisals.Hypothesis.Uploads;

/// <summary>
/// A single parsed row from a Condominium Excel upload.
/// FSD columns (Figure 66): Floor No, Building, Apartment No, Apartment, Apartment Type,
/// Condo Area (Sq.M), Selling Price (Baht), Remark 1, Remark 2.
/// </summary>
public class CondominiumUnitRow : Entity<Guid>
{
    public Guid UploadId { get; private set; }
    public Guid HypothesisAnalysisId { get; private set; }
    public int SequenceNumber { get; private set; }

    public int? FloorNo { get; private set; }
    public string? Building { get; private set; }
    public string? AptNo { get; private set; }
    public string? Apartment { get; private set; }
    public string? ModelType { get; private set; }
    public decimal? UsableAreaSqM { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public string? Remark1 { get; private set; }
    public string? Remark2 { get; private set; }

    private CondominiumUnitRow() { }

    public static CondominiumUnitRow Create(
        Guid uploadId,
        Guid hypothesisAnalysisId,
        int sequenceNumber,
        int? floorNo,
        string? building,
        string? aptNo,
        string? apartment,
        string? modelType,
        decimal? usableAreaSqM,
        decimal? sellingPrice,
        string? remark1,
        string? remark2)
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
            Apartment = apartment,
            ModelType = modelType,
            UsableAreaSqM = usableAreaSqM,
            SellingPrice = sellingPrice,
            Remark1 = remark1,
            Remark2 = remark2
        };
    }
}
