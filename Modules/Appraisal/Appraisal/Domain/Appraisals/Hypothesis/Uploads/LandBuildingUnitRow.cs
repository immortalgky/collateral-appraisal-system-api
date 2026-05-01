namespace Appraisal.Domain.Appraisals.Hypothesis.Uploads;

/// <summary>
/// A single parsed row from a Land &amp; Building Excel upload.
/// FSD columns: Plan No, House No, Model Name, Land Area (Sq.Wa), Project Selling Price.
/// </summary>
public class LandBuildingUnitRow : Entity<Guid>
{
    public Guid UploadId { get; private set; }
    public Guid HypothesisAnalysisId { get; private set; }
    public int SequenceNumber { get; private set; }

    public string? PlanNo { get; private set; }
    public string? HouseNo { get; private set; }
    public string? ModelName { get; private set; }
    public decimal? LandAreaSqWa { get; private set; }
    public decimal? SellingPrice { get; private set; }

    private LandBuildingUnitRow() { }

    public static LandBuildingUnitRow Create(
        Guid uploadId,
        Guid hypothesisAnalysisId,
        int sequenceNumber,
        string? planNo,
        string? houseNo,
        string? modelName,
        decimal? landAreaSqWa,
        decimal? sellingPrice)
    {
        return new LandBuildingUnitRow
        {
            Id = Guid.CreateVersion7(),
            UploadId = uploadId,
            HypothesisAnalysisId = hypothesisAnalysisId,
            SequenceNumber = sequenceNumber,
            PlanNo = planNo,
            HouseNo = houseNo,
            ModelName = modelName,
            LandAreaSqWa = landAreaSqWa,
            SellingPrice = sellingPrice
        };
    }
}
