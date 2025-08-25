namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondoRoof : ValueObject
{
    public string? Roof { get; }
    public string? RoofOther { get; }

    private CondoRoof(string? roof, string? roofOther)
    {
        Roof = roof;
        RoofOther = roofOther;
    }

    public static CondoRoof Create(string? roof, string? roofOther)
    {
        return new CondoRoof(roof, roofOther);
    }
}
