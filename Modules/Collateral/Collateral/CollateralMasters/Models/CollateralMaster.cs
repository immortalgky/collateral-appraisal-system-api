using Collateral.CollateralMasters.Events;

namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Parameters passed by the upsert service when updating a Land master from an appraisal.
/// </summary>
public sealed record LandUpsertData(
    // Owner
    string? OwnerName,
    // Last-known land context
    string? LandShapeType,
    string? LandZoneType,
    string? UrbanPlanningType,
    decimal? AccessRoadWidth,
    decimal? RoadFrontage,
    decimal? LandArea,
    string? Street,
    string? Village,
    decimal? Latitude,
    decimal? Longitude,
    // Appraisal summary
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate,
    // Construction tracking
    bool IsUnderConstruction,
    decimal? OverallConstructionProgressPercent,
    // Three-value model (Phase C, wired in PR-8)
    // UnitPrice: cost-approach only — from PricingFinalValue.FinalValueAdjusted. IsMaster + aliases.
    decimal? UnitPrice,
    // BuildingCost: cost-approach only — from PricingFinalValue.BuildingCost. IsMaster only.
    decimal? BuildingCost,
    // AppraisalValue: all approaches — from PricingFinalValue.AppraisalPrice (fallbacks: FinalValueAdjusted, FinalValueRounded). IsMaster only.
    decimal? AppraisalValue
);

/// <summary>
/// Parameters passed by the upsert service when updating a Condo master from an appraisal.
/// </summary>
public sealed record CondoUpsertData(
    // Owner
    string? OwnerName,
    // Last-known
    string? CondoName,
    string? Province,
    decimal? UsableArea,
    string? LocationType,
    int? BuildingAge,
    int? ConstructionYear,
    string? ModelName,
    // Appraisal summary
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate,
    // Three-value model (Phase C, wired in PR-8)
    decimal? UnitPrice,    // cost-approach only — PricingFinalValue.FinalValueAdjusted
    decimal? BuildingCost, // cost-approach only — PricingFinalValue.BuildingCost
    decimal? AppraisalValue // all approaches — PricingFinalValue.AppraisalPrice (with fallbacks)
);

/// <summary>
/// Parameters passed by the upsert service when updating a Leasehold master from an appraisal.
/// </summary>
public sealed record LeaseholdUpsertData(
    DateOnly? LeaseTermEnd,
    int? LeaseTermMonths,
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate
);

/// <summary>
/// Parameters passed by the upsert service when updating a Machine master from an appraisal.
/// </summary>
public sealed record MachineUpsertData(
    string? IncomingRegistrationNo,
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate
);

public class CollateralMaster : Aggregate<Guid>
{
    private readonly List<CollateralEngagement> _engagements = [];
    private readonly List<CollateralMasterAuditLog> _auditLogs = [];

    public IReadOnlyList<CollateralEngagement> Engagements => _engagements.AsReadOnly();
    public IReadOnlyList<CollateralMasterAuditLog> AuditLogs => _auditLogs.AsReadOnly();

    public string CollateralType { get; private set; } = null!;
    public string? OwnerName { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// True = primary row carrying all heavy data and engagements.
    /// False = alias row pointing at the IsMaster row via ParentMasterId.
    /// All non-Land types are always IsMaster=true (singleton groups).
    /// </summary>
    public bool IsMaster { get; private set; } = true;

    /// <summary>
    /// Self-FK to the IsMaster row in the same group. NULL when IsMaster=true.
    /// </summary>
    public Guid? ParentMasterId { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Updated automatically by SQL Server on every write.
    /// EditCollateralMasterCommandHandler returns 409 on DbUpdateConcurrencyException.
    /// </summary>
    public byte[]? RowVersion { get; private set; }

    public LandDetail? LandDetail { get; private set; }
    public CondoDetail? CondoDetail { get; private set; }
    public LeaseholdDetail? LeaseholdDetail { get; private set; }
    public MachineDetail? MachineDetail { get; private set; }

    private CollateralMaster() { }

    public static CollateralMaster CreateLand(
        string ownerName,
        string landOfficeCode,
        string province,
        string district,
        string subDistrict,
        string titleType,
        string titleNumber,
        string? surveyNumber,
        string? landParcelNumber,
        string? street,
        string? village,
        decimal? latitude,
        decimal? longitude)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Land,
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.LandDetail = new LandDetail(
            master.Id,
            landOfficeCode, province, district, subDistrict, titleType, titleNumber,
            surveyNumber, landParcelNumber,
            street, village, latitude, longitude,
            isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    /// <summary>
    /// Creates an alias row for a Land property in a multi-title group.
    /// Alias rows carry only the dedup-key columns in LandDetail; heavy data lives on the IsMaster row.
    /// Engagements and last-known updates must go through the IsMaster row.
    /// </summary>
    public static CollateralMaster CreateLandAlias(
        Guid parentMasterId,
        string landOfficeCode,
        string province,
        string district,
        string subDistrict,
        string titleType,
        string titleNumber,
        string? surveyNumber,
        string? landParcelNumber)
    {
        var alias = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Land,
            OwnerName = null,
            IsDeleted = false,
            IsMaster = false,
            ParentMasterId = parentMasterId,
        };

        // Alias LandDetail carries only the dedup key; last-known fields stay null.
        alias.LandDetail = new LandDetail(
            alias.Id,
            landOfficeCode, province, district, subDistrict, titleType, titleNumber,
            surveyNumber, landParcelNumber,
            street: null, village: null,
            latitude: null, longitude: null,
            isDeleted: false);

        // No domain event for alias creation — alias is a structural detail, not a business event.
        return alias;
    }

    public static CollateralMaster CreateCondo(
        string ownerName,
        string landOfficeCode,
        string condoRegistrationNumber,
        string buildingNumber,
        string floorNumber,
        string roomNumber,
        string titleNumber,
        string titleType,
        string? condoName,
        string? province)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Condo,
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.CondoDetail = new CondoDetail(
            master.Id,
            landOfficeCode, condoRegistrationNumber, buildingNumber, floorNumber, roomNumber, titleNumber, titleType,
            condoName, province,
            isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    public static CollateralMaster CreateLeasehold(
        string lessee,
        string leaseRegistrationNo,
        Guid underlyingMasterId,
        string lessor,
        DateOnly leaseTermStart)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Leasehold,
            OwnerName = lessee,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.LeaseholdDetail = new LeaseholdDetail(
            master.Id,
            leaseRegistrationNo, underlyingMasterId, lessor, lessee, leaseTermStart,
            isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    public static CollateralMaster CreateMachine(
        string ownerName,
        string? machineRegistrationNo,
        string? serialNo,
        string? brand,
        string? model,
        string? manufacturer)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Machine,
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.MachineDetail = new MachineDetail(
            master.Id,
            machineRegistrationNo, serialNo, brand, model, manufacturer,
            isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    /// <summary>
    /// Overwrites last-known fields on a Land master and updates construction tracking.
    /// Raises ConstructionStatusChangedEvent when IsUnderConstructionAtLastAppraisal flips.
    /// </summary>
    public void UpsertFromLandAppraisal(LandUpsertData data)
    {
        if (LandDetail is null)
            throw new InvalidOperationException("UpsertFromLandAppraisal called on a non-Land master.");

        if (!IsMaster)
            throw new InvalidOperationException(
                $"UpsertFromLandAppraisal must be called on the IsMaster row (Id={Id}). " +
                $"This row is an alias; its master is ParentMasterId={ParentMasterId}.");

        // Populate OwnerName on the IsMaster aggregate root
        if (!string.IsNullOrWhiteSpace(data.OwnerName))
            OwnerName = data.OwnerName;

        LandDetail.UpdateLastKnown(
            data.LandShapeType, data.LandZoneType, data.UrbanPlanningType,
            data.AccessRoadWidth, data.RoadFrontage, data.LandArea,
            data.Street, data.Village,
            data.Latitude, data.Longitude);

        LandDetail.UpdateValues(data.UnitPrice, data.BuildingCost, data.AppraisalValue);

        bool wasUnderConstruction = LandDetail.IsUnderConstructionAtLastAppraisal;
        decimal? fromPercent = LandDetail.OverallConstructionProgressPercent;

        LandDetail.UpdateAppraisalSummary(
            data.AppraisalId, data.AppraisalNumber, data.AppraisalDate,
            data.IsUnderConstruction, data.OverallConstructionProgressPercent);

        // Raise domain event when construction flag changes
        if (wasUnderConstruction != data.IsUnderConstruction)
        {
            AddDomainEvent(new ConstructionStatusChangedEvent(
                Id,
                wasUnderConstruction,
                data.IsUnderConstruction,
                fromPercent,
                data.OverallConstructionProgressPercent));
        }
    }

    /// <summary>
    /// Overwrites last-known fields on a Condo master.
    /// </summary>
    public void UpsertFromCondoAppraisal(CondoUpsertData data)
    {
        if (CondoDetail is null)
            throw new InvalidOperationException("UpsertFromCondoAppraisal called on a non-Condo master.");

        // Populate OwnerName on the aggregate root
        if (!string.IsNullOrWhiteSpace(data.OwnerName))
            OwnerName = data.OwnerName;

        CondoDetail.UpdateLastKnown(
            data.CondoName, data.Province, data.UsableArea, data.LocationType,
            data.BuildingAge, data.ConstructionYear, data.ModelName);

        CondoDetail.UpdateValues(data.UnitPrice, data.BuildingCost, data.AppraisalValue);

        CondoDetail.UpdateAppraisalSummary(
            data.AppraisalId, data.AppraisalNumber, data.AppraisalDate);
    }

    /// <summary>
    /// Overwrites last-known fields on a Leasehold master.
    /// </summary>
    public void UpsertFromLeaseholdAppraisal(LeaseholdUpsertData data)
    {
        if (LeaseholdDetail is null)
            throw new InvalidOperationException("UpsertFromLeaseholdAppraisal called on a non-Leasehold master.");

        LeaseholdDetail.UpdateLastKnown(data.LeaseTermEnd, data.LeaseTermMonths);

        LeaseholdDetail.UpdateAppraisalSummary(data.AppraisalId, data.AppraisalNumber, data.AppraisalDate);
    }

    /// <summary>
    /// Overwrites last-known fields on a Machine master.
    /// Also handles promotion: if the existing master has no registration number but the incoming
    /// data provides one that matches the composite, sets the registration number.
    /// </summary>
    public void UpsertFromMachineAppraisal(MachineUpsertData data)
    {
        if (MachineDetail is null)
            throw new InvalidOperationException("UpsertFromMachineAppraisal called on a non-Machine master.");

        // Promotion: existing master stored with composite-only but now has a registration number
        if (!string.IsNullOrWhiteSpace(data.IncomingRegistrationNo)
            && string.IsNullOrWhiteSpace(MachineDetail.MachineRegistrationNo))
        {
            MachineDetail.PromoteToRegistration(data.IncomingRegistrationNo);
        }

        MachineDetail.UpdateAppraisalSummary(data.AppraisalId, data.AppraisalNumber, data.AppraisalDate);
    }

    /// <summary>
    /// Edits admin-accessible fields. Raises CollateralMasterEditedEvent carrying a JSON
    /// field-diff so the audit log writer can persist the change log.
    /// </summary>
    public void Edit(string? ownerName, LandAdminEdit? land, CondoAdminEdit? condo,
        LeaseholdAdminEdit? leasehold, MachineAdminEdit? machine,
        string reason, string by)
    {
        if (!IsMaster)
            throw new InvalidOperationException(
                $"Edit must be called on the IsMaster row (Id={Id}). " +
                $"This row is an alias; its master is ParentMasterId={ParentMasterId}.");

        var diff = new System.Collections.Generic.Dictionary<string, object?>();

        if (ownerName is not null && ownerName != OwnerName)
        {
            diff["OwnerName"] = new { from = OwnerName, to = ownerName };
            OwnerName = ownerName;
        }

        if (land is not null && LandDetail is not null)
            LandDetail.ApplyAdminEdit(land, diff);

        if (condo is not null && CondoDetail is not null)
            CondoDetail.ApplyAdminEdit(condo, diff);

        if (leasehold is not null && LeaseholdDetail is not null)
            LeaseholdDetail.ApplyAdminEdit(leasehold, diff);

        if (machine is not null && MachineDetail is not null)
            MachineDetail.ApplyAdminEdit(machine, diff);

        var changedFields = System.Text.Json.JsonSerializer.Serialize(diff);
        AddDomainEvent(new CollateralMasterEditedEvent(Id, changedFields, reason, by));
    }

    public void SoftDelete(string reason, string by)
    {
        IsDeleted = true;
        SyncIsDeletedToDetails(true);
        AddDomainEvent(new CollateralMasterSoftDeletedEvent(Id, reason, by));
    }

    public void Restore(string reason, string by)
    {
        IsDeleted = false;
        SyncIsDeletedToDetails(false);
        AddDomainEvent(new CollateralMasterRestoredEvent(Id, reason, by));
    }

    public void AppendEngagement(
        Guid appraisalId,
        string appraisalNumber,
        Guid requestId,
        string requestNumber,
        string appraisalType,
        DateTime appraisalDate,
        string? appraiserUserId,
        Guid? appraisalCompanyId,
        string? appraisalCompanyName,
        decimal? constructionInspectionFeeAmount,
        string snapshot)
    {
        if (!IsMaster)
            throw new InvalidOperationException(
                $"AppendEngagement must be called on the IsMaster row (Id={Id}). " +
                $"This row is an alias; its master is ParentMasterId={ParentMasterId}.");

        var engagement = new CollateralEngagement(
            Id, appraisalId, appraisalNumber, requestId, requestNumber,
            appraisalType, appraisalDate,
            appraiserUserId, appraisalCompanyId, appraisalCompanyName,
            constructionInspectionFeeAmount, snapshot);

        _engagements.Add(engagement);
        AddDomainEvent(new CollateralEngagementAddedEvent(Id, engagement.Id, appraisalId));
    }

    private void SyncIsDeletedToDetails(bool isDeleted)
    {
        if (LandDetail is not null) LandDetail.SetIsDeleted(isDeleted);
        if (CondoDetail is not null) CondoDetail.SetIsDeleted(isDeleted);
        if (LeaseholdDetail is not null) LeaseholdDetail.SetIsDeleted(isDeleted);
        if (MachineDetail is not null) MachineDetail.SetIsDeleted(isDeleted);
    }
}
