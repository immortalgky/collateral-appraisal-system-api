using Collateral.CollateralMasters.Events;
using Collateral.Contracts;

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
    // BuildingValue: cost-approach only — from PricingFinalValue.BuildingValue. IsMaster only.
    decimal? BuildingValue,
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
    decimal? UsableArea,
    string? LocationType,
    int? BuildingAge,
    int? ConstructionYear,
    string? ModelName,
    // GPS coordinates (Phase 1 — geo filter prerequisite)
    decimal? Latitude,
    decimal? Longitude,
    // Appraisal summary
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate,
    // Three-value model (Phase C, wired in PR-8)
    decimal? UnitPrice,     // cost-approach only — PricingFinalValue.FinalValueAdjusted
    decimal? BuildingValue, // cost-approach only — PricingFinalValue.BuildingValue
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
    DateTime AppraisalDate,
    // Useful-life years from the appraisal's machinery cost item (outbound Collateral Result).
    decimal? LifeYear
);

/// <summary>
/// Parameters passed by the upsert service when updating a PRJ (block-project) master from an appraisal.
/// </summary>
public sealed record ProjectUpsertData(
    string ProjectType,
    string? ProjectName,
    string? Developer,
    string? Address,
    string? Province,
    decimal? Latitude,
    decimal? Longitude,
    int TotalUnits,
    int RemainingUnits,
    decimal? ProjectSellingPrice,
    // Per-unit master rows to replace the existing set.
    // Empty list is valid (no units yet); the existing rows will still be deleted.
    IReadOnlyList<ProjectUnit> Units,
    // Customer name from request.RequestCustomers (TOP 1 by RequestId).
    string? CustomerName,
    Guid AppraisalId,
    string AppraisalNumber,
    DateTime AppraisalDate
);

public class CollateralMaster : Aggregate<Guid>
{
    private readonly List<CollateralEngagement> _engagements = [];
    private readonly List<CollateralMasterAuditLog> _auditLogs = [];
    private readonly List<CollateralDocument> _documents = [];

    public IReadOnlyList<CollateralEngagement> Engagements => _engagements.AsReadOnly();
    public IReadOnlyList<CollateralMasterAuditLog> AuditLogs => _auditLogs.AsReadOnly();
    public IReadOnlyList<CollateralDocument> Documents => _documents.AsReadOnly();

    public string CollateralType { get; private set; } = null!;
    public string? OwnerName { get; private set; }

    /// <summary>
    /// Customer name for PRJ (block-project) masters — distinct from <see cref="OwnerName"/>
    /// (which is the developer/property owner). Populated by Phase 2 upsert from appraisal data.
    /// Phase 1: always null on creation.
    /// </summary>
    public string? CustomerName { get; private set; }

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
    public ProjectDetail? ProjectDetail { get; private set; }

    // --- Reappraisal exclusion (all types) ---
    public bool ExcludedFromReappraisal { get; private set; }
    public DateTime? ExcludedFromReappraisalAt { get; private set; }
    public string? ExcludedFromReappraisalBy { get; private set; }

    /// <summary>
    /// Host (AS400) collateral identifier (CCDCID). Populated by a future inbound host-mapping
    /// interface; NULL until then. Used as the key for the outbound Collateral Result interface —
    /// only masters with a non-null HostCollateralId are exported.
    /// </summary>
    public string? HostCollateralId { get; private set; }

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
        string? rawang,
        string? street,
        string? village,
        decimal? latitude,
        decimal? longitude,
        string? collateralType = null)
    {
        // Default to bare land; caller may pass "LB" when the appraisal includes buildings.
        var effectiveType = string.IsNullOrWhiteSpace(collateralType)
            ? CollateralTypes.Land
            : collateralType;

        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = effectiveType,
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.LandDetail = new LandDetail(
            master.Id,
            landOfficeCode, province, district, subDistrict, titleType, titleNumber,
            surveyNumber, landParcelNumber, rawang,
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
        string? landParcelNumber,
        string? rawang,
        string? collateralType = null)
    {
        var effectiveType = string.IsNullOrWhiteSpace(collateralType)
            ? CollateralTypes.Land
            : collateralType;

        var alias = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = effectiveType,
            OwnerName = null,
            IsDeleted = false,
            IsMaster = false,
            ParentMasterId = parentMasterId,
        };

        // Alias LandDetail carries only the dedup key; last-known fields stay null.
        alias.LandDetail = new LandDetail(
            alias.Id,
            landOfficeCode, province, district, subDistrict, titleType, titleNumber,
            surveyNumber, landParcelNumber, rawang,
            street: null, village: null,
            latitude: null, longitude: null,
            isDeleted: false);

        // No domain event for alias creation — alias is a structural detail, not a business event.
        return alias;
    }

    /// <summary>
    /// Demotes an existing IsMaster row to a typed alias of the appraisal's primary collateral.
    /// Used by the one-collateral-per-appraisal upsert path when a legacy standalone master
    /// (e.g. a Condo/Machine/Leasehold master created before this model existed) is discovered
    /// as a NON-primary component of an appraisal being (re)processed. The row keeps its own
    /// type detail (CondoDetail/MachineDetail/LeaseholdDetail) — only IsMaster/ParentMasterId flip.
    /// Guarded against demoting a row that already owns engagements: under the
    /// one-collateral-per-appraisal invariant, only the primary ever accumulates engagements
    /// (CollateralMasterUpsertService.AppendEngagement always targets the primary), so a non-primary
    /// row reaching this method with engagements attached indicates a bug upstream — fail loudly
    /// instead of silently orphaning engagement history.
    /// </summary>
    public void DemoteToAlias(Guid parentMasterId)
    {
        if (Engagements.Count > 0)
            throw new InvalidOperationException(
                $"Cannot demote CollateralMaster {Id} to an alias of {parentMasterId}: it already owns " +
                $"{Engagements.Count} engagement(s). This should not happen under the one-collateral-per-appraisal " +
                "model — investigate before retrying.");

        IsMaster = false;
        ParentMasterId = parentMasterId;
    }

    /// <summary>
    /// Promotes a typed alias back to a standalone IsMaster — used when a component that was
    /// previously demoted under a different appraisal's primary is now, in THIS appraisal, the
    /// primary collateral in its own right (e.g. a Condo alias reappraised standalone). Aliases
    /// never accumulate engagements (see DemoteToAlias's guard), so promotion is always safe.
    /// Idempotent no-op if already a master.
    /// </summary>
    public void PromoteToMaster()
    {
        if (IsMaster) return;
        IsMaster = true;
        ParentMasterId = null;
    }

    /// <summary>
    /// Creates a PRJ (block-project) master. Always IsMaster=true, ParentMasterId=null.
    /// Attaches a fresh ProjectDetail; caller must subsequently call UpsertFromProjectAppraisal.
    /// </summary>
    public static CollateralMaster CreateProject(string projectType, string? projectName)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Project,
            OwnerName = null,
            CustomerName = null, // Phase 2 populates from appraisal data
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.ProjectDetail = new ProjectDetail(master.Id, projectType, projectName, isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    /// <summary>
    /// Overwrites last-known fields on a PRJ (block-project) master.
    /// </summary>
    public void UpsertFromProjectAppraisal(ProjectUpsertData data)
    {
        if (ProjectDetail is null)
            throw new InvalidOperationException("UpsertFromProjectAppraisal called on a non-Project master.");

        ProjectDetail.UpdateStructure(
            data.ProjectType,
            data.ProjectName,
            data.Developer,
            data.Address,
            data.Province,
            data.Latitude,
            data.Longitude,
            data.TotalUnits,
            data.RemainingUnits,
            data.ProjectSellingPrice);

        // Replace the unit set with the incoming snapshot. FindProjectMasterByLastAppraisalIdAsync
        // eager-loads ProjectDetail.Units, so clearing the tracked collection makes EF delete the
        // orphaned old rows (required FK + cascade) and insert the new ones in the SAME SaveChanges —
        // an atomic replace for both first-appraisal (empty) and reappraisal (full swap).
        ProjectDetail.ReplaceUnits(data.Units);

        // Recalculate RemainingUnits from the in-memory unit sale flags.
        // TotalUnits was already set by UpdateStructure (from the appraisal-side count).
        ProjectDetail.RecountRemaining();

        SetCustomerName(data.CustomerName);

        ProjectDetail.UpdateAppraisalSummary(
            data.AppraisalId,
            data.AppraisalNumber,
            data.AppraisalDate);
    }

    /// <summary>
    /// Sets the customer name for a PRJ master. Called by the upsert service on each appraisal completion.
    /// Null is accepted (customer may not be available in the request).
    /// </summary>
    public void SetCustomerName(string? customerName)
    {
        CustomerName = customerName;
    }

    /// <summary>
    /// Marks this master as excluded from the next reappraisal cycle.
    /// </summary>
    public void MarkExcludedFromReappraisal(string? by, DateTime now)
    {
        ExcludedFromReappraisal = true;
        ExcludedFromReappraisalAt = now;
        ExcludedFromReappraisalBy = by;
    }

    /// <summary>
    /// Clears the reappraisal-exclusion flag so the master re-enters the next cycle.
    /// </summary>
    public void ClearExclusionFromReappraisal()
    {
        ExcludedFromReappraisal = false;
        ExcludedFromReappraisalAt = null;
        ExcludedFromReappraisalBy = null;
    }

    /// <summary>
    /// Sets the host (AS400) collateral identifier. Called by the future inbound host-mapping
    /// interface. Idempotent overwrite; blank is normalised to null.
    /// </summary>
    public void SetHostCollateralId(string? hostCollateralId)
    {
        HostCollateralId = string.IsNullOrWhiteSpace(hostCollateralId) ? null : hostCollateralId.Trim();
    }

    /// <summary>
    /// Flips the CollateralType discriminator to reflect the latest appraisal classification.
    /// Called by the upsert service (LATEST-wins): e.g. L → LB when a building appraisal is
    /// applied to an existing bare-land master.
    /// </summary>
    public void UpdateCollateralType(string collateralType)
    {
        if (string.IsNullOrWhiteSpace(collateralType))
            throw new ArgumentException("CollateralType must not be empty.", nameof(collateralType));
        if (string.Equals(CollateralType, collateralType, StringComparison.Ordinal))
            return; // idempotent — no event, no entity dirtying when the type already matches
        var old = CollateralType;
        CollateralType = collateralType;
        AddDomainEvent(new CollateralTypeChangedEvent(Id, old, collateralType));
    }

    public static CollateralMaster CreateCondo(
        string ownerName,
        string? landOfficeCode,
        string condoRegistrationNumber,
        string buildingNumber,
        string floorNumber,
        string roomNumber,
        string province,
        string district,
        string subDistrict,
        string? condoName)
    {
        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Condo, // "U"
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = true,
            ParentMasterId = null,
        };

        master.CondoDetail = new CondoDetail(
            master.Id,
            landOfficeCode, condoRegistrationNumber, buildingNumber, floorNumber, roomNumber,
            province, district, subDistrict, condoName,
            isDeleted: false);

        master.AddDomainEvent(new CollateralMasterCreatedEvent(master.Id, master.CollateralType));
        return master;
    }

    /// <summary>
    /// Creates an alias row for a Condo component that is NOT the appraisal's primary collateral
    /// (one-collateral-per-appraisal model). Unlike Land aliases, a Condo alias keeps its OWN full
    /// CondoDetail — only IsMaster/ParentMasterId mark it as structurally subordinate to the primary.
    /// No domain event — alias creation is a structural detail, not a business event.
    /// </summary>
    public static CollateralMaster CreateCondoAlias(
        Guid parentMasterId,
        string ownerName,
        string? landOfficeCode,
        string condoRegistrationNumber,
        string buildingNumber,
        string floorNumber,
        string roomNumber,
        string province,
        string district,
        string subDistrict,
        string? condoName)
    {
        var alias = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Condo, // "U"
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = false,
            ParentMasterId = parentMasterId,
        };

        alias.CondoDetail = new CondoDetail(
            alias.Id,
            landOfficeCode, condoRegistrationNumber, buildingNumber, floorNumber, roomNumber,
            province, district, subDistrict, condoName,
            isDeleted: false);

        return alias;
    }

    public static CollateralMaster CreateLeasehold(
        string lessee,
        string leaseRegistrationNo,
        Guid underlyingMasterId,
        string lessor,
        DateOnly leaseTermStart,
        string? collateralType = null)
    {
        // Default to bare leasehold; caller passes "LSB" / "LS" when the leasehold appraisal
        // covers a building or land+building so the master is born with the correct discriminator
        // — avoids firing both CollateralMasterCreatedEvent("LSL") and CollateralTypeChangedEvent("LSL"→"LS")
        // in the same SaveChanges when a fresh master is appraised as LS/LSB.
        var effectiveType = string.IsNullOrWhiteSpace(collateralType)
            ? CollateralTypes.Leasehold
            : collateralType;

        var master = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = effectiveType,
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

    /// <summary>
    /// Creates an alias row for a Leasehold component that is NOT the appraisal's primary collateral
    /// (one-collateral-per-appraisal model). Keeps its OWN full LeaseholdDetail (including the
    /// UnderlyingMasterId FK, unrelated to the IsMaster/ParentMasterId hierarchy) — only
    /// IsMaster/ParentMasterId mark it as structurally subordinate to the primary.
    /// No domain event — alias creation is a structural detail, not a business event.
    /// </summary>
    public static CollateralMaster CreateLeaseholdAlias(
        Guid parentMasterId,
        string lessee,
        string leaseRegistrationNo,
        Guid underlyingMasterId,
        string lessor,
        DateOnly leaseTermStart,
        string? collateralType = null)
    {
        var effectiveType = string.IsNullOrWhiteSpace(collateralType)
            ? CollateralTypes.Leasehold
            : collateralType;

        var alias = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = effectiveType,
            OwnerName = lessee,
            IsDeleted = false,
            IsMaster = false,
            ParentMasterId = parentMasterId,
        };

        alias.LeaseholdDetail = new LeaseholdDetail(
            alias.Id,
            leaseRegistrationNo, underlyingMasterId, lessor, lessee, leaseTermStart,
            isDeleted: false);

        return alias;
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
    /// Creates an alias row for a Machine component that is NOT the appraisal's primary collateral
    /// (one-collateral-per-appraisal model). Keeps its OWN full MachineDetail — only
    /// IsMaster/ParentMasterId mark it as structurally subordinate to the primary.
    /// No domain event — alias creation is a structural detail, not a business event.
    /// </summary>
    public static CollateralMaster CreateMachineAlias(
        Guid parentMasterId,
        string ownerName,
        string? machineRegistrationNo,
        string? serialNo,
        string? brand,
        string? model,
        string? manufacturer)
    {
        var alias = new CollateralMaster
        {
            Id = Guid.CreateVersion7(),
            CollateralType = CollateralTypes.Machine,
            OwnerName = ownerName,
            IsDeleted = false,
            IsMaster = false,
            ParentMasterId = parentMasterId,
        };

        alias.MachineDetail = new MachineDetail(
            alias.Id,
            machineRegistrationNo, serialNo, brand, model, manufacturer,
            isDeleted: false);

        return alias;
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

        LandDetail.UpdateValues(data.UnitPrice, data.BuildingValue, data.AppraisalValue);

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
            data.CondoName, data.UsableArea, data.LocationType,
            data.BuildingAge, data.ConstructionYear, data.ModelName,
            data.Latitude, data.Longitude);

        CondoDetail.UpdateValues(data.UnitPrice, data.BuildingValue, data.AppraisalValue);

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

        MachineDetail.SetLifeYear(data.LifeYear);

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
        string snapshot,
        DateTime createdAt,
        string? appraisedCollateralType = null,
        decimal? landAreaInSqWa = null,
        decimal? appraisalValue = null,
        decimal? forcedSaleValue = null,
        string? internalAppraiserName = null,
        decimal? landValue = null,
        decimal? buildingValue = null,
        string? appraisalCompanyCode = null)
    {
        if (!IsMaster)
            throw new InvalidOperationException(
                $"AppendEngagement must be called on the IsMaster row (Id={Id}). " +
                $"This row is an alias; its master is ParentMasterId={ParentMasterId}.");

        var engagement = new CollateralEngagement(
            Id, appraisalId, appraisalNumber, requestId, requestNumber,
            appraisalType, appraisalDate,
            appraiserUserId, appraisalCompanyId, appraisalCompanyName,
            constructionInspectionFeeAmount, snapshot, createdAt,
            appraisedCollateralType, landAreaInSqWa, appraisalValue,
            forcedSaleValue, internalAppraiserName, landValue, buildingValue,
            appraisalCompanyCode);

        _engagements.Add(engagement);
        AddDomainEvent(new CollateralEngagementAddedEvent(Id, engagement.Id, appraisalId));
    }

    /// <summary>
    /// Attaches a legal document to this master.
    /// The file must already be uploaded to the Document module; pass in the returned DocumentId.
    /// </summary>
    /// <param name="documentType">Must be one of <see cref="DocumentTypes"/> constants.</param>
    /// <param name="documentId">FK to the Document module's document store.</param>
    /// <param name="fileName">Original file name returned by the Document module upload.</param>
    /// <param name="description">Optional user-supplied description.</param>
    public CollateralDocument AttachDocument(
        string documentType,
        Guid documentId,
        string fileName,
        string? description)
    {
        if (!DocumentTypes.IsValid(documentType))
            throw new DomainException(
                $"Invalid document type '{documentType}'. " +
                $"Allowed values: {string.Join(", ", new[] { DocumentTypes.TitleDeed, DocumentTypes.LeaseContract, DocumentTypes.OwnershipCertificate, DocumentTypes.EncumbranceLetter, DocumentTypes.Other })}.");

        // Guard against double-submit / retry: refuse to attach the same upstream DocumentId
        // twice while it's still active. Archived duplicates are permitted (re-upload after archive).
        if (_documents.Any(d => d.IsActive && d.DocumentId == documentId))
            throw new DomainException(
                $"Document {documentId} is already attached to this collateral master.");

        var document = CollateralDocument.Create(Id, documentType, documentId, fileName, description);
        _documents.Add(document);
        AddDomainEvent(new CollateralDocumentAttachedEvent(Id, document.Id, documentId, documentType, fileName));
        return document;
    }

    /// <summary>
    /// Soft-archives the document row identified by its PK (<paramref name="documentRowId"/>).
    /// Idempotent: archiving an already-archived row is a no-op (consistent with DELETE semantics).
    /// Throws <see cref="NotFoundException"/> when no document with that Id exists on this master.
    /// </summary>
    public void ArchiveDocument(Guid documentRowId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentRowId);
        if (document is null)
            throw new NotFoundException("CollateralDocument", documentRowId);

        if (!document.IsActive)
            return;  // idempotent no-op

        document.Archive();
        AddDomainEvent(new CollateralDocumentArchivedEvent(Id, document.Id, document.DocumentId));
    }

    private void SyncIsDeletedToDetails(bool isDeleted)
    {
        if (LandDetail is not null) LandDetail.SetIsDeleted(isDeleted);
        if (CondoDetail is not null) CondoDetail.SetIsDeleted(isDeleted);
        if (LeaseholdDetail is not null) LeaseholdDetail.SetIsDeleted(isDeleted);
        if (MachineDetail is not null) MachineDetail.SetIsDeleted(isDeleted);
        if (ProjectDetail is not null) ProjectDetail.SetIsDeleted(isDeleted);
    }
}
