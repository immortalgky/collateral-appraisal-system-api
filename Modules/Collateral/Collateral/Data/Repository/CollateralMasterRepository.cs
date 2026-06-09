using Collateral.CollateralMasters.Models;

namespace Collateral.Data.Repository;

public class CollateralMasterRepository(CollateralDbContext dbContext) : ICollateralMasterRepository
{
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
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default)
    {
        // null is a valid dedup component for survey/parcel — must match exactly
        // Dedup matches both L (bare land) and LB (land+building) — same physical title.
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType) && m.IsMaster &&
                m.LandDetail!.LandOfficeCode == landOfficeCode &&
                m.LandDetail.Province == province &&
                m.LandDetail.District == district &&
                m.LandDetail.SubDistrict == subDistrict &&
                m.LandDetail.TitleType == titleType &&
                m.LandDetail.TitleNumber == titleNumber &&
                m.LandDetail.SurveyNumber == surveyNumber &&
                m.LandDetail.LandParcelNumber == landParcelNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CollateralMaster?> FindLandByDedupKeyIncludingAliases(
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default)
    {
        // Same as FindLandByDedupKey but includes alias rows (IsMaster=false).
        // Matches both L and LB — same physical title.
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .Where(m => !m.IsDeleted && landTypes.Contains(m.CollateralType) &&
                m.LandDetail!.LandOfficeCode == landOfficeCode &&
                m.LandDetail.Province == province &&
                m.LandDetail.District == district &&
                m.LandDetail.SubDistrict == subDistrict &&
                m.LandDetail.TitleType == titleType &&
                m.LandDetail.TitleNumber == titleNumber &&
                m.LandDetail.SurveyNumber == surveyNumber &&
                m.LandDetail.LandParcelNumber == landParcelNumber)
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
        string landOfficeCode, string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string titleNumber, string titleType,
        CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Include(m => m.CondoDetail)
            .Include(m => m.Engagements)
            .Where(m =>
                !m.IsDeleted &&
                m.CollateralType == CollateralTypes.Condo && // "U"
                m.CondoDetail!.LandOfficeCode == landOfficeCode &&
                m.CondoDetail.CondoRegistrationNumber == condoRegistrationNumber &&
                m.CondoDetail.BuildingNumber == buildingNumber &&
                m.CondoDetail.FloorNumber == floorNumber &&
                m.CondoDetail.RoomNumber == roomNumber &&
                m.CondoDetail.TitleNumber == titleNumber &&
                m.CondoDetail.TitleType == titleType)
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
        string landOfficeCode, string province, string district, string subDistrict,
        string titleType, string titleNumber, string? surveyNumber, string? landParcelNumber,
        CancellationToken ct = default)
    {
        var landTypes = new[] { CollateralTypes.Land, CollateralTypes.LandWithBuilding };
        return await dbContext.CollateralMasters
            .Where(m =>
                m.Id != excludeMasterId &&
                !m.IsDeleted &&
                landTypes.Contains(m.CollateralType) &&
                m.LandDetail!.LandOfficeCode == landOfficeCode &&
                m.LandDetail.Province == province &&
                m.LandDetail.District == district &&
                m.LandDetail.SubDistrict == subDistrict &&
                m.LandDetail.TitleType == titleType &&
                m.LandDetail.TitleNumber == titleNumber &&
                m.LandDetail.SurveyNumber == surveyNumber &&
                m.LandDetail.LandParcelNumber == landParcelNumber)
            .AnyAsync(ct);
    }

    public async Task<bool> CondoDedupCollidesAsync(
        Guid excludeMasterId,
        string landOfficeCode, string condoRegistrationNumber,
        string buildingNumber, string floorNumber, string roomNumber,
        string titleNumber, string titleType,
        CancellationToken ct = default)
        => await dbContext.CollateralMasters
            .Where(m =>
                m.Id != excludeMasterId &&
                !m.IsDeleted &&
                m.CollateralType == CollateralTypes.Condo &&
                m.CondoDetail!.LandOfficeCode == landOfficeCode &&
                m.CondoDetail.CondoRegistrationNumber == condoRegistrationNumber &&
                m.CondoDetail.BuildingNumber == buildingNumber &&
                m.CondoDetail.FloorNumber == floorNumber &&
                m.CondoDetail.RoomNumber == roomNumber &&
                m.CondoDetail.TitleNumber == titleNumber &&
                m.CondoDetail.TitleType == titleType)
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
