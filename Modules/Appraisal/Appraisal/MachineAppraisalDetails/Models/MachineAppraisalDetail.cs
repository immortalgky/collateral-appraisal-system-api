namespace Appraisal.MachineAppraisalDetails.Models;

public class MachineAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; } = default!;
    public AppraisalDetail AppraisalDetail { get; private set; } = default!;

    private MachineAppraisalDetail() { }

    private MachineAppraisalDetail(
        long apprId,
        AppraisalDetail appraisalDetail

    )
    {
        ApprId = apprId;
        AppraisalDetail = appraisalDetail;
    }

    public static MachineAppraisalDetail Create(
        long apprId,
        AppraisalDetail appraisalDetail
    )
    {
        return new MachineAppraisalDetail(
            apprId,
            appraisalDetail
        );
    }
}