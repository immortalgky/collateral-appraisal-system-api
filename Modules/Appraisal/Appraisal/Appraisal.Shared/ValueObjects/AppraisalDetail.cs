namespace Appraisal.Appraisal.Shared.ValueObjects;

public class AppraisalDetail : ValueObject
{
    public bool? CanUse { get; }
    public string? Location { get; }
    public string? ConditionUse { get; }
    public string? UsePurpose { get; }
    public string? Part { get; }
    public string? Remark { get; }
    public string? Other { get; }
    public string? AppraiserOpinion { get; }

    private AppraisalDetail() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private AppraisalDetail(
            bool? canUse,
            string? location,
            string? conditionUse,
            string? usePurpose,
            string? part,
            string? remark,
            string? other,
            string? appraiserOpinion
        )
    {
        CanUse = canUse;
        Location = location;
        ConditionUse = conditionUse;
        UsePurpose = usePurpose;
        Part = part;
        Remark = remark;
        Other = other;
        AppraiserOpinion = appraiserOpinion;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static AppraisalDetail Create(
            bool? canUse,
            string? location,
            string? conditionUse,
            string? usePurpose,
            string? part,
            string? remark,
            string? other,
            string? appraiserOpinion
        )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(conditionUse);
        ArgumentException.ThrowIfNullOrWhiteSpace(usePurpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        ArgumentException.ThrowIfNullOrWhiteSpace(remark);
        ArgumentException.ThrowIfNullOrWhiteSpace(other);
        ArgumentException.ThrowIfNullOrWhiteSpace(appraiserOpinion);

        return new AppraisalDetail(
            canUse,
            location,
            conditionUse,
            usePurpose,
            part,
            remark,
            other,
            appraiserOpinion
        );
    }
}