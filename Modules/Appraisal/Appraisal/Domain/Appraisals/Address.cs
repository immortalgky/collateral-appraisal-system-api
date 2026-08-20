namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing Thai administrative address divisions (Tambon / Amphoe / Changwat).
/// LandOffice is not part of this VO — it lives as a scalar on the owning entity.
/// </summary>
public record Address
{
    public string? SubDistrict { get; init; } // Tambon
    public string? District { get; init; } // Amphoe
    public string? Province { get; init; } // Changwat

    private Address()
    {
    }

    public static Address Create(
        string? subDistrict,
        string? district,
        string? province)
    {
        return new Address
        {
            SubDistrict = subDistrict,
            District = district,
            Province = province
        };
    }

    public string? FullAddress => string.Join(", ",
        new[] { SubDistrict, District, Province }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}