using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Extensions;
using Appraisal.AppraisalProperties.ValueObjects;

namespace Appraisal.AppraisalProperties.Models;

public class LandAppraisalDetail : Entity<long>
{
    public long ApprId { get; private set; }
    public string? PropertyName { get; private set; }
    public string? CheckOwner { get; private set; }
    public string? Owner { get; private set; }
    public ObligationDetail ObligationDetail { get; private set; } = default!;
    public LandLocationDetail LandLocationDetail { get; private set; } = default!;
    public LandFillDetail LandFillDetail { get; private set; } = default!;
    public LandAccessibilityDetail LandAccessibilityDetail { get; private set; } = default!;
    public string? AnticipationOfProp { get; private set; }
    public LandLimitation LandLimitation { get; private set; } = default!;
    public string? Eviction { get; private set; }
    public string? Allocation { get; private set; }
    public ConsecutiveArea ConsecutiveArea { get; private set; } = default!;
    public LandMiscellaneousDetail LandMiscellaneousDetail { get; private set; } = default!;

    private LandAppraisalDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private LandAppraisalDetail(
        long apprId,
        string propertyName,
        string checkOwner,
        string owner,
        ObligationDetail obligationDetail,
        LandLocationDetail landLocationDetail,
        LandFillDetail landFillDetail,
        LandAccessibilityDetail landAccessibilityDetail,
        string? anticipationOfProp,
        LandLimitation landLimitation,
        string? eviction,
        string? allocation,
        ConsecutiveArea consecutiveArea,
        LandMiscellaneousDetail landMiscellaneousDetail
    )
    {
        ApprId = apprId;
        PropertyName = propertyName;
        CheckOwner = checkOwner;
        Owner = owner;
        ObligationDetail = obligationDetail;
        LandLocationDetail = landLocationDetail;
        LandFillDetail = landFillDetail;
        LandAccessibilityDetail = landAccessibilityDetail;
        AnticipationOfProp = anticipationOfProp;
        LandLimitation = landLimitation;
        Eviction = eviction;
        Allocation = allocation;
        ConsecutiveArea = consecutiveArea;
        LandMiscellaneousDetail = landMiscellaneousDetail;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static LandAppraisalDetail Create(
        long apprId,
        string propertyName,
        string checkOwner,
        string owner,
        ObligationDetail obligationDetail,
        LandLocationDetail landLocationDetail,
        LandFillDetail landFillDetail,
        LandAccessibilityDetail landAccessibilityDetail,
        string? anticipationOfProp,
        LandLimitation landLimitation,
        string? eviction,
        string? allocation,
        ConsecutiveArea consecutiveArea,
        LandMiscellaneousDetail landMiscellaneousDetail
    )
    {
        return new LandAppraisalDetail(
            apprId,
            propertyName,
            checkOwner,
            owner,
            obligationDetail,
            landLocationDetail,
            landFillDetail,
            landAccessibilityDetail,
            anticipationOfProp,
            landLimitation,
            eviction,
            allocation,
            consecutiveArea,
            landMiscellaneousDetail
        );
    }

    public void Update(LandAppraisalDetail model)
    {
        ArgumentNullException.ThrowIfNull(model);

        PropertyName = model.PropertyName;
        CheckOwner = model.CheckOwner;
        Owner = model.Owner;
        AnticipationOfProp = model.AnticipationOfProp;
        Eviction = model.Eviction;
        Allocation = model.Allocation;

        ObligationDetail = model.ObligationDetail;
        LandLocationDetail = model.LandLocationDetail;
        LandFillDetail = model.LandFillDetail;
        LandAccessibilityDetail = model.LandAccessibilityDetail;
        LandLimitation = model.LandLimitation;
        ConsecutiveArea = model.ConsecutiveArea;
        LandMiscellaneousDetail = model.LandMiscellaneousDetail;
    }

    // Overload: Update using DTO (map DTO -> ValueObjects; no entity creation)
    public void Update(LandAppraisalDetailDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        PropertyName = dto.PropertyName;
        CheckOwner = dto.CheckOwner;
        Owner = dto.Owner;
        AnticipationOfProp = dto.AnticipationOfProp;
        Eviction = dto.Eviction;
        Allocation = dto.Allocation;

        ObligationDetail = dto.ObligationDetail.ToEntity();
        LandLocationDetail = dto.LandLocationDetail.ToEntity();
        LandFillDetail = dto.LandFillDetail.ToEntity();
        LandAccessibilityDetail = dto.LandAccessibilityDetail.ToEntity();
        LandLimitation = dto.LandLimitation.ToEntity();
        ConsecutiveArea = dto.ConsecutiveArea.ToEntity();
        LandMiscellaneousDetail = dto.LandMiscellaneousDetail.ToEntity();
    }
}
