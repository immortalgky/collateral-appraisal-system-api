using Collateral.CollateralMasters.Models;

namespace Collateral.Data.Repository;

public interface ICollateralMasterRepository
{
    void Add(CollateralMaster master);
    Task<CollateralMaster?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a master by id regardless of IsDeleted. Used by Restore to locate deleted masters.
    /// </summary>
    Task<CollateralMaster?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    // --- Type-aware dedup lookups (tracked entities for upsert) ---

    Task<CollateralMaster?> FindLandByDedupKey(
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Like FindLandByDedupKey but returns ANY matching row — master or alias.
    /// The caller resolves to the master via ParentMasterId when IsMaster=false.
    /// Used by the multi-title upsert algorithm to detect which group a title belongs to.
    /// </summary>
    Task<CollateralMaster?> FindLandByDedupKeyIncludingAliases(
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a CollateralMaster by Id (tracked) without loading detail navigation properties.
    /// Lightweight resolution used when navigating ParentMasterId to find the IsMaster row.
    /// Includes Engagements for the upsert path.
    /// </summary>
    Task<CollateralMaster?> FindByIdWithEngagementsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all alias rows whose ParentMasterId equals masterId.
    /// Used to enumerate known titles in a group when building lookup results and deciding
    /// which aliases still need to be created during reappraisal.
    /// </summary>
    Task<List<CollateralMaster>> FindAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default);

    Task<CollateralMaster?> FindCondoByDedupKey(
        string landOfficeCode, string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string titleNumber, string titleType,
        CancellationToken ct = default);

    Task<CollateralMaster?> FindLeaseholdByDedupKey(
        string leaseRegistrationNo, Guid underlyingMasterId,
        string lessor, string lessee, DateOnly leaseTermStart,
        CancellationToken ct = default);

    /// <summary>
    /// Tier-1: lookup by MachineRegistrationNo if provided.
    /// Tier-2: lookup by (SerialNo, Brand, Model, Manufacturer) when tier-1 misses.
    /// Returns the existing tracked master (may require promotion at call site).
    /// </summary>
    Task<CollateralMaster?> FindMachineForUpsert(
        string? registrationNo, string? serialNo, string? brand, string? model, string? manufacturer,
        CancellationToken ct = default);

    // --- Admin: dedup-collision checks (excludes the given masterId from the match) ---

    Task<bool> LandDedupCollidesAsync(
        Guid excludeMasterId,
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default);

    Task<bool> CondoDedupCollidesAsync(
        Guid excludeMasterId,
        string landOfficeCode, string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string titleNumber, string titleType,
        CancellationToken ct = default);

    Task<bool> LeaseholdDedupCollidesAsync(
        Guid excludeMasterId,
        string leaseRegistrationNo, Guid underlyingMasterId,
        string lessor, string lessee, DateOnly leaseTermStart,
        CancellationToken ct = default);

    Task<bool> MachineDedupCollidesAsync(
        Guid excludeMasterId,
        string? machineRegistrationNo, string? serialNo, string? brand, string? model, string? manufacturer,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted Leasehold masters whose UnderlyingMasterId equals the given masterId.
    /// Used to enforce the RESTRICT constraint before soft-delete.
    /// </summary>
    Task<List<Guid>> GetActiveLeaseholdIdsForUnderlyingAsync(Guid underlyingMasterId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
