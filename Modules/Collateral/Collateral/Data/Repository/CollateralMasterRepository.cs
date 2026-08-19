using System.Linq.Expressions;
using Collateral.CollateralMasters.Models;
using Collateral.Contracts;

namespace Collateral.Data.Repository;

public class CollateralMasterRepository(CollateralDbContext dbContext) : ICollateralMasterRepository
{
    // Single source of truth for the Land/LB dedup-key predicate. MUST stay in sync with
    // UX_LandDetails_DedupKey_Active (LandDetailConfiguration) and the in-memory BuildTitleKey
    // (CollateralMasterUpsertService). LandOfficeCode is NOT part of the key. Nullable
    // survey/parcel/rawang use EF null-semantics: a null param matches NULL rows (IS NULL).
    /// <summary>
    /// The land dedup key: administrative location plus title number.
    ///
    /// Narrowed from eight columns to four on 2026-08-09 at the business owner's instruction.
    /// TitleType, SurveyNumber, LandParcelNumber and Rawang are no longer part of it — they were
    /// splitting one physical parcel across several masters whenever an appraiser recorded them
    /// differently (Rawang in particular is blank on ~99.8% of title rows, so filling it in on a later
    /// appraisal minted a second master for land that already had one).
    ///
    /// Consequence to be aware of: Thai title numbering runs per document type, so a โฉนด and a นส.3ก
    /// bearing the same number in the same sub-district now resolve to ONE master. That trade-off was
    /// raised and accepted.
    ///
    /// Mirrored in memory by <c>CollateralMasterUpsertService.BuildTitleKey</c> and in the database by
    /// <c>UX_LandDetails_DedupKey_Active</c> — all three must change together.
    /// </summary>
    private static Expression<Func<CollateralMaster, bool>> LandKeyMatches(
        string province, string district, string subDistrict, string titleNumber)
        => m =>
            m.LandDetail!.Province == province &&
            m.LandDetail.District == district &&
            m.LandDetail.SubDistrict == subDistrict &&
            m.LandDetail.TitleNumber == titleNumber;

    // Single source of truth for the Condo dedup-key predicate. MUST stay in sync with
    // UX_CondoDetails_DedupKey_Active (CondoDetailConfiguration). LandOfficeCode is NOT part of
    // the key; Province/District/SubDistrict are the required geographic disambiguator.
    private static Expression<Func<CollateralMaster, bool>> CondoKeyMatches(
        string condoRegistrationNumber, string buildingNumber, string floorNumber, string roomNumber,
        string province, string district, string subDistrict)
        => m =>
            m.CondoDetail!.CondoRegistrationNumber == condoRegistrationNumber &&
            m.CondoDetail.BuildingNumber == buildingNumber &&
            m.CondoDetail.FloorNumber == floorNumber &&
            m.CondoDetail.RoomNumber == roomNumber &&
            m.CondoDetail.Province == province &&
            m.CondoDetail.District == district &&
            m.CondoDetail.SubDistrict == subDistrict;

    public void Add(CollateralMaster master) => dbContext.CollateralMasters.Add(master);

    public async Task<CollateralMaster?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.CollateralMasters
            .AsSplitQuery()
            .Include(m => m.LandDetail)
            .Include(m => m.CondoDetail)
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .Include(m => m.Documents)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken);

    public async Task<CollateralMaster?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.CollateralMasters
            .AsSplitQuery()
            .Include(m => m.LandDetail)
            .Include(m => m.CondoDetail)
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.MachineDetail)
            .Include(m => m.Engagements)
            .Include(m => m.Documents)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<CollateralMaster?> FindLandByDedupKey(
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default)
    {
        // Dedup matches both L (bare land) and LB (land+building) — same physical title.
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType) && m.IsMaster)
            .Where(LandKeyMatches(province, district, subDistrict, titleNumber))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindLandByDedupKeyIncludingAliases(
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default)
    {
        // Same as FindLandByDedupKey but includes alias rows (IsMaster=false).
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType))
            .Where(LandKeyMatches(province, district, subDistrict, titleNumber))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindByIdWithEngagementsAsync(Guid id, CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);

    /// <summary>
    /// Finds the master an appraisal is bound to, via that appraisal's engagement
    /// (<c>CollateralEngagements</c> is UNIQUE on <c>AppraisalId</c>, so at most one row).
    ///
    /// Used as the <i>fallback</i> when a collateral's dedup key finds nothing — see
    /// <c>CollateralMasterUpsertService.FindMasterViaPreviousAppraisalAsync</c>.
    /// </summary>
    public async Task<CollateralMaster?> FindMasterByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var masterId = await dbContext.CollateralEngagements
            .AsNoTracking()
            .Where(e => e.AppraisalId == appraisalId)
            .Select(e => (Guid?)e.CollateralMasterId)
            .FirstOrDefaultAsync(ct);

        if (masterId is null)
            return null;

        // Must Include EVERY type detail, not just LandDetail as FindByIdWithEngagementsAsync does:
        // the callers include the condo / machine / leasehold paths, and UpsertFrom*Appraisal throws
        // immediately when its type navigation is null (lazy loading is not enabled).
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.CondoDetail)
            .Include(m => m.MachineDetail)
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.ProjectDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.Id == masterId.Value && !m.IsDeleted, ct);
    }

    public async Task<List<CollateralMaster>> FindAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.ParentMasterId == masterId && !m.IsDeleted)
            .ToListAsync(ct);

    // No IsDeleted filter and every detail Included — soft-delete/restore must reach rows that are
    // already in the target state (idempotency) and must flip each alias's own detail row, which is
    // what releases that alias from its dedup-key filtered index.
    public async Task<List<CollateralMaster>> FindAllAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.CondoDetail)
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.MachineDetail)
            .Include(m => m.ProjectDetail)
            .Where(m => m.ParentMasterId == masterId)
            .ToListAsync(ct);

    // Deliberately Include-free — see the interface. Tracked, because the caller mutates the rows.
    public async Task<List<CollateralMaster>> FindAliasesByParentMasterIdsAsync(
        IReadOnlyCollection<Guid> parentMasterIds, CancellationToken ct = default)
    {
        if (parentMasterIds.Count == 0) return [];

        var result = new List<CollateralMaster>();

        // Chunked for the same reason as HostCollateralLinkIngestor.LoadEngagementsAsync: a full
        // AS400 dump would otherwise build an IN clause past SQL Server's parameter limit.
        foreach (var chunk in parentMasterIds.Distinct().Chunk(1000))
        {
            var rows = await dbContext.CollateralMasters
                .Where(m => m.ParentMasterId != null && chunk.Contains(m.ParentMasterId.Value))
                .ToListAsync(ct);

            result.AddRange(rows);
        }

        return result;
    }

    public async Task<CollateralMaster?> FindCondoByDedupKey(
        string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string province, string district, string subDistrict,
        CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && m.CollateralType == CollateralTypes.Condo) // "U"
            .Where(CondoKeyMatches(condoRegistrationNumber, buildingNumber, floorNumber, roomNumber, province, district, subDistrict))
            .FirstOrDefaultAsync(ct);

    public async Task<CollateralMaster?> FindLeaseholdByDedupKey(
        string leaseRegistrationNo, Guid underlyingMasterId,
        string lessor, string lessee, DateOnly leaseTermStart,
        CancellationToken ct = default)
    {
        // Dedup matches the whole leasehold family — same physical leasehold registration.
        var leaseholdTypes = CollateralTypes.LeaseholdFamily;
        return await dbContext.CollateralMasters
            .Include(m => m.LeaseholdDetail)
            .Include(m => m.Engagements)
            .Where(m =>
                !m.IsDeleted &&
                leaseholdTypes.Contains(m.CollateralType) &&
                m.LeaseholdDetail!.LeaseRegistrationNo == leaseRegistrationNo &&
                m.LeaseholdDetail.UnderlyingMasterId == underlyingMasterId &&
                m.LeaseholdDetail.Lessor == lessor &&
                m.LeaseholdDetail.Lessee == lessee &&
                m.LeaseholdDetail.LeaseTermStart == leaseTermStart)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindMachineForUpsert(
        string? registrationNo, string? serialNo, string? brand, string? model, string? manufacturer,
        CancellationToken ct = default)
    {
        // Tier-1: registration number lookup (exact, non-null)
        if (!string.IsNullOrWhiteSpace(registrationNo))
        {
            var byReg = await dbContext.CollateralMasters
                .Include(m => m.MachineDetail)
                .Include(m => m.Engagements)
                .Where(m =>
                    !m.IsDeleted &&
                    m.CollateralType == CollateralTypes.Machine && // "MAC"
                    m.MachineDetail!.MachineRegistrationNo == registrationNo)
                .FirstOrDefaultAsync(ct);

            if (byReg is not null)
                return byReg;

            // Promotion: check if a composite-keyed master exists for the same machine
            if (!string.IsNullOrWhiteSpace(serialNo) &&
                !string.IsNullOrWhiteSpace(brand) &&
                !string.IsNullOrWhiteSpace(model) &&
                !string.IsNullOrWhiteSpace(manufacturer))
            {
                return await dbContext.CollateralMasters
                    .Include(m => m.MachineDetail)
                    .Include(m => m.Engagements)
                    .Where(m =>
                        !m.IsDeleted &&
                        m.CollateralType == CollateralTypes.Machine &&
                        m.MachineDetail!.MachineRegistrationNo == null &&
                        m.MachineDetail.SerialNo == serialNo &&
                        m.MachineDetail.Brand == brand &&
                        m.MachineDetail.Model == model &&
                        m.MachineDetail.Manufacturer == manufacturer)
                    .FirstOrDefaultAsync(ct);
            }

            return null;
        }

        // Tier-2: composite key lookup
        if (!string.IsNullOrWhiteSpace(serialNo) &&
            !string.IsNullOrWhiteSpace(brand) &&
            !string.IsNullOrWhiteSpace(model) &&
            !string.IsNullOrWhiteSpace(manufacturer))
        {
            return await dbContext.CollateralMasters
                .Include(m => m.MachineDetail)
                .Include(m => m.Engagements)
                .Where(m =>
                    !m.IsDeleted &&
                    m.CollateralType == CollateralTypes.Machine &&
                    m.MachineDetail!.MachineRegistrationNo == null &&
                    m.MachineDetail.SerialNo == serialNo &&
                    m.MachineDetail.Brand == brand &&
                    m.MachineDetail.Model == model &&
                    m.MachineDetail.Manufacturer == manufacturer)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    public async Task<bool> LandDedupCollidesAsync(
        Guid excludeMasterId,
        string province, string district, string subDistrict, string titleNumber,
        CancellationToken ct = default)
    {
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Where(m => m.Id != excludeMasterId && !m.IsDeleted && landTypes.Contains(m.CollateralType))
            .Where(LandKeyMatches(province, district, subDistrict, titleNumber))
            .AnyAsync(ct);
    }

    public async Task<bool> CondoDedupCollidesAsync(
        Guid excludeMasterId,
        string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string province, string district, string subDistrict,
        CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Where(m => m.Id != excludeMasterId && !m.IsDeleted && m.CollateralType == CollateralTypes.Condo)
            .Where(CondoKeyMatches(condoRegistrationNumber, buildingNumber, floorNumber, roomNumber, province, district, subDistrict))
            .AnyAsync(ct);

    public async Task<bool> LeaseholdDedupCollidesAsync(
        Guid excludeMasterId,
        string leaseRegistrationNo, Guid underlyingMasterId,
        string lessor, string lessee, DateOnly leaseTermStart,
        CancellationToken ct = default)
    {
        var leaseholdTypes = CollateralTypes.LeaseholdFamily;
        return await dbContext.CollateralMasters
            .Where(m =>
                m.Id != excludeMasterId &&
                !m.IsDeleted &&
                leaseholdTypes.Contains(m.CollateralType) &&
                m.LeaseholdDetail!.LeaseRegistrationNo == leaseRegistrationNo &&
                m.LeaseholdDetail.UnderlyingMasterId == underlyingMasterId &&
                m.LeaseholdDetail.Lessor == lessor &&
                m.LeaseholdDetail.Lessee == lessee &&
                m.LeaseholdDetail.LeaseTermStart == leaseTermStart)
            .AnyAsync(ct);
    }

    public async Task<bool> MachineDedupCollidesAsync(
        Guid excludeMasterId,
        string? machineRegistrationNo, string? serialNo, string? brand, string? model, string? manufacturer,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(machineRegistrationNo))
        {
            return await dbContext.CollateralMasters
                .Where(m =>
                    m.Id != excludeMasterId &&
                    !m.IsDeleted &&
                    m.CollateralType == CollateralTypes.Machine &&
                    m.MachineDetail!.MachineRegistrationNo == machineRegistrationNo)
                .AnyAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(serialNo) && !string.IsNullOrWhiteSpace(brand) &&
            !string.IsNullOrWhiteSpace(model) && !string.IsNullOrWhiteSpace(manufacturer))
        {
            return await dbContext.CollateralMasters
                .Where(m =>
                    m.Id != excludeMasterId &&
                    !m.IsDeleted &&
                    m.CollateralType == CollateralTypes.Machine &&
                    m.MachineDetail!.MachineRegistrationNo == null &&
                    m.MachineDetail.SerialNo == serialNo &&
                    m.MachineDetail.Brand == brand &&
                    m.MachineDetail.Model == model &&
                    m.MachineDetail.Manufacturer == manufacturer)
                .AnyAsync(ct);
        }

        return false;
    }

    public async Task<List<Guid>> GetActiveLeaseholdIdsForUnderlyingAsync(
        Guid underlyingMasterId,
        CancellationToken ct = default)
    {
        var leaseholdTypes = CollateralTypes.LeaseholdFamily;
        return await dbContext.CollateralMasters
            .Where(m =>
                !m.IsDeleted &&
                leaseholdTypes.Contains(m.CollateralType) &&
                m.LeaseholdDetail!.UnderlyingMasterId == underlyingMasterId)
            .Select(m => m.Id)
            .ToListAsync(ct);
    }

    // Resolved through the engagement, not ProjectDetail.AppraisalSummary.LastAppraisalId.
    // CollateralEngagements is UNIQUE on AppraisalId (UX_CollateralEngagements_Appraisal), so this is
    // an exact indexed hit; ProjectDetails has no index at all, and its LastAppraisalId is a
    // latest-WRITE-wins cache that an out-of-order replay can leave pointing at the wrong appraisal.
    public async Task<CollateralMaster?> FindProjectMasterByAppraisalIdAsync(
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var masterId = await dbContext.CollateralEngagements
            .AsNoTracking()
            .Where(e => e.AppraisalId == appraisalId)
            .Select(e => (Guid?)e.CollateralMasterId)
            .FirstOrDefaultAsync(ct);

        if (masterId is null)
            return null;

        return await dbContext.CollateralMasters
            .Include(m => m.ProjectDetail)
            .ThenInclude(d => d!.Units)
            .Include(m => m.Engagements)
            .Where(m =>
                m.Id == masterId.Value &&
                !m.IsDeleted &&
                m.CollateralType == CollateralTypes.Project &&
                m.IsMaster &&
                m.ProjectDetail != null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken);
}
