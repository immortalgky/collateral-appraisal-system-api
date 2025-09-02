namespace Appraisal.AppraisalProperties.ValueObjects;

public class UtilizationDetail : ValueObject
{
    public string? Utilization { get; }
    public string? UseForOtherPurpose { get; }

    private UtilizationDetail(string? utilization, string? useForOtherPurpose)
    {
        Utilization = utilization;
        UseForOtherPurpose = useForOtherPurpose;
    }

    public static UtilizationDetail Create(string? utilization, string? useForOtherPurpose)
    {
        return new UtilizationDetail(utilization, useForOtherPurpose);
    }
}
