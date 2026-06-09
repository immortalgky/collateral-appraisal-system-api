using MediatR;

namespace Collateral.Contracts.BlockUnits;

/// <summary>
/// Fetches the collateral master's ProjectUnits for the project whose
/// AppraisalSummary.LastAppraisalId matches <paramref name="PrevAppraisalId"/>.
///
/// Used by the Appraisal module's <c>SeedProjectUnitsFromPriorAsync</c> to seed the new
/// block-reappraisal working copy from the master inventory rather than the prior appraisal's
/// unit rows — because BUM edits the master between appraisals, making it the up-to-date
/// inventory including all sold/unsold statuses.
///
/// Returns null when no matching master is found (first-ever appraisal, no completion yet).
/// </summary>
public record GetProjectMasterUnitsByPrevAppraisalQuery(Guid PrevAppraisalId)
    : IRequest<ProjectMasterUnitsResult?>;

/// <summary>
/// The project type and full unit list from the collateral master.
/// </summary>
public record ProjectMasterUnitsResult(
    string ProjectType,
    IReadOnlyList<ProjectMasterUnitDto> Units
);

/// <summary>
/// Flat projection of a collateral.ProjectUnit row — all fields needed to reconstruct
/// an appraisal.ProjectUnit via the domain factory methods.
/// </summary>
public record ProjectMasterUnitDto(
    int SequenceNumber,
    bool IsSold,
    string? PurchaseBy,
    string? LoanBankName,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo only
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // LandAndBuilding only
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea
);
