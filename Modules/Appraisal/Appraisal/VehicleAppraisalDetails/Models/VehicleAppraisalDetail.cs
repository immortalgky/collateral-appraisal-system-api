using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Appraisal.Shared.ValueObjects;
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

    public void Update(VehicleAppraisalDetail model)
    {
        ArgumentNullException.ThrowIfNull(model);
        AppraisalDetail = model.AppraisalDetail;
    }

    // Overload: Update using DTO (map DTO -> ValueObjects)
    public void Update(VehicleAppraisalDetailDto dto)
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