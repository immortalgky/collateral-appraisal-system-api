namespace Appraisal.Domain.Appraisals.Hypothesis.Uploads;

/// <summary>
/// A single parsed row from a Land &amp; Building Excel upload.
/// FSD columns (Figure 50): Plan No, House No, Model Name, Location, Floor No,
/// Land Area (Sq.Wa), Usable Area (Sq.M), Selling Price (Baht), Remark 1, Remark 2.
/// </summary>
public class LandBuildingUnitRow : Entity<Guid>
{
    public Guid UploadId { get; private set; }
    public Guid HypothesisAnalysisId { get; private set; }
    public int SequenceNumber { get; private set; }

    public string? PlanNo { get; private set; }
    public string? HouseNo { get; private set; }
    public string? ModelName { get; private set; }
    public string? Location { get; private set; }
    public int? FloorNo { get; private set; }
    public decimal? LandAreaSqWa { get; private set; }
    public decimal? UsableAreaSqM { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public string? Remark1 { get; private set; }
    public string? Remark2 { get; private set; }

    private LandBuildingUnitRow() { }

    public static LandBuildingUnitRow Create(
        Guid uploadId,
        Guid hypothesisAnalysisId,
        int sequenceNumber,
        string? planNo,
        string? houseNo,
        string? modelName,
        string? location,
        int? floorNo,
        decimal? landAreaSqWa,
        decimal? usableAreaSqM,
        decimal? sellingPrice,
        string? remark1,
        string? remark2)
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
            Location = location,
            FloorNo = floorNo,
            LandAreaSqWa = landAreaSqWa,
            UsableAreaSqM = usableAreaSqM,
            SellingPrice = sellingPrice,
            Remark1 = remark1,
            Remark2 = remark2
        };
    }

    /// <summary>Deep-clone for CI carry-forward — newAnalysisId + the prior→new upload id map.</summary>
    public static LandBuildingUnitRow CloneForAnalysis(
        LandBuildingUnitRow source, Guid newAnalysisId, Guid newUploadId)
    {
        return new LandBuildingUnitRow
        {
            Id = Guid.CreateVersion7(),
            UploadId = newUploadId,
            HypothesisAnalysisId = newAnalysisId,
            SequenceNumber = source.SequenceNumber,
            PlanNo = source.PlanNo,
            HouseNo = source.HouseNo,
            ModelName = source.ModelName,
            Location = source.Location,
            FloorNo = source.FloorNo,
            LandAreaSqWa = source.LandAreaSqWa,
            UsableAreaSqM = source.UsableAreaSqM,
            SellingPrice = source.SellingPrice,
            Remark1 = source.Remark1,
            Remark2 = source.Remark2
        };
    }
}
