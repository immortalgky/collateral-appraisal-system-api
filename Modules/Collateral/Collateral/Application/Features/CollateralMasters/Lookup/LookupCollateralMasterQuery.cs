namespace Collateral.Application.Features.CollateralMasters.Lookup;

/// <summary>
/// Lookup a CollateralMaster by type-specific dedup key.
/// Returns the master + appropriate detail + last engagement summary + prior company IDs (for appeal exclusion).
/// Returns null (404) when no match is found.
/// </summary>
public record LookupCollateralMasterQuery(
    string Type,

    // Land dedup params
    string? Province,
    string? District,
    string? SubDistrict,
    string? TitleNumber,

    // Condo dedup params (with Province/District/SubDistrict, shared with Land above)
    string? CondoRegistrationNumber,
    string? Building,
    string? Floor,
    string? Unit,

    // Leasehold dedup params
    string? ContractNo,
    Guid? UnderlyingMasterId,
    string? Lessor,
    string? Lessee,
    DateOnly? LeaseTermStart,

    // Machine dedup params (tier-1)
    string? MachineRegistrationNo,
    // Machine dedup params (tier-2 fallback)
    string? SerialNo,
    string? Brand,
    string? Model,
    string? Manufacturer
) : IQuery<LookupCollateralMasterResult>;
