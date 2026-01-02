namespace Appraisal.AppraisalProperties.ValueObjects;

public class ForestBoundary : ValueObject
{
    public bool? IsForestBoundary { get; }
    public string? IsForestBoundaryRemark { get; }

    private ForestBoundary(bool? isForestBoundary, string? isForestBoundaryRemark)
    {
        IsForestBoundary = isForestBoundary;
        IsForestBoundaryRemark = isForestBoundaryRemark;
    }

    public static ForestBoundary Create(bool? isForestBoundary, string? isForestBoundaryRemark)
    {
        return new ForestBoundary(isForestBoundary, isForestBoundaryRemark);
    }
}
