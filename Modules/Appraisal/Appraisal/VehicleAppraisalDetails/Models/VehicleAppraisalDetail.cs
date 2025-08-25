namespace Appraisal.VehicleAppraisalDetails.Models;

public class VehicleAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; } = default!;
    public AppraisalDetail AppraisalDetail { get; private set; } = default!;

    private VehicleAppraisalDetail() { }

    private VehicleAppraisalDetail(
        long apprId,
        AppraisalDetail appraisalDetail
    )
    {
        ApprId = apprId;
        AppraisalDetail = appraisalDetail;
    }

    public static VehicleAppraisalDetail Create(
        long apprId,
        AppraisalDetail appraisalDetail
    )
    {
        return new VehicleAppraisalDetail(
            apprId,
            appraisalDetail
        );
    }
}