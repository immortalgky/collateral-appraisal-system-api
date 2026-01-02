namespace Appraisal.AppraisalProperties.ValueObjects;

public class Expropriation : ValueObject
{
    public bool? IsExpropriate { get; }
    public string? IsExpropriateRemark { get; }
    public bool? InLineExpropriate { get; }
    public string? InLineExpropriateRemark { get; }
    public string? RoyalDecree { get; }

    private Expropriation(
        bool? isExpropriate,
        string? isExpropriateRemark,
        bool? inLineExpropriate,
        string? inLineExpropriateRemark,
        string? royalDecree
    )
    {
        IsExpropriate = isExpropriate;
        IsExpropriateRemark = isExpropriateRemark;
        InLineExpropriate = inLineExpropriate;
        InLineExpropriateRemark = inLineExpropriateRemark;
        RoyalDecree = royalDecree;
    }

    public static Expropriation Create(
        bool? isExpropriate,
        string? isExpropriateRemark,
        bool? inLineExpropriate,
        string? inLineExpropriateRemark,
        string? royalDecree
    )
    {
        return new Expropriation(
            isExpropriate,
            isExpropriateRemark,
            inLineExpropriate,
            inLineExpropriateRemark,
            royalDecree
        );
    }
}
