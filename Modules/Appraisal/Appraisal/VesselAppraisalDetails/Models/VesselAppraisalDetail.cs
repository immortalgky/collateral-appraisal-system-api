using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Appraisal.Shared.ValueObjects;
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

    public void Update(VesselAppraisalDetail model)
    {
        ArgumentNullException.ThrowIfNull(model);
        AppraisalDetail = model.AppraisalDetail;
    }

    // Overload: Update using DTO (map DTO -> ValueObjects)
    public void Update(VesselAppraisalDetailDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var vo = AppraisalDetail.Create(
            dto.AppraisalDetail.CanUse,
            dto.AppraisalDetail.Location,
            dto.AppraisalDetail.ConditionUse,
            dto.AppraisalDetail.UsePurpose,
            dto.AppraisalDetail.Part,
            dto.AppraisalDetail.Remark,
            dto.AppraisalDetail.Other,
            dto.AppraisalDetail.AppraiserOpinion
        );
        AppraisalDetail = vo;
    }
}