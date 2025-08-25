namespace Appraisal.MachineAppraisalDetails.ValueObjects;

public class RightsAndConditionsOfLegalRestrictions : ValueObject
{
    public string? Proprietor { get; }
    public string? Owner { get; }
    public string? MachineLocation { get; }
    public string? Obligation { get; }
    public string? Other { get; }

    private RightsAndConditionsOfLegalRestrictions() { }

    private RightsAndConditionsOfLegalRestrictions(
        string proprietor,
        string owner,
        string machineLocation,
        string obligation,
        string other
    )
    {
        Proprietor = proprietor;
        Owner = owner;
        MachineLocation = machineLocation;
        Obligation = obligation;
        Other = other;
    }

    public static RightsAndConditionsOfLegalRestrictions Crate(
        string proprietor,
        string owner,
        string machineLocation,
        string obligation,
        string other
    )
    {
        return new RightsAndConditionsOfLegalRestrictions(
            proprietor,
            owner,
            machineLocation,
            obligation,
            other
        );
    }
}