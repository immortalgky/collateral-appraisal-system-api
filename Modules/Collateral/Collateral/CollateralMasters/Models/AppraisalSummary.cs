namespace Collateral.CollateralMasters.Models;

public class AppraisalSummary
{
    public Guid? LastAppraisalId { get; private set; }
    public string? LastAppraisalNumber { get; private set; }
    public DateTime? LastAppraisedDate { get; private set; }

    private AppraisalSummary() { }

    public AppraisalSummary(
        Guid? lastAppraisalId,
        string? lastAppraisalNumber,
        DateTime? lastAppraisedDate)
    {
        LastAppraisalId = lastAppraisalId;
        LastAppraisalNumber = lastAppraisalNumber;
        LastAppraisedDate = lastAppraisedDate;
    }

    public void Update(
        Guid? lastAppraisalId,
        string? lastAppraisalNumber,
        DateTime? lastAppraisedDate)
    {
        LastAppraisalId = lastAppraisalId;
        LastAppraisalNumber = lastAppraisalNumber;
        LastAppraisedDate = lastAppraisedDate;
    }
}
