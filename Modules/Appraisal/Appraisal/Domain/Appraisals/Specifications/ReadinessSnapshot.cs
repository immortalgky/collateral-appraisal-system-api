namespace Appraisal.Domain.Appraisals.Specifications;

/// <summary>
/// Lightweight, infrastructure-agnostic projection of the data the four pricing-analysis
/// preconditions need. Both write and read paths build the same snapshot (write path via
/// the EF aggregate, read path via Dapper) so the domain rules can be reused without
/// caring how the data was loaded.
/// </summary>
public sealed record ReadinessSnapshot(
    Guid AppraisalId,
    Guid GroupId,
    int MarketSurveyCount,
    IReadOnlyList<PropertySnapshot> Properties);

/// <summary>
/// Per-property projection used by the rules. Booleans pre-compute the existence checks
/// that would otherwise require navigation properties or extra joins.
/// </summary>
public sealed record PropertySnapshot(
    Guid PropertyId,
    string PropertyType,
    bool HasBuildingDetail,
    bool HasRentalInfo,
    bool HasRentalSchedule,
    string Status);
