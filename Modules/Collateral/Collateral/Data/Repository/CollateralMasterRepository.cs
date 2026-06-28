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
    private static Expression<Func<CollateralMaster, bool>> LandKeyMatches(
        string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber, string? rawang)
        => m =>
            m.LandDetail!.Province == province &&
            m.LandDetail.District == district &&
            m.LandDetail.SubDistrict == subDistrict &&
            m.LandDetail.TitleType == titleType &&
            m.LandDetail.TitleNumber == titleNumber &&
            m.LandDetail.SurveyNumber == surveyNumber &&
            m.LandDetail.LandParcelNumber == landParcelNumber &&
            m.LandDetail.Rawang == rawang;

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
        string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber, string? rawang,
        CancellationToken ct = default)
    {
        // Dedup matches both L (bare land) and LB (land+building) — same physical title.
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType) && m.IsMaster)
            .Where(LandKeyMatches(province, district, subDistrict, titleType, titleNumber, surveyNumber, landParcelNumber, rawang))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindLandByDedupKeyIncludingAliases(
        string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber, string? rawang,
        CancellationToken ct = default)
    {
        // Same as FindLandByDedupKey but includes alias rows (IsMaster=false).
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType))
            .Where(LandKeyMatches(province, district, subDistrict, titleType, titleNumber, surveyNumber, landParcelNumber, rawang))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindByIdWithEngagementsAsync(Guid id, CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);

    public async Task<List<CollateralMaster>> FindAliasesByParentMasterIdAsync(Guid masterId, CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.ParentMasterId == masterId && !m.IsDeleted)
            .ToListAsync(ct);

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
        // Dedup matches LSL, LSB, LS — same physical leasehold registration.
        var leaseholdTypes = new[] { CollateralTypes.Leasehold, CollateralTypes.LeaseholdBuilding, CollateralTypes.LeaseholdWithBuilding };
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
        string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber, string? rawang,
        CancellationToken ct = default)
    {
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Where(m => m.Id != excludeMasterId && !m.IsDeleted && landTypes.Contains(m.CollateralType))
            .Where(LandKeyMatches(province, district, subDistrict, titleType, titleNumber, surveyNumber, landParcelNumber, rawang))
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
        var leaseholdTypes = new[] { CollateralTypes.Leasehold, CollateralTypes.LeaseholdBuilding, CollateralTypes.LeaseholdWithBuilding };
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
        var leaseholdTypes = new[] { CollateralTypes.Leasehold, CollateralTypes.LeaseholdBuilding, CollateralTypes.LeaseholdWithBuilding };
        return await dbContext.CollateralMasters
            .Where(m =>
                !m.IsDeleted &&
                leaseholdTypes.Contains(m.CollateralType) &&
                m.LeaseholdDetail!.UnderlyingMasterId == underlyingMasterId)
            .Select(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<CollateralMaster?> FindProjectMasterByLastAppraisalIdAsync(
        Guid lastAppraisalId,
        CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.ProjectDetail)
            .ThenInclude(d => d!.Units)
            .Include(m => m.Engagements)
            .Where(m =>
                !m.IsDeleted &&
                m.CollateralType == CollateralTypes.Project &&
                m.IsMaster &&
                m.ProjectDetail != null &&
                m.ProjectDetail.AppraisalSummary.LastAppraisalId == lastAppraisalId)
            .FirstOrDefaultAsync(ct);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken);
}
