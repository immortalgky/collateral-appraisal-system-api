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

    /// <summary>
    /// Finds the land master for a title. The key is administrative location plus title number —
    /// four columns, narrowed from eight on 2026-08-09. See <c>CollateralMasterRepository.LandKeyMatches</c>
    /// for the reasoning and the accepted trade-off.
    /// </summary>
    Task<CollateralMaster?> FindLandByDedupKey(
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Like FindLandByDedupKey but returns ANY matching row — master or alias.
    /// The caller resolves to the master via ParentMasterId when IsMaster=false.
    /// Used by the multi-title upsert algorithm to detect which group a title belongs to.
    /// </summary>
    Task<CollateralMaster?> FindLandByDedupKeyIncludingAliases(
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a CollateralMaster by Id (tracked) without loading detail navigation properties.
    /// Lightweight resolution used when navigating ParentMasterId to find the IsMaster row.
    /// Includes Engagements for the upsert path.
    /// </summary>
    Task<CollateralMaster?> FindByIdWithEngagementsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Finds the master an appraisal is bound to, via that appraisal's engagement.
    /// Used as the fallback when the collateral's dedup key finds nothing.
    /// </summary>
    Task<CollateralMaster?> FindMasterByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);

    /// <summary>
    /// Returns all alias rows whose ParentMasterId equals masterId.
    /// Used to enumerate known titles in a group when building lookup results and deciding
    /// which aliases still need to be created during reappraisal.
    /// </summary>
    Task<List<CollateralMaster>> FindAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default);

    /// <summary>
    /// Returns every alias row of <paramref name="masterId"/> regardless of its IsDeleted state, with
    /// ALL type details loaded.
    ///
    /// Differs from <see cref="FindAliasesByParentMasterIdAsync"/> on both counts, and needs to:
    /// soft-delete/restore has to reach the already-flipped rows to be idempotent, and it must reach
    /// a condo / leasehold / machine alias's detail row to clear its dedup-key filtered index
    /// (that method Includes only LandDetail, so those details would stay behind).
    /// </summary>
    Task<List<CollateralMaster>> FindAllAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default);

    /// <summary>
    /// Batched sibling of <see cref="FindAllAliasesByParentMasterIdAsync"/> that loads the alias rows
    /// alone, with no detail navigations.
    ///
    /// For the nightly AS400 feed, which may touch thousands of masters in one file and only writes
    /// the two redemption flags on the alias row itself. Pulling five Includes per master to set two
    /// scalars would dominate the run. Callers that need to reach an alias's DETAIL row — soft-delete
    /// and restore, which must clear its dedup-key filtered index — must keep using the singular
    /// method.
    /// </summary>
    Task<List<CollateralMaster>> FindAliasesByParentMasterIdsAsync(
        IReadOnlyCollection<Guid> parentMasterIds, CancellationToken ct = default);

    Task<CollateralMaster?> FindCondoByDedupKey(
        string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string province, string district, string subDistrict,
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
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default);

    Task<bool> CondoDedupCollidesAsync(
        Guid excludeMasterId,
        string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string province, string district, string subDistrict,
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

    /// <summary>
    /// Finds the PRJ, IsMaster, non-deleted master that the given appraisal is bound to, via that
    /// appraisal's engagement. Used by the project-branch upsert to detect a previously-created
    /// master for the same appraisal lineage (reappraisal dedup). Includes ProjectDetail and Engagements.
    /// </summary>
    Task<CollateralMaster?> FindProjectMasterByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
