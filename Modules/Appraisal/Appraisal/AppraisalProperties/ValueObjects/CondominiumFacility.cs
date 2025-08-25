namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondominiumFacility : ValueObject
{
    public string? CondoFacility { get; }
    public string? CondoFacilityOther { get; }

    private CondominiumFacility(string? condoFacility, string? condoFacilityOther)
    {
        CondoFacility = condoFacility;
        CondoFacilityOther = condoFacilityOther;
    }

    public static CondominiumFacility Create(string? condoFacility, string? condoFacilityOther)
    {
        return new CondominiumFacility(condoFacility, condoFacilityOther);
    }
}
