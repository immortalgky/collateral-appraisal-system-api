namespace Collateral.Application.Features.Reappraisal.GetCandidateById;

/// <summary>
/// Returns full detail for a single reappraisal candidate plus a list of other
/// nearby Pending candidates within <paramref name="RadiusKm"/> (default 1 km).
/// Maps to GET /reappraisal/candidates/{id}.
/// </summary>
public record GetReappraisalCandidateByIdQuery(
    Guid Id,
    decimal RadiusKm = 1m
) : IQuery<GetReappraisalCandidateByIdResult?>;
