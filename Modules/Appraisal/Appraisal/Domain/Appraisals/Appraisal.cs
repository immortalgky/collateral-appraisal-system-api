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

    // Read-only accessors
    public IReadOnlyList<AppraisalProperty> Properties => _properties.AsReadOnly();
    public IReadOnlyList<PropertyGroup> Groups => _groups.AsReadOnly();
    public IReadOnlyList<AppraisalAssignment> Assignments => _assignments.AsReadOnly();

    public bool HasActiveAssignment => _assignments.Any(a =>
        a.AssignmentStatus == AssignmentStatus.Assigned ||
        a.AssignmentStatus == AssignmentStatus.InProgress);

    // Core Properties
    public string? AppraisalNumber { get; private set; }
    public Guid RequestId { get; private set; }
    public AppraisalStatus Status { get; private set; } = null!;
    public string AppraisalType { get; private set; } = null!; // Initial, Revaluation, Special
    public string Priority { get; private set; } = null!; // Normal, High

    // For ConstructionInspection appraisals — the prior appraisal this CI is following up on.
    // Used by AssignmentFeeService to seed the appraisal fee from the prior engagement's
    // CI fee (CI bypasses the normal tier/quotation pipeline).
    public Guid? PrevAppraisalId { get; private set; }

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

    // Cancellation metadata
    public DateTime? CancelledAt { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancelReason { get; private set; }

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
        DateTime? requestedAt,
        Guid? prevAppraisalId)
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
        PrevAppraisalId = prevAppraisalId;

        if (slaDays.HasValue)
        {
            SLADueDate = DateTime.Now.AddDays(slaDays.Value);
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
        DateTime? requestedAt = null,
        Guid? prevAppraisalId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appraisalType);
        ArgumentException.ThrowIfNullOrWhiteSpace(priority);

        var appraisal = new Appraisal(requestId, appraisalType, priority, slaDays,
            isPma, purpose, channel, bankingSegment, facilityLimit, hasAppraisalBook,
            requestedBy, requestedAt, prevAppraisalId);
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
    /// Add a lease agreement condo property with condo detail, lease agreement, and rental info
    /// </summary>
    public AppraisalProperty AddLeaseAgreementCondoProperty()
    {
        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LeaseAgreementCondo);

        property.SetCondoDetail(CondoAppraisalDetail.Create(property.Id));
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
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!,
                newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementBuilding)
        {
            newProperty.SetBuildingDetail(BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!,
                newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementLandAndBuilding)
        {
            newProperty.SetLandAndBuildingDetails(
                LandAppraisalDetail.CopyFrom(source.LandDetail!, newProperty.Id),
                BuildingAppraisalDetail.CopyFrom(source.BuildingDetail!, newProperty.Id));
            newProperty.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(source.LeaseAgreementDetail!,
                newProperty.Id));
            newProperty.SetRentalInfo(RentalInfo.CopyFrom(source.RentalInfo!, newProperty.Id));
        }
        else if (source.PropertyType == PropertyType.LeaseAgreementCondo)
        {
            newProperty.SetCondoDetail(CondoAppraisalDetail.CopyFrom(source.CondoDetail!, newProperty.Id));
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

    /// <summary>
    /// Creates a Pending assignment without activating it.
    /// Status stays Pending — caller is responsible for promoting it later (e.g. via Assign()).
    /// </summary>
    public AppraisalAssignment CreatePendingAssignment(
        string assignmentType,
        string assignmentMethod,
        string? internalFollowupMethod,
        Guid? quotationRequestId,
        string registeredBy)
    {
        var assignment = AppraisalAssignment.Create(
            Id,
            assignmentType,
            assignmentMethod: assignmentMethod,
            internalFollowupMethod: internalFollowupMethod,
            assignedBy: registeredBy);
        if (quotationRequestId.HasValue)
            assignment.SetQuotationRequestId(quotationRequestId.Value);
        _assignments.Add(assignment);
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

        ValidateCollateralIdentityFields();

        UpdateStatus(AppraisalStatus.Completed);

        // Stamp completion timestamp so downstream consumers (CollateralEngagement, time-series)
        // get an accurate AppraisalDate. Only stamp on first transition; idempotent if re-entered.
        CompletedAt ??= DateTime.UtcNow;

        // Calculate actual days
        ActualDaysToComplete = CreatedAt.HasValue ? (DateTime.Now - CreatedAt.Value).Days : null;
        IsWithinSLA = !SLADueDate.HasValue || DateTime.Now <= SLADueDate.Value;

        AddDomainEvent(new AppraisalCompletedEvent(this));
    }

    /// <summary>
    /// Stamps committee approval evidence and transitions Status to Completed.
    /// Idempotent — no-op if already stamped (CompletedAt set).
    /// In-progress / under-review statuses are still derived from workflow state,
    /// but terminal Completed/Cancelled statuses are persisted on the row so
    /// downstream queries can filter on Status directly without view-level CASE logic.
    /// </summary>
    public void MarkApprovedByCommittee(string committeeCode, DateTime approvedAt)
    {
        if (CompletedAt.HasValue) return; // idempotent guard — safe on pipeline retries
        CompletedAt = approvedAt;
        ApprovedByCommittee = committeeCode;

        // Calculate actual days
        ActualDaysToComplete = CreatedAt.HasValue ? (DateTime.Now - CreatedAt.Value).Days : null;
        IsWithinSLA = !SLADueDate.HasValue || DateTime.Now <= SLADueDate.Value;

        if (Status != AppraisalStatus.Completed)
            UpdateStatus(AppraisalStatus.Completed);

        AddDomainEvent(new AppraisalCompletedEvent(this));
    }

    /// <summary>
    /// Idempotently syncs the appraisal status from a workflow transition.
    /// No-ops if the status is already the target, or if the appraisal is in a terminal state
    /// (Completed / Cancelled) that workflow should not override.
    /// </summary>
    public void SyncStatusFromWorkflow(AppraisalStatus target)
    {
        if (Status == target) return;
        if (Status == AppraisalStatus.Completed || Status == AppraisalStatus.Cancelled) return;

        UpdateStatus(target);
    }

    /// <summary>
    /// Cancel the appraisal, stamping cancellation audit metadata before transitioning status.
    /// </summary>
    public void Cancel(string cancelledBy, DateTime cancelledAt, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelledBy);

        if (Status == AppraisalStatus.Completed)
            throw new InvalidAppraisalStateException("Cannot cancel a completed appraisal");

        if (Status == AppraisalStatus.Cancelled) return; // idempotent — already cancelled

        CancelledBy = cancelledBy;
        CancelledAt = cancelledAt;
        CancelReason = reason;

        UpdateStatus(AppraisalStatus.Cancelled);

        // Cancel any active assignment
        var activeAssignment = _assignments.FirstOrDefault(a =>
            a.AssignmentStatus == AssignmentStatus.Assigned ||
            a.AssignmentStatus == AssignmentStatus.InProgress);

        activeAssignment?.Cancel(reason ?? "Cancelled via workflow");
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

    /// <summary>
    /// Validates that each in-scope property carries the identity fields required for
    /// Collateral master dedup before the appraisal is allowed to reach Completed.
    ///
    /// All four collateral types enforce strict identity field requirements per the v1 spec.
    ///
    ///   Land (L, LB):
    ///     Required — LandTitle: at least one title with non-empty TitleNumber
    ///     Required — Address.LandOffice (treated as LandOfficeCode — controlled-list dropdown value)
    ///     Required — Address.Province
    ///     Required — Address.District (Amphur)
    ///     Required — Address.SubDistrict (Tambon)
    ///
    ///   Condo (U):
    ///     Required — Address.LandOffice (controlled-list dropdown value = LandOfficeCode)
    ///     Required — CondoRegistrationNumber
    ///     Required — BuildingNumber
    ///     Required — FloorNumber
    ///     Required — RoomNumber (= UnitNumber in spec)
    ///     Required — TitleNumber (unit deed number — new column)
    ///     Required — TitleType (unit deed type — new column)
    ///
    ///   Leasehold (LSL, LSB, LS):
    ///     Required — ContractNo (= LeaseRegistrationNo Tor Dor 11 reference)
    ///     Required — LessorName
    ///     Required — LesseeName
    ///     Required — LeaseStartDate
    ///     Required — at least one sibling Land/LB property in the same appraisal
    ///                (UnderlyingMasterId is derived at upsert time by scanning siblings)
    ///
    ///   Machinery (MAC):
    ///     Tier-1 — RegistrationNo present → sufficient on its own
    ///     Tier-2 — when RegistrationNo is absent: all of (SerialNo, Brand, Model, Manufacturer) required
    /// </summary>
    private void ValidateCollateralIdentityFields()
    {
        for (var i = 0; i < _properties.Count; i++)
        {
            var property = _properties[i];
            var propertyNum = i + 1;
            var typeCode = property.PropertyType.Code;

            if (property.PropertyType == PropertyType.Land ||
                property.PropertyType == PropertyType.LandAndBuilding)
            {
                ValidateLandIdentityFields(property, propertyNum);
            }
            else if (property.PropertyType == PropertyType.Condo)
            {
                ValidateCondoIdentityFields(property, propertyNum);
            }
            else if (property.PropertyType.IsLeaseAgreement)
            {
                ValidateLeaseholdIdentityFields(property, propertyNum, _properties);
            }
            else if (property.PropertyType == PropertyType.Machinery)
            {
                ValidateMachineryIdentityFields(property, propertyNum);
            }
            // Building (B), Vehicle (VEH), Vessel (VES) — no collateral master dedup in v1
        }
    }

    private static void ValidateLandIdentityFields(AppraisalProperty property, int propertyNum)
    {
        var missing = new List<string>();
        var detail = property.LandDetail;

        if (detail == null)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Land) is missing land detail.");
        }

        // Validate title: at least one title with a non-empty TitleNumber
        if (detail.Titles.Count == 0 || detail.Titles.All(t => string.IsNullOrWhiteSpace(t.TitleNumber)))
        {
            missing.Add("TitleNumber (at least one title must have a non-empty TitleNumber)");
        }

        // LandOffice stored as controlled-list dropdown value — treated as LandOfficeCode
        if (string.IsNullOrWhiteSpace(detail.Address?.LandOffice))
            missing.Add("LandOffice (LandOfficeCode)");

        if (string.IsNullOrWhiteSpace(detail.Address?.Province))
            missing.Add("Province");

        if (string.IsNullOrWhiteSpace(detail.Address?.District))
            missing.Add("District (Amphur)");

        if (string.IsNullOrWhiteSpace(detail.Address?.SubDistrict))
            missing.Add("SubDistrict (Tambon)");

        if (missing.Count > 0)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Land) is missing required fields: " +
                string.Join(", ", missing) + ".");
        }
    }

    private static void ValidateCondoIdentityFields(AppraisalProperty property, int propertyNum)
    {
        var missing = new List<string>();
        var detail = property.CondoDetail;

        if (detail == null)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Condo) is missing condo detail.");
        }

        // LandOffice stored as controlled-list dropdown value — treated as LandOfficeCode
        if (string.IsNullOrWhiteSpace(detail.Address?.LandOffice))
            missing.Add("LandOffice (LandOfficeCode)");

        if (string.IsNullOrWhiteSpace(detail.CondoRegistrationNumber))
            missing.Add("CondoRegistrationNumber");
        if (string.IsNullOrWhiteSpace(detail.BuildingNumber))
            missing.Add("BuildingNumber");
        if (string.IsNullOrWhiteSpace(detail.FloorNumber))
            missing.Add("FloorNumber");
        if (string.IsNullOrWhiteSpace(detail.RoomNumber))
            missing.Add("RoomNumber (UnitNumber)");

        // Unit deed identifiers required for collateral master dedup
        if (string.IsNullOrWhiteSpace(detail.TitleNumber))
            missing.Add("TitleNumber (unit deed number)");
        if (string.IsNullOrWhiteSpace(detail.TitleType))
            missing.Add("TitleType (unit deed type)");

        if (missing.Count > 0)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Condo) is missing required fields: " +
                string.Join(", ", missing) + ".");
        }
    }

    private static void ValidateLeaseholdIdentityFields(
        AppraisalProperty property,
        int propertyNum,
        IReadOnlyList<AppraisalProperty> allProperties)
    {
        var missing = new List<string>();
        var detail = property.LeaseAgreementDetail;

        if (detail == null)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Leasehold) is missing lease agreement detail.");
        }

        // ContractNo is used as the Tor Dor 11 lease registration number (dedup key)
        if (string.IsNullOrWhiteSpace(detail.ContractNo))
            missing.Add("ContractNo (LeaseRegistrationNo / Tor Dor 11)");

        if (string.IsNullOrWhiteSpace(detail.LessorName))
            missing.Add("LessorName");
        if (string.IsNullOrWhiteSpace(detail.LesseeName))
            missing.Add("LesseeName");
        if (!detail.LeaseStartDate.HasValue)
            missing.Add("LeaseStartDate");

        if (missing.Count > 0)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Leasehold) is missing required fields: " +
                string.Join(", ", missing) + ".");
        }

        // Leasehold requires at least one underlying Land or LandAndBuilding property in the same appraisal
        // so the upsert service can derive UnderlyingMasterId
        var hasUnderlyingProperty = allProperties.Any(p =>
            p.PropertyType == PropertyType.Land ||
            p.PropertyType == PropertyType.LandAndBuilding);

        if (!hasUnderlyingProperty)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Leasehold) requires at least one " +
                "Land or LandAndBuilding property in the same appraisal to resolve the underlying collateral master.");
        }
    }

    private static void ValidateMachineryIdentityFields(AppraisalProperty property, int propertyNum)
    {
        var detail = property.MachineryDetail;

        if (detail == null)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Machinery) is missing machinery detail.");
        }

        // Tier-1: RegistrationNo present → sufficient
        if (!string.IsNullOrWhiteSpace(detail.RegistrationNo))
            return;

        // Tier-2: require SerialNo + Brand + Model + Manufacturer (LocationOwner dropped per spec v1)
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(detail.SerialNo)) missing.Add("SerialNo");
        if (string.IsNullOrWhiteSpace(detail.Brand)) missing.Add("Brand");
        if (string.IsNullOrWhiteSpace(detail.Model)) missing.Add("Model");
        if (string.IsNullOrWhiteSpace(detail.Manufacturer)) missing.Add("Manufacturer");

        if (missing.Count > 0)
        {
            throw new InvalidAppraisalStateException(
                $"Cannot complete appraisal: property #{propertyNum} (Machinery) must have either " +
                "RegistrationNo OR all of (SerialNo, Brand, Model, Manufacturer). Missing: " +
                string.Join(", ", missing) + ".");
        }
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

        var daysRemaining = (SLADueDate.Value - DateTime.Now).Days;
        SLAStatus = daysRemaining switch
        {
            < 0 => "Breached",
            < 2 => "AtRisk",
            _ => "OnTrack"
        };
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