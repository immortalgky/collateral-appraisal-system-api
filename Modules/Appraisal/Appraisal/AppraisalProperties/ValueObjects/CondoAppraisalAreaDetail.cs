namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondoAppraisalAreaDetail : ValueObject
{
    public string? AreaDesc { get; }
    public decimal? AreaSize { get; }

    private CondoAppraisalAreaDetail(string? areaDesc, decimal? areaSize)
    {
        AreaDesc = areaDesc;
        AreaSize = areaSize;
    }

    public static CondoAppraisalAreaDetail Create(string? areaDesc, decimal? areaSize)
    {
        return new CondoAppraisalAreaDetail(areaDesc, areaSize);
    }
}
