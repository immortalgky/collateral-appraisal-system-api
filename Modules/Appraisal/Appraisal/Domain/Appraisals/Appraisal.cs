namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appraisal Aggregate Root - Main entry point for appraisal operations.
/// Manages the complete appraisal lifecycle from creation to completion.
/// </summary>
public class Appraisal : Aggregate<Guid>
{
    // Private collections for child entities
    private readonly List<AppraisalProperty> _properties = [];
    private readonly List<PropertyGroup> _groups = [];
    private readonly List<AppraisalAssignment> _assignments = [];
    private readonly List<CondoModel> _condoModels = [];
    private readonly List<CondoTower> _condoTowers = [];
    private readonly List<CondoUnit> _condoUnits = [];
    private readonly List<CondoUnitUpload> _condoUnitUploads = [];
    private readonly List<VillageModel> _villageModels = [];
    private readonly List<VillageUnit> _villageUnits = [];
    private readonly List<VillageUnitUpload> _villageUnitUploads = [];

    // Read-only accessors
    public IReadOnlyList<AppraisalProperty> Properties => _properties.AsReadOnly();
    public IReadOnlyList<PropertyGroup> Groups => _groups.AsReadOnly();
    public IReadOnlyList<AppraisalAssignment> Assignments => _assignments.AsReadOnly();
    public IReadOnlyList<CondoModel> CondoModels => _condoModels.AsReadOnly();
    public IReadOnlyList<CondoTower> CondoTowers => _condoTowers.AsReadOnly();
    public IReadOnlyList<CondoUnit> CondoUnits => _condoUnits.AsReadOnly();
    public IReadOnlyList<CondoUnitUpload> CondoUnitUploads => _condoUnitUploads.AsReadOnly();
    public CondoProject? CondoProject { get; private set; }
    public CondoPricingAssumption? CondoPricingAssumption { get; private set; }
    public IReadOnlyList<VillageModel> VillageModels => _villageModels.AsReadOnly();
    public IReadOnlyList<VillageUnit> VillageUnits => _villageUnits.AsReadOnly();
    public IReadOnlyList<VillageUnitUpload> VillageUnitUploads => _villageUnitUploads.AsReadOnly();
    public VillageProject? VillageProject { get; private set; }
    public VillageProjectLand? VillageProjectLand { get; private set; }
    public VillagePricingAssumption? VillagePricingAssumption { get; private set; }

    // Core Properties
    public string? AppraisalNumber { get; private set; }
    public Guid RequestId { get; private set; }
    public AppraisalStatus Status { get; private set; } = null!;
    public string AppraisalType { get; private set; } = null!; // Initial, Revaluation, Special
    public string Priority { get; private set; } = null!; // Normal, High

    // Request-level properties for workflow routing
    public bool IsPma { get; private set; }
    public string? Purpose { get; private set; }
    public string? Channel { get; private set; }
    public string? BankingSegment { get; private set; }
    public decimal? FacilityLimit { get; private set; }
    public bool HasAppraisalBook { get; private set; }

    // Request metadata (denormalized from Request aggregate at creation time)
    public string? RequestedBy { get; private set; }
    public DateTime? RequestedAt { get; private set; }

    // SLA Tracking
    public int? SLADays { get; private set; }
    public DateTime? SLADueDate { get; private set; }
    public string? SLAStatus { get; private set; } // OnTrack, AtRisk, Breached
    public int? ActualDaysToComplete { get; private set; }
    public bool? IsWithinSLA { get; private set; }

    // Committee approval evidence
    public DateTime? CompletedAt { get; private set; }
    public string? ApprovedByCommittee { get; private set; }

    // Soft Delete
    public SoftDelete SoftDelete { get; private set; } = SoftDelete.NotDeleted();

    // Private constructor for EF Core
    private Appraisal()
    {
    }

    // Private constructor for factory method
    private Appraisal(
        Guid requestId,
        string appraisalType,
        string priority,
        int? slaDays,
        bool isPma,
        string? purpose,
        string? channel,
        string? bankingSegment,
        decimal? facilityLimit,
        bool hasAppraisalBook,
        string? requestedBy,
        DateTime? requestedAt)
    {
        Id = Guid.CreateVersion7();
        RequestId = requestId;
        AppraisalType = appraisalType;
        Priority = priority;
        Status = AppraisalStatus.Pending;
        SLADays = slaDays;
        IsPma = isPma;
        Purpose = purpose;
        Channel = channel;
        BankingSegment = bankingSegment;
        FacilityLimit = facilityLimit;
        HasAppraisalBook = hasAppraisalBook;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;

        if (slaDays.HasValue)
        {
            SLADueDate = DateTime.UtcNow.AddDays(slaDays.Value);
            SLAStatus = "OnTrack";
        }
    }

    /// <summary>
    /// Factory method to create a new Appraisal
    /// </summary>
    public static Appraisal Create(
        Guid requestId,
        string appraisalType,
        string priority,
        int? slaDays = null,
        string? requestedBy = null,
        bool isPma = false,
        string? purpose = null,
        string? channel = null,
        string? bankingSegment = null,
        decimal? facilityLimit = null,
        bool hasAppraisalBook = false,
        DateTime? requestedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appraisalType);
        ArgumentException.ThrowIfNullOrWhiteSpace(priority);

        var appraisal = new Appraisal(requestId, appraisalType, priority, slaDays,
            isPma, purpose, channel, bankingSegment, facilityLimit, hasAppraisalBook,
            requestedBy, requestedAt);
        appraisal.AddDomainEvent(new AppraisalCreatedEvent(appraisal, requestedBy));

        return appraisal;
    }

    /// <summary>
    /// Set the appraisal number (typically auto-generated)
    /// </summary>
    public void SetAppraisalNumber(string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        AppraisalNumber = number;
    }

    #region Property Management

    /// <summary>
    /// Add a property to this appraisal
    /// </summary>
    public AppraisalProperty AddProperty(string propertyType, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyType);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, propertyType, description);
        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a land property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddLandProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Land);

        var landDetail = LandAppraisalDetail.Create(property.Id);
        property.SetLandDetail(landDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a building property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddBuildingProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Building);

        var buildingDetail = BuildingAppraisalDetail.Create(property.Id);
        property.SetBuildingDetail(buildingDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a condo property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddCondoProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Condo);

        var condoDetail = CondoAppraisalDetail.Create(property.Id);
        property.SetCondoDetail(condoDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a land and building property with details to this appraisal.
    /// Creates both LandAppraisalDetail and BuildingAppraisalDetail linked to the same AppraisalProperty.
    /// </summary>
    public AppraisalProperty AddLandAndBuildingProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LandAndBuilding);

        // Create both detail records linked to the same property
        var landDetail = LandAppraisalDetail.Create(property.Id);
        var buildingDetail = BuildingAppraisalDetail.Create(property.Id);
        property.SetLandAndBuildingDetails(landDetail, buildingDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a vehicle property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddVehicleProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Vehicle);

        var vehicleDetail = VehicleAppraisalDetail.Create(property.Id);
        property.SetVehicleDetail(vehicleDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a vessel property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddVesselProperty()
    {

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Vessel);

        var vesselDetail = VesselAppraisalDetail.Create(property.Id);
        property.SetVesselDetail(vesselDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a machinery property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddMachineryProperty()
    {

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Machinery);

        var machineryDetail = MachineryAppraisalDetail.Create(property.Id);
        property.SetMachineryDetail(machineryDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a lease agreement land property with land detail, lease agreement, and rental info
    /// </summary>
    public AppraisalProperty AddLeaseAgreementLandProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LeaseAgreementLand);

        property.SetLandDetail(LandAppraisalDetail.Create(property.Id));
        property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        property.SetRentalInfo(RentalInfo.Create(property.Id));

        _properties.Add(property);
        return property;
    }

    /// <summary>
    /// Add a lease agreement building property with building detail, lease agreement, and rental info
    /// </summary>
    public AppraisalProperty AddLeaseAgreementBuildingProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LeaseAgreementBuilding);

        property.SetBuildingDetail(BuildingAppraisalDetail.Create(property.Id));
        property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        property.SetRentalInfo(RentalInfo.Create(property.Id));

        _properties.Add(property);
        return property;
    }

    /// <summary>
    /// Add a lease agreement land and building property with both details, lease agreement, and rental info
    /// </summary>
    public AppraisalProperty AddLeaseAgreementLandAndBuildingProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LeaseAgreementLandAndBuilding);

        property.SetLandAndBuildingDetails(
            LandAppraisalDetail.Create(property.Id),
            BuildingAppraisalDetail.Create(property.Id));
        property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        property.SetRentalInfo(RentalInfo.Create(property.Id));

        _properties.Add(property);
        return property;
    }

    /// <summary>
    /// Deep-copy an existing property. Call SaveChanges to generate the ID,
    /// then use AddPropertyToGroup to assign it to a group.
    /// </summary>
    public AppraisalProperty CopyProperty(Guid sourcePropertyId)
    {
        var source = _properties.FirstOrDefault(p => p.Id == sourcePropertyId)
                     ?? throw new InvalidOperationException($"Property {sourcePropertyId} not found");

        var sequenceNumber = _properties.Count + 1;
        var newProperty = AppraisalProperty.Create(Id, sequenceNumber, source.PropertyType);

        if (source.PropertyType == PropertyType.Land)
        {
            newProperty.SetLandDetail(LandAppraisalDetail.CopyFrom(source.LandDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.Building)
        {
            newProperty.SetBuildingDetail(BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LandAndBuilding)
        {
            newProperty.SetLandAndBuildingDetails(
                LandAppraisalDetail.CopyFrom(source.LandDetail!, newProperty.Id),
                BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.Condo)
        {
            newProperty.SetCondoDetail(CondoAppraisalDetail.CopyFrom(source.CondoDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.Vehicle)
        {
            newProperty.SetVehicleDetail(VehicleAppraisalDetail.CopyFrom(source.VehicleDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.Vessel)
        {
            newProperty.SetVesselDetail(VesselAppraisalDetail.CopyFrom(source.VesselDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.Machinery)
        {
            newProperty.SetMachineryDetail(MachineryAppraisalDetail.CopyFrom(source.MachineryDetail!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementLand)
        {
            newProperty.SetLandDetail(LandAppraisalDetail.CopyFrom(source.LandDetail!, newProperty.Id));
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!, newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementBuilding)
        {
            newProperty.SetBuildingDetail(BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!, newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementLandAndBuilding)
        {
            newProperty.SetLandAndBuildingDetails(
                LandAppraisalDetail.CopyFrom(source.LandDetail!, newProperty.Id),
                BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!, newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }

        _properties.Add(newProperty);

        return newProperty;
    }

    /// <summary>
    /// Get a property by ID
    /// </summary>
    public AppraisalProperty? GetProperty(Guid propertyId)
    {
        return _properties.FirstOrDefault(p => p.Id == propertyId);
    }

    /// <summary>
    /// Remove a property from this appraisal
    /// </summary>
    public void RemoveProperty(Guid propertyId)
    {
        var property = _properties.FirstOrDefault(c => c.Id == propertyId);
        if (property is null)
            throw new InvalidOperationException($"Property {propertyId} not found");

        _properties.Remove(property);

        // Resequence remaining properties
        for (var i = 0; i < _properties.Count; i++) _properties[i].UpdateSequence(i + 1);
    }

    #endregion

    #region Group Management

    /// <summary>
    /// Create a new property group
    /// </summary>
    public PropertyGroup CreateGroup(string groupName, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var groupNumber = _groups.Count + 1;
        var group = PropertyGroup.Create(Id, groupNumber, groupName, description);
        _groups.Add(group);

        return group;
    }

    /// <summary>
    /// Add a property to a group
    /// </summary>
    public void AddPropertyToGroup(Guid groupId, Guid propertyId)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new InvalidOperationException($"Group {groupId} not found");

        var property = _properties.FirstOrDefault(c => c.Id == propertyId)
                       ?? throw new InvalidOperationException($"Property {propertyId} not found");

        // Check if property is already in another group
        var existingGroup = _groups.FirstOrDefault(g => g.Items.Any(i => i.AppraisalPropertyId == propertyId));
        if (existingGroup is not null)
            throw new InvalidOperationException($"Property {propertyId} is already in group {existingGroup.GroupName}");

        group.AddProperty(propertyId);
    }

    /// <summary>
    /// Remove a property from a group
    /// </summary>
    public void RemovePropertyFromGroup(Guid groupId, Guid propertyId)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new InvalidOperationException($"Group {groupId} not found");

        group.RemoveProperty(propertyId);
    }

    /// <summary>
    /// Delete a property group
    /// </summary>
    public void DeleteGroup(Guid groupId)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new InvalidOperationException($"Group {groupId} not found");

        if (group.Items.Count > 0)
            throw new InvalidOperationException(
                $"Cannot delete group '{group.GroupName}' because it still contains {group.Items.Count} property(ies). Remove all properties from the group first.");

        _groups.Remove(group);

        // Resequence remaining groups
        for (var i = 0; i < _groups.Count; i++) _groups[i].UpdateGroupNumber(i + 1);
    }

    /// <summary>
    /// Move a property from its current group to a target group at an optional position.
    /// The source group is discovered automatically.
    /// </summary>
    public void MovePropertyToGroup(Guid propertyId, Guid targetGroupId, int? targetPosition)
    {
        var targetGroup = _groups.FirstOrDefault(g => g.Id == targetGroupId)
                          ?? throw new InvalidOperationException($"Target group {targetGroupId} not found");

        var sourceGroup = _groups.FirstOrDefault(g => g.Items.Any(i => i.AppraisalPropertyId == propertyId))
                          ?? throw new InvalidOperationException($"Property {propertyId} is not in any group");

        if (sourceGroup.Id == targetGroupId)
            throw new InvalidOperationException("Property is already in the target group");

        // Remove from source (auto-resequences remaining items)
        sourceGroup.RemoveProperty(propertyId);

        // Add to target at position, or append
        if (targetPosition.HasValue)
            targetGroup.InsertProperty(propertyId, targetPosition.Value);
        else
            targetGroup.AddProperty(propertyId);
    }

    /// <summary>
    /// Reorder properties within a group using a full ordered list of property IDs.
    /// </summary>
    public void ReorderPropertiesInGroup(Guid groupId, List<Guid> orderedPropertyIds)
    {
        var group = _groups.FirstOrDefault(g => g.Id == groupId)
                    ?? throw new InvalidOperationException($"Group {groupId} not found");

        group.ReorderProperties(orderedPropertyIds);
    }

    #endregion

    #region Assignment Management

    /// <summary>
    /// Assign the appraisal to an internal user or external company
    /// </summary>
    public AppraisalAssignment Assign(
        string assignmentType,
        string? assigneeUserId = null,
        string? assigneeCompanyId = null,
        string assignmentMethod = "Manual",
        string? internalAppraiserId = null,
        string? internalFollowupAssignmentMethod = null,
        Guid? autoRuleId = null,
        string assignedBy = "")
    {
        ValidateCanAssign();

        var previousAssignment = _assignments.LastOrDefault();
        var reassignmentNumber = previousAssignment?.ReassignmentNumber + 1 ?? 1;

        var assignment = AppraisalAssignment.Create(
            Id,
            assignmentType,
            assigneeUserId,
            assigneeCompanyId,
            assignmentMethod,
            internalAppraiserId,
            internalFollowupAssignmentMethod,
            autoRuleId,
            previousAssignment?.Id,
            reassignmentNumber,
            assignedBy);

        _assignments.Add(assignment);
        UpdateStatus(AppraisalStatus.Assigned);

        AddDomainEvent(new AppraisalAssignedEvent(this, assignment));

        return assignment;
    }

    public AppraisalAssignment AssignAdmin()
    {
        var assignment = AppraisalAssignment.Create(
            Id,
            "Internal",
            null,
            null,
            "Manual",
            null,
            null,
            null,
            null,
            1,
            "System");

        _assignments.Add(assignment);
        UpdateStatus(AppraisalStatus.Pending);

        AddDomainEvent(new AppraisalAssignedEvent(this, assignment));

        return assignment;
    }

    private void ValidateCanAssign()
    {
        if (Status != AppraisalStatus.Pending && Status != AppraisalStatus.Assigned)
            throw new InvalidAppraisalStateException(
                $"Cannot assign appraisal in status '{Status}'. Must be Pending or Assigned.");

        // Check if there's an active assignment
        var activeAssignment = _assignments.FirstOrDefault(a =>
            a.AssignmentStatus == AssignmentStatus.Assigned ||
            a.AssignmentStatus == AssignmentStatus.InProgress);

        if (activeAssignment is not null)
            throw new InvalidAppraisalStateException(
                "Cannot create new assignment while there is an active assignment. " +
                "Please reject or complete the current assignment first.");
    }

    #endregion

    #region Status Management

    /// <summary>
    /// Start work on this appraisal
    /// </summary>
    public void StartWork()
    {
        ValidateStatus(AppraisalStatus.Assigned, "start work on");

        var activeAssignment = GetActiveAssignment();
        activeAssignment.StartWork();

        UpdateStatus(AppraisalStatus.InProgress);
        UpdateSlaStatus();
    }

    // TODO: Remove this method once a proper workflow transitions appraisal to UnderReview before committee voting.
    public void EnsureUnderReview()
    {
        if (Status == AppraisalStatus.UnderReview)
            return;
        UpdateStatus(AppraisalStatus.UnderReview);
    }

    /// <summary>
    /// Submit appraisal for review
    /// </summary>
    public void SubmitForReview()
    {
        ValidateStatus(AppraisalStatus.InProgress, "submit for review");

        var activeAssignment = GetActiveAssignment();
        activeAssignment.Complete();

        UpdateStatus(AppraisalStatus.UnderReview);
    }

    /// <summary>
    /// Complete the appraisal (after review approval)
    /// </summary>
    public void Complete()
    {
        ValidateStatus(AppraisalStatus.UnderReview, "complete");

        UpdateStatus(AppraisalStatus.Completed);

        // Calculate actual days
        ActualDaysToComplete = CreatedAt.HasValue ? (DateTime.UtcNow - CreatedAt.Value).Days : null;
        IsWithinSLA = !SLADueDate.HasValue || DateTime.UtcNow <= SLADueDate.Value;

        AddDomainEvent(new AppraisalCompletedEvent(this));
    }

    /// <summary>
    /// Stamps committee approval evidence. Idempotent — no-op if already stamped.
    /// Does NOT touch Status; status will be derived from workflow state later.
    /// </summary>
    public void MarkApprovedByCommittee(string committeeCode, DateTime approvedAt)
    {
        if (CompletedAt.HasValue) return; // idempotent guard — safe on pipeline retries
        CompletedAt = approvedAt;
        ApprovedByCommittee = committeeCode;
    }

    /// <summary>
    /// Cancel the appraisal
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status == AppraisalStatus.Completed)
            throw new InvalidAppraisalStateException("Cannot cancel a completed appraisal");

        UpdateStatus(AppraisalStatus.Cancelled);

        // Cancel any active assignment
        var activeAssignment = _assignments.FirstOrDefault(a =>
            a.AssignmentStatus == AssignmentStatus.Assigned ||
            a.AssignmentStatus == AssignmentStatus.InProgress);

        activeAssignment?.Cancel(reason);
    }

    private void UpdateStatus(AppraisalStatus newStatus)
    {
        var oldStatus = Status;
        Status = newStatus;

        AddDomainEvent(new AppraisalStatusChangedEvent(this, oldStatus, newStatus));
    }

    private void ValidateStatus(AppraisalStatus expectedStatus, string action)
    {
        if (Status != expectedStatus)
            throw new InvalidAppraisalStateException(
                $"Cannot {action} appraisal in status '{Status}'. Expected status: '{expectedStatus}'");
    }

    private AppraisalAssignment GetActiveAssignment()
    {
        return _assignments.FirstOrDefault(a =>
                   a.AssignmentStatus == AssignmentStatus.Assigned ||
                   a.AssignmentStatus == AssignmentStatus.InProgress)
               ?? throw new InvalidAppraisalStateException("No active assignment found");
    }

    #endregion

    #region SLA Management

    /// <summary>
    /// Update SLA status based on current date
    /// </summary>
    public void UpdateSlaStatus()
    {
        if (!SLADueDate.HasValue) return;

        var daysRemaining = (SLADueDate.Value - DateTime.UtcNow).Days;
        SLAStatus = daysRemaining switch
        {
            < 0 => "Breached",
            < 2 => "AtRisk",
            _ => "OnTrack"
        };
    }

    #endregion

    #region Block Condo Management

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public CondoProject SetCondoProject(
        // Project Info
        string? projectName = null,
        string? projectDescription = null,
        string? developer = null,
        DateTime? projectSaleLaunchDate = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        // Project Details
        int? unitForSaleCount = null,
        int? numberOfPhase = null,
        string? landOffice = null,
        string? projectType = null,
        string? builtOnTitleDeedNumber = null,
        // Location
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        string? postcode = null,
        string? locationNumber = null,
        string? road = null,
        string? soi = null,
        // Utilities & Facilities
        List<string>? utilities = null,
        string? utilitiesOther = null,
        List<string>? facilities = null,
        string? facilitiesOther = null,
        // Other
        string? remark = null)
    {
        if (CondoProject is null)
        {
            CondoProject = CondoProject.Create(Id);
        }

        CondoProject.Update(
            projectName, projectDescription, developer, projectSaleLaunchDate,
            landAreaRai, landAreaNgan, landAreaWa,
            unitForSaleCount, numberOfPhase, landOffice, projectType, builtOnTitleDeedNumber,
            coordinates, address, postcode, locationNumber, road, soi,
            utilities, utilitiesOther, facilities, facilitiesOther,
            remark);

        return CondoProject;
    }

    public CondoModel AddCondoModel()
    {
        var model = CondoModel.Create(Id);
        _condoModels.Add(model);
        return model;
    }

    public void RemoveCondoModel(Guid modelId)
    {
        var model = _condoModels.FirstOrDefault(m => m.Id == modelId)
                    ?? throw new InvalidOperationException($"Condo model {modelId} not found");
        _condoModels.Remove(model);
    }

    public CondoTower AddCondoTower()
    {
        var tower = CondoTower.Create(Id);
        _condoTowers.Add(tower);
        return tower;
    }

    public void RemoveCondoTower(Guid towerId)
    {
        var tower = _condoTowers.FirstOrDefault(t => t.Id == towerId)
                    ?? throw new InvalidOperationException($"Condo tower {towerId} not found");
        _condoTowers.Remove(tower);
    }

    public CondoUnitUpload ImportCondoUnits(string fileName, Guid? documentId, List<CondoUnit> units)
    {
        var upload = CondoUnitUpload.Create(Id, fileName, documentId);
        _condoUnitUploads.Add(upload);

        // Mark previous uploads as unused
        foreach (var existing in _condoUnitUploads.Where(u => u.IsUsed))
            existing.MarkAsUnused();
        upload.MarkAsUsed();

        // Remove old units and add new ones, linking each unit to the upload
        _condoUnits.Clear();
        foreach (var unit in units)
        {
            unit.SetUploadBatchId(upload.Id);
            _condoUnits.Add(unit);
        }

        // Auto-create placeholder towers and models from unique names
        var towerNames = _condoUnits.Where(u => !string.IsNullOrWhiteSpace(u.TowerName))
            .Select(u => u.TowerName!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in towerNames)
        {
            if (!_condoTowers.Any(t => string.Equals(t.TowerName, name, StringComparison.OrdinalIgnoreCase)))
            {
                _condoTowers.Add(CondoTower.Create(Id, name));
            }
        }

        var modelTypes = _condoUnits.Where(u => !string.IsNullOrWhiteSpace(u.ModelType))
            .Select(u => u.ModelType!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in modelTypes)
        {
            if (!_condoModels.Any(m => string.Equals(m.ModelName, name, StringComparison.OrdinalIgnoreCase)))
            {
                _condoModels.Add(CondoModel.Create(Id, name));
            }
        }

        // Link units to towers and models by FK
        foreach (var unit in _condoUnits)
        {
            if (!string.IsNullOrWhiteSpace(unit.TowerName))
            {
                var tower = _condoTowers.First(t => string.Equals(t.TowerName, unit.TowerName, StringComparison.OrdinalIgnoreCase));
                unit.SetCondoTowerId(tower.Id);
            }
            if (!string.IsNullOrWhiteSpace(unit.ModelType))
            {
                var model = _condoModels.First(m => string.Equals(m.ModelName, unit.ModelType, StringComparison.OrdinalIgnoreCase));
                unit.SetCondoModelId(model.Id);
            }
        }

        return upload;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public CondoPricingAssumption SetCondoPricingAssumption(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? poolViewAdjustment,
        decimal? southAdjustment,
        decimal? otherAdjustment,
        int? floorIncrementEveryXFloor,
        decimal? floorIncrementAmount,
        decimal? forceSalePercentage)
    {
        if (CondoPricingAssumption is null)
        {
            CondoPricingAssumption = CondoPricingAssumption.Create(Id);
        }

        CondoPricingAssumption.Update(
            locationMethod, cornerAdjustment, edgeAdjustment,
            poolViewAdjustment, southAdjustment, otherAdjustment,
            floorIncrementEveryXFloor, floorIncrementAmount, forceSalePercentage);

        return CondoPricingAssumption;
    }

    #endregion

    #region Block Village Management

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public VillageProject SetVillageProject(
        // Project Info
        string? projectName = null,
        string? projectDescription = null,
        string? developer = null,
        DateTime? projectSaleLaunchDate = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        // Project Details
        int? unitForSaleCount = null,
        int? numberOfPhase = null,
        string? landOffice = null,
        string? projectType = null,
        DateTime? licenseExpirationDate = null,
        // Location
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        string? postcode = null,
        string? locationNumber = null,
        string? road = null,
        string? soi = null,
        // Utilities & Facilities
        List<string>? utilities = null,
        string? utilitiesOther = null,
        List<string>? facilities = null,
        string? facilitiesOther = null,
        // Other
        string? remark = null)
    {
        if (VillageProject is null)
        {
            VillageProject = VillageProject.Create(Id);
        }

        VillageProject.Update(
            projectName, projectDescription, developer, projectSaleLaunchDate,
            landAreaRai, landAreaNgan, landAreaWa,
            unitForSaleCount, numberOfPhase, landOffice, projectType, licenseExpirationDate,
            coordinates, address, postcode, locationNumber, road, soi,
            utilities, utilitiesOther, facilities, facilitiesOther,
            remark);

        return VillageProject;
    }

    public VillageProjectLand SetVillageProjectLand(VillageProjectLand land)
    {
        VillageProjectLand = land;
        return VillageProjectLand;
    }

    public VillageModel AddVillageModel()
    {
        var model = VillageModel.Create(Id);
        _villageModels.Add(model);
        return model;
    }

    public void RemoveVillageModel(Guid modelId)
    {
        var model = _villageModels.FirstOrDefault(m => m.Id == modelId)
                    ?? throw new InvalidOperationException($"Village model {modelId} not found");
        _villageModels.Remove(model);
    }

    public VillageUnitUpload ImportVillageUnits(string fileName, Guid? documentId, List<VillageUnit> units)
    {
        var upload = VillageUnitUpload.Create(Id, fileName, documentId);
        _villageUnitUploads.Add(upload);

        // Mark previous uploads as unused
        foreach (var existing in _villageUnitUploads.Where(u => u.IsUsed))
            existing.MarkAsUnused();
        upload.MarkAsUsed();

        // Remove old units and add new ones, linking each unit to the upload
        _villageUnits.Clear();
        foreach (var unit in units)
        {
            unit.SetUploadBatchId(upload.Id);
            _villageUnits.Add(unit);
        }

        // Auto-create placeholder models from unique names
        var modelNames = _villageUnits.Where(u => !string.IsNullOrWhiteSpace(u.ModelName))
            .Select(u => u.ModelName!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in modelNames)
        {
            if (!_villageModels.Any(m => string.Equals(m.ModelName, name, StringComparison.OrdinalIgnoreCase)))
            {
                _villageModels.Add(VillageModel.Create(Id, name));
            }
        }

        // Link units to models by FK
        foreach (var unit in _villageUnits)
        {
            if (!string.IsNullOrWhiteSpace(unit.ModelName))
            {
                var model = _villageModels.First(m => string.Equals(m.ModelName, unit.ModelName, StringComparison.OrdinalIgnoreCase));
                unit.SetVillageModelId(model.Id);
            }
        }

        return upload;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public VillagePricingAssumption SetVillagePricingAssumption(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? nearGardenAdjustment,
        decimal? otherAdjustment,
        decimal? landIncreaseDecreaseRate,
        decimal? forceSalePercentage)
    {
        if (VillagePricingAssumption is null)
        {
            VillagePricingAssumption = VillagePricingAssumption.Create(Id);
        }

        VillagePricingAssumption.Update(
            locationMethod, cornerAdjustment, edgeAdjustment,
            nearGardenAdjustment, otherAdjustment, landIncreaseDecreaseRate, forceSalePercentage);

        return VillagePricingAssumption;
    }

    #endregion

    #region Soft Delete

    public void Delete(Guid deletedBy)
    {
        SoftDelete = SoftDelete.Deleted(deletedBy);
    }

    public void Restore()
    {
        SoftDelete = SoftDelete.NotDeleted();
    }

    #endregion
}