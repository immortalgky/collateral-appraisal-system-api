namespace Appraisal.Domain.Appraisals;

public class AppraisalDecision : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Opinion & Condition fields (Parameter Code stored in *Type)
    public bool? IsPriceVerified { get; private set; }
    public string? ConditionType { get; private set; }
    public string? Condition { get; private set; }
    public string? RemarkType { get; private set; }
    public string? Remark { get; private set; }
    public string? ExternalAppraiserOpinionType { get; private set; }
    public string? ExternalAppraiserOpinion { get; private set; }
    public string? CommitteeOpinionType { get; private set; }
    public string? CommitteeOpinion { get; private set; }
    public string? InternalAppraiserOpinionType { get; private set; }
    public string? InternalAppraiserOpinion { get; private set; }

    // Review values
    public decimal? TotalAppraisalPriceReview { get; private set; }
    public string? AdditionalAssumptions { get; private set; }

    // Construction summary "เอกสารประกอบ" checkbox overrides (สรุปรายงานการตรวจงานก่อสร้าง report).
    // null = fall back to the auto-derived value (a matching document is attached); true/false = explicit
    // override, used mainly when an external company bundles the documents into D001 (the appraisal book)
    // so nothing can be auto-detected and the book-verification reviewer ticks manually.
    public bool? HasConstructionLicenseDoc { get; private set; }
    public bool? HasConstructionProgressTableDoc { get; private set; }
    public bool? HasConstructionPhotoDoc { get; private set; }

    private AppraisalDecision()
    {
    }

    public static AppraisalDecision Create(Guid appraisalId)
    {
        return new AppraisalDecision
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public void Update(
        bool? isPriceVerified,
        string? conditionType,
        string? condition,
        string? remarkType,
        string? remark,
        string? externalAppraiserOpinionType,
        string? externalAppraiserOpinion,
        string? committeeOpinionType,
        string? committeeOpinion,
        string? internalAppraiserOpinionType,
        string? internalAppraiserOpinion,
        string? additionalAssumptions,
        bool? hasConstructionLicenseDoc,
        bool? hasConstructionProgressTableDoc,
        bool? hasConstructionPhotoDoc)
    {
        IsPriceVerified = isPriceVerified;
        ConditionType = conditionType;
        Condition = condition;
        RemarkType = remarkType;
        Remark = remark;
        ExternalAppraiserOpinionType = externalAppraiserOpinionType;
        ExternalAppraiserOpinion = externalAppraiserOpinion;
        CommitteeOpinionType = committeeOpinionType;
        CommitteeOpinion = committeeOpinion;
        InternalAppraiserOpinionType = internalAppraiserOpinionType;
        InternalAppraiserOpinion = internalAppraiserOpinion;
        AdditionalAssumptions = additionalAssumptions;
        HasConstructionLicenseDoc = hasConstructionLicenseDoc;
        HasConstructionProgressTableDoc = hasConstructionProgressTableDoc;
        HasConstructionPhotoDoc = hasConstructionPhotoDoc;
    }
}
