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

    // Core Properties
    public string? AppraisalNumber { get; private set; }
    public Guid RequestId { get; private set; }
    public AppraisalStatus Status { get; private set; } = null!;
    public string AppraisalType { get; private set; } = null!; // Initial, Revaluation, Special
    public string Priority { get; private set; } = null!; // Normal, High

    // SLA Tracking
    public int? SLADays { get; private set; }
    public DateTime? SLADueDate { get; private set; }
    public string? SLAStatus { get; private set; } // OnTrack, AtRisk, Breached
    public int? ActualDaysToComplete { get; private set; }
    public bool? IsWithinSLA { get; private set; }

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
        int? slaDays)
    {
        Id = Guid.NewGuid();
        RequestId = requestId;
        AppraisalType = appraisalType;
        Priority = priority;
        Status = AppraisalStatus.Pending;
        SLADays = slaDays;

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
        int? slaDays = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appraisalType);
        ArgumentException.ThrowIfNullOrWhiteSpace(priority);

        var appraisal = new Appraisal(requestId, appraisalType, priority, slaDays);
        appraisal.AddDomainEvent(new AppraisalCreatedEvent(appraisal));

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
    public AppraisalProperty AddLandProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Land, description);

        var landDetail = LandAppraisalDetail.Create(property.Id);
        property.SetLandDetail(landDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a building property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddBuildingProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Building, description);

        var buildingDetail = BuildingAppraisalDetail.Create(property.Id);
        property.SetBuildingDetail(buildingDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a condo property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddCondoProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Condo, description);

        var condoDetail = CondoAppraisalDetail.Create(property.Id);
        property.SetCondoDetail(condoDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a land and building property with details to this appraisal.
    /// Creates both LandAppraisalDetail and BuildingAppraisalDetail linked to the same AppraisalProperty.
    /// </summary>
    public AppraisalProperty AddLandAndBuildingProperty(string ownerName, string ownershipType,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownershipType);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.LandAndBuilding, description);

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
    public AppraisalProperty AddVehicleProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Vehicle, description);

        var vehicleDetail = VehicleAppraisalDetail.Create(property.Id, owner, Guid.Empty);
        property.SetVehicleDetail(vehicleDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a vessel property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddVesselProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Vessel, description);

        var vesselDetail = VesselAppraisalDetail.Create(property.Id, owner, Guid.Empty);
        property.SetVesselDetail(vesselDetail);

        _properties.Add(property);

        return property;
    }

    /// <summary>
    /// Add a machinery property with detail to this appraisal
    /// </summary>
    public AppraisalProperty AddMachineryProperty(string owner, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var sequenceNumber = _properties.Count + 1;
        var property = AppraisalProperty.Create(Id, sequenceNumber, PropertyType.Machinery, description);

        var machineryDetail = MachineryAppraisalDetail.Create(property.Id, owner, Guid.Empty);
        property.SetMachineryDetail(machineryDetail);

        _properties.Add(property);

        return property;
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

    #endregion

    #region Assignment Management

    /// <summary>
    /// Assign the appraisal to an internal user or external company
    /// </summary>
    public AppraisalAssignment Assign(
        string assignmentMode,
        Guid? assigneeUserId = null,
        Guid? assigneeCompanyId = null,
        string assignmentSource = "Manual",
        Guid? autoRuleId = null)
    {
        ValidateCanAssign();

        var previousAssignment = _assignments.LastOrDefault();
        var reassignmentNumber = previousAssignment?.ReassignmentNumber + 1 ?? 1;

        var assignment = AppraisalAssignment.Create(
            Id,
            assignmentMode,
            assigneeUserId,
            assigneeCompanyId,
            assignmentSource,
            autoRuleId,
            previousAssignment?.Id,
            reassignmentNumber);

        _assignments.Add(assignment);
        UpdateStatus(AppraisalStatus.Assigned);

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
        ActualDaysToComplete = CreatedOn.HasValue ? (DateTime.UtcNow - CreatedOn.Value).Days : null;
        IsWithinSLA = !SLADueDate.HasValue || DateTime.UtcNow <= SLADueDate.Value;

        AddDomainEvent(new AppraisalCompletedEvent(this));
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