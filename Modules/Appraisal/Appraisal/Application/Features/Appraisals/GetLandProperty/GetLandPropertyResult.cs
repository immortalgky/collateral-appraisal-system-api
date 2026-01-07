namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Result of getting a land property
/// </summary>
public record GetLandPropertyResult
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
    public string? LandDescription { get; init; }
    public string? OwnerName { get; init; }
    public bool IsOwnerVerified { get; init; }
    public bool HasObligation { get; init; }
    public string? ObligationDetails { get; init; }

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
