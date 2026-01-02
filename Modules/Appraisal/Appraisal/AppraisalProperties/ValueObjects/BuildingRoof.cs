namespace Appraisal.AppraisalProperties.ValueObjects;

public class BuildingRoof : ValueObject
{
    public string? Roof { get; }
    public string? RoofOther { get; }

    private BuildingRoof(string? roof, string? roofOther)
    {
        Roof = roof;
        RoofOther = roofOther;
    }

    public static BuildingRoof Create(string? roof, string? roofOther)
    {
        return new BuildingRoof(roof, roofOther);
    }
}
