namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing Thai administrative address divisions.
/// </summary>
public record AdministrativeAddress
{
    public string? SubDistrict { get; init; } // Tambon
    public string? District { get; init; } // Amphoe
    public string? Province { get; init; } // Changwat
    public string? LandOffice { get; init; } // Related land office

    private AdministrativeAddress()
    {
    }

    public static AdministrativeAddress Create(
        string? subDistrict,
        string? district,
        string? province,
        string? landOffice = null)
    {
        return new AdministrativeAddress
        {
            SubDistrict = subDistrict,
            District = district,
            Province = province,
            LandOffice = landOffice
        };
    }

    public string? FullAddress => string.Join(", ",
        new[] { SubDistrict, District, Province }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}