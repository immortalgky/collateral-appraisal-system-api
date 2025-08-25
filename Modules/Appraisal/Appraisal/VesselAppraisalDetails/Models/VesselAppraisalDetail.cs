namespace Appraisal.VesselAppraisalDetails.Models;

public class VesselAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; } = default!;
    public AppraisalDetail AppraisalDetail { get; private set; } = default!;

    private VesselAppraisalDetail() { }

    private VesselAppraisalDetail(
        long apprId,
        AppraisalDetail appraisalDetail
    )
    {
        ApprId = apprId;
        AppraisalDetail = appraisalDetail;
    }
    public static VesselAppraisalDetail Create(
        long apprId,
        AppraisalDetail appraisalDetail
    )
    {
        return new VesselAppraisalDetail(
            apprId,
            appraisalDetail
        );
    }
}