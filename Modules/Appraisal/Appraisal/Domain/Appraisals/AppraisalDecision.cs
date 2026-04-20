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
    public string? AppraiserOpinionType { get; private set; }
    public string? AppraiserOpinion { get; private set; }
    public string? CommitteeOpinionType { get; private set; }
    public string? CommitteeOpinion { get; private set; }

    // Review values
    public decimal? TotalAppraisalPriceReview { get; private set; }
    public string? AdditionalAssumptions { get; private set; }

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
        string? appraiserOpinionType,
        string? appraiserOpinion,
        string? committeeOpinionType,
        string? committeeOpinion,
        string? additionalAssumptions)
    {
        IsPriceVerified = isPriceVerified;
        ConditionType = conditionType;
        Condition = condition;
        RemarkType = remarkType;
        Remark = remark;
        AppraiserOpinionType = appraiserOpinionType;
        AppraiserOpinion = appraiserOpinion;
        CommitteeOpinionType = committeeOpinionType;
        CommitteeOpinion = committeeOpinion;
        AdditionalAssumptions = additionalAssumptions;
    }
}
