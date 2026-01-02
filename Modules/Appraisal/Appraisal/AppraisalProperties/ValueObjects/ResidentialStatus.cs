namespace Appraisal.AppraisalProperties.ValueObjects;

public class ResidentialStatus : ValueObject
{
    public string? IsResidential { get; }
    public short? BuildingYear { get; }
    public string? DueTo { get; }

    private ResidentialStatus(string? isResidential, short? buildingYear, string? dueTo)
    {
        IsResidential = isResidential;
        BuildingYear = buildingYear;
        DueTo = dueTo;
    }

    public static ResidentialStatus Create(
        string? isResidential,
        short? buildingYear,
        string? dueTo
    )
    {
        return new ResidentialStatus(isResidential, buildingYear, dueTo);
    }
}
