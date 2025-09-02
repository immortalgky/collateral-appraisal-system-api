namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingCeiling : ValueObject
{
    public string? Ceiling { get; }
    public string? CeilingOther { get; }

    private BuildingCeiling(string? ceiling, string? ceilingOther)
    {
        Ceiling = ceiling;
        CeilingOther = ceilingOther;
    }

    public static BuildingCeiling Create(string? ceiling, string? ceilingOther)
    {
        return new BuildingCeiling(ceiling, ceilingOther);
    }
}
