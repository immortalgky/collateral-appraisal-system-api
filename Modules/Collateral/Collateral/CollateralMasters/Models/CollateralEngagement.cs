namespace Collateral.CollateralMasters.Models;

public class CollateralEngagement
{
    public Guid Id { get; private set; }
    public Guid CollateralMasterId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string AppraisalNumber { get; private set; } = null!;
    public Guid RequestId { get; private set; }
    public string RequestNumber { get; private set; } = null!;
    // PropertyId dropped (PR-4): engagement is now per-appraisal, not per-property.
    // Members live inside the Snapshot's groups[*].properties[] array.
    public string AppraisalType { get; private set; } = null!;
    public DateTime AppraisalDate { get; private set; }
    // AppraisedValue dropped (PR-4): group-level values live on master detail rows
    // (LandDetail.AppraisalValue etc.) and inside the engagement Snapshot JSON.
    public string? AppraiserUserId { get; private set; }
    public Guid? AppraisalCompanyId { get; private set; }
    public string? AppraisalCompanyName { get; private set; }
    // Construction Inspection Fee captured from this engagement's AppraisalFee.
    // Reused as the appraisal fee when a future Construction Inspection appraisal is created
    // for the same collateral (CI bypasses the normal tier/quotation pipeline).
    public decimal? ConstructionInspectionFeeAmount { get; private set; }
    public string Snapshot { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private CollateralEngagement() { }

    internal CollateralEngagement(
        Guid collateralMasterId,
        Guid appraisalId,
        string appraisalNumber,
        Guid requestId,
        string requestNumber,
        string appraisalType,
        DateTime appraisalDate,
        string? appraiserUserId,
        Guid? appraisalCompanyId,
        string? appraisalCompanyName,
        decimal? constructionInspectionFeeAmount,
        string snapshot)
    {
        Id = Guid.CreateVersion7();
        CollateralMasterId = collateralMasterId;
        AppraisalId = appraisalId;
        AppraisalNumber = appraisalNumber;
        RequestId = requestId;
        RequestNumber = requestNumber;
        AppraisalType = appraisalType;
        AppraisalDate = appraisalDate;
        AppraiserUserId = appraiserUserId;
        AppraisalCompanyId = appraisalCompanyId;
        AppraisalCompanyName = appraisalCompanyName;
        ConstructionInspectionFeeAmount = constructionInspectionFeeAmount;
        Snapshot = snapshot;
        CreatedAt = DateTime.UtcNow;
    }
}
