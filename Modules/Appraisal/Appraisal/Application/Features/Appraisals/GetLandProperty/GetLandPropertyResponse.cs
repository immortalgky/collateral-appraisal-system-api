namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Response for getting a land property
/// </summary>
public record GetLandPropertyResponse
{
    // Property Info
    public Guid PropertyId { get; init; }
    public Guid AppraisalId { get; init; }
    public int SequenceNumber { get; init; }
    public string PropertyType { get; init; } = null!;
    public string? Description { get; init; }

    // Land Detail Info
    public Guid? LandDetailId { get; init; }
    public string? PropertyName { get; init; }
    public string? LandDesc { get; init; }
    public string? Owner { get; init; }
    public bool VerifiableOwner { get; init; }
    public bool IsObligation { get; init; }
    public string? Obligation { get; init; }

    // Location
    public string? Street { get; init; }
    public string? Soi { get; init; }
    public string? Village { get; init; }
    public string? SubDistrict { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }

    // Coordinates
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    // Other
    public string? Remark { get; init; }
}
