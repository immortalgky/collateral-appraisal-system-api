namespace Request.Application.Features.Reappraisal.GetCandidateById;

/// <summary>
/// Returns full detail for a single reappraisal candidate plus a list of other
/// nearby Pending candidates within <paramref name="RadiusKm"/> (default 1 km).
/// Maps to GET /api/v1/reappraisal/candidates/{id}.
/// </summary>
public record GetReappraisalCandidateByIdQuery(
    Guid Id,
    /// <summary>
    /// Radius for the "Group Appraisal" nearby candidates list.
    /// TODO(confirm): default radius (currently 1 km). Confirm with business / Parameter module.
    /// </summary>
    decimal RadiusKm = 1m
) : IQuery<GetReappraisalCandidateByIdResult?>;
