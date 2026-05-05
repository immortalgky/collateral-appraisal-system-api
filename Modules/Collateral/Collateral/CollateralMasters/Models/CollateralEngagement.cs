namespace Collateral.CollateralMasters.Models;

public class CollateralEngagement
{
    public Guid Id { get; private set; }
    public Guid CollateralMasterId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string AppraisalNumber { get; private set; } = null!;
    public Guid RequestId { get; private set; }
    public string RequestNumber { get; private set; } = null!;
    public Guid PropertyId { get; private set; }
    public string AppraisalType { get; private set; } = null!;
    public DateTime AppraisalDate { get; private set; }
    public decimal? AppraisedValue { get; private set; }
    public string? AppraiserUserId { get; private set; }
    public Guid? AppraisalCompanyId { get; private set; }
    public string? AppraisalCompanyName { get; private set; }
    public string Snapshot { get; private set; } = null!;
    public DateTime CreatedOn { get; private set; }

    private CollateralEngagement() { }

    internal CollateralEngagement(
        Guid collateralMasterId,
        Guid appraisalId,
        string appraisalNumber,
        Guid requestId,
        string requestNumber,
        Guid propertyId,
        string appraisalType,
        DateTime appraisalDate,
        decimal? appraisedValue,
        string? appraiserUserId,
        Guid? appraisalCompanyId,
        string? appraisalCompanyName,
        string snapshot)
    {
        Id = Guid.CreateVersion7();
        CollateralMasterId = collateralMasterId;
        AppraisalId = appraisalId;
        AppraisalNumber = appraisalNumber;
        RequestId = requestId;
        RequestNumber = requestNumber;
        PropertyId = propertyId;
        AppraisalType = appraisalType;
        AppraisalDate = appraisalDate;
        AppraisedValue = appraisedValue;
        AppraiserUserId = appraiserUserId;
        AppraisalCompanyId = appraisalCompanyId;
        AppraisalCompanyName = appraisalCompanyName;
        Snapshot = snapshot;
        CreatedOn = DateTime.UtcNow;
    }
}
