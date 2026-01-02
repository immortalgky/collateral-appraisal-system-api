namespace Appraisal.AppraisalProperties.ValueObjects;

public class Encroachment : ValueObject
{
    public bool? IsEncroached { get; }
    public string? IsEncroachedRemark { get; }
    public decimal? EncroachArea { get; }

    private Encroachment(bool? isEncroached, string? isEncroachedRemark, decimal? encroachArea)
    {
        IsEncroached = isEncroached;
        IsEncroachedRemark = isEncroachedRemark;
        EncroachArea = encroachArea;
    }

    public static Encroachment Create(
        bool? isEncroached,
        string? isEncroachedRemark,
        decimal? encroachArea
    )
    {
        return new Encroachment(isEncroached, isEncroachedRemark, encroachArea);
    }
}
