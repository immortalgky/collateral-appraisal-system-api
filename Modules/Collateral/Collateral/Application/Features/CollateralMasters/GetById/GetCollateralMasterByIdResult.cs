using System.Text.Json.Serialization;
using Collateral.Application.Features.CollateralMasters.Lookup;

namespace Collateral.Application.Features.CollateralMasters.GetById;

/// <summary>
/// Full master detail response (same detail shape as Lookup, plus underlying master summary for Leasehold).
/// Type-specific detail properties are always serialised — even when null — so the client knows which
/// fields exist for the given collateral type without having to guess at property presence.
/// </summary>
public record GetCollateralMasterByIdResult(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int EngagementCount,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    // Type-specific detail — always serialise (null means "not this type")
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LandDetailDto? LandDetail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] CondoDetailDto? CondoDetail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LeaseholdDetailDto? LeaseholdDetail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] MachineDetailDto? MachineDetail,
    // For Leasehold: inline summary of the underlying master
    UnderlyingMasterSummaryDto? UnderlyingMaster
);

public record UnderlyingMasterSummaryDto(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    // Only meaningful for Land or Condo underlying
    string? Province,
    string? TitleNumber,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue
);
