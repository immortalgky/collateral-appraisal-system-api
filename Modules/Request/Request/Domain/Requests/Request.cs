namespace Request.Domain.Requests;

public class Request : Aggregate<Guid>
{
    /// <summary>
    /// Purpose code that always denotes a PMA appraisal. A request with this purpose is
    /// treated as PMA regardless of the user-entered <see cref="IsPma"/> flag.
    /// </summary>
    public const string PmaPurposeCode = "14";

    public RequestNumber? RequestNumber { get; private set; }
    public RequestStatus Status { get; private set; } = default!;
    public string? Purpose { get; private set; }
    public string? Channel { get; private set; }
    public UserInfo Requestor { get; private set; } = default!;
    public DateTime? RequestedAt { get; private set; }
    public UserInfo Creator { get; private set; } = default!;
    public new DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Priority? Priority { get; private set; }
    public bool IsPma { get; private set; }
    public SoftDelete SoftDelete { get; private set; } = default!;
    public RequestDetail? Detail { get; private set; }

    // External system integration
    public string? ExternalCaseKey { get; private set; }
    public string? ExternalSystem { get; private set; }

    private readonly List<RequestCustomer> _customers = [];
    public IReadOnlyList<RequestCustomer> Customers => _customers.AsReadOnly();

    private readonly List<RequestProperty> _properties = [];
    public IReadOnlyList<RequestProperty> Properties => _properties.AsReadOnly();

    private readonly List<RequestDocument> _documents = [];
    public IReadOnlyList<RequestDocument> Documents => _documents.AsReadOnly();

    private Request()
    {
        // For EF Core
    }

    private Request(DateTime createdAt)
    {
        Id = Guid.CreateVersion7();
        Status = RequestStatus.Draft;
        Priority = Priority.Normal;
        CreatedAt = createdAt;
        SoftDelete = SoftDelete.NotDeleted;
    }

    /// <summary>
    /// Creates a new Request with full validation.
    /// </summary>
    public static Request Create(RequestData data)
    {
        var request = new Request(data.CreatedAt);
        request.Save(data);

        return request;
    }

    public void Validate()
    {
        // validate detail
        Detail!.Validate();

        // validate customers
        foreach (var customer in _customers) customer.Validate();

        // validate properties
        foreach (var property in _properties) property.Validate(Detail.LoanDetail?.BankingSegment);

        // validate documents
        foreach (var document in _documents) document.Validate();
    }

    public void Save(RequestData data)
    {
        Purpose = data.Purpose;
        Channel = data.Channel;
        Requestor = data.Requestor;
        Creator = data.Creator;
        Priority = Priority.FromString(data.Priority);
        // PMA is auto-derived: purpose code "14" is always a PMA appraisal, regardless of the
        // user-entered flag. Covers create and all update paths (UpdateRequest /
        // UpdateDraftRequest / UpdateRequestService) since they all route through Save.
        IsPma = data.IsPma || data.Purpose == PmaPurposeCode;
    }

    public void SetDetail(RequestDetail? detail)
    {
        if (Detail == detail) return;

        Detail = detail;
    }

    public void SetCustomers(List<RequestCustomer>? customers)
    {
        if (customers is not null && Customers.SequenceEqual(customers)) return;

        customers?
            .GroupBy(c => new { c.Name })
            .Where(g => g.Count() > 1)
            .ToList()
            .ForEach(g => throw new ArgumentException(
                $"Duplicate customer found: Name='{g.Key.Name}'"));

        _customers.Clear();

        if (customers is not null && customers.Count > 0)
            _customers.AddRange(customers);
    }

    public void SetProperties(List<RequestProperty>? properties)
    {
        if (properties is not null && Properties.SequenceEqual(properties)) return;

        properties?
            .GroupBy(p => new { p.PropertyType, p.BuildingType })
            .Where(g => g.Count() > 1)
            .ToList()
            .ForEach(g => throw new ArgumentException(
                $"Duplicate property found: PropertyType='{g.Key.PropertyType}', BuildingType='{g.Key.BuildingType}'"));

        _properties.Clear();

        if (properties is not null && properties.Count > 0)
            _properties.AddRange(properties);
    }

    private void UpdateStatus(RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Status = status;
    }

    /// <summary>
    /// True once the request has been handed over to the appraisal workflow.
    /// RequestedAt is stamped by <see cref="Submit"/> and never cleared, so it stays a reliable
    /// marker even for rows whose Status was corrupted by an earlier post-submit save.
    /// Deliberately a method, not a property, so EF Core never tries to map it.
    /// </summary>
    public bool HasBeenSubmitted()
    {
        return RequestedAt is not null || (Status != RequestStatus.Draft && Status != RequestStatus.New);
    }

    private void EnsureNotSubmitted(string action)
    {
        RuleCheck.Valid()
            .AddErrorIf(HasBeenSubmitted(), $"Cannot {action} a request that has already been submitted.")
            .ThrowIfInvalid();
    }

    /// <summary>
    /// Promotes the request to "New" (validated, ready to submit) after a full save.
    /// Silently does nothing once the request has been submitted: editing a submitted request --
    /// for example after a route-back to appraisal-initiation -- is legitimate, but it must not
    /// drag the request back into the pre-submission listing.
    /// </summary>
    public void MarkAsNew()
    {
        if (HasBeenSubmitted()) return;

        UpdateStatus(RequestStatus.New);
    }

    /// <summary>
    /// Demotes the request to "Draft". Rejected once the request has been submitted, because a
    /// draft save skips validation and must never be applied to a request already in the workflow.
    /// </summary>
    public void MarkAsDraft()
    {
        EnsureNotSubmitted("save as draft");

        UpdateStatus(RequestStatus.Draft);
    }

    /// <summary>
    /// Sets the request number. Called automatically during SaveChanges.
    /// </summary>
    internal void SetRequestNumber(RequestNumber requestNumber)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        RequestNumber = requestNumber;
    }

    /// <summary>
    /// Soft-deletes the request. Only requests that are still in the intake queue can be deleted --
    /// a submitted request is owned by the appraisal workflow, and deleting it leaves an orphaned
    /// task that can no longer be opened.
    /// </summary>
    public void Delete(string deletedBy, DateTime deletedAt)
    {
        EnsureNotSubmitted("delete");

        SoftDelete = SoftDelete.Delete(deletedBy, deletedAt);
    }

    public void SetExternalReference(string externalCaseKey, string externalSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalCaseKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSystem);

        ExternalCaseKey = externalCaseKey;
        ExternalSystem = externalSystem;
    }

    /// <summary>
    /// Submits the request. <paramref name="groupTag"/> is a transient hint for reappraisal
    /// batches — it is NOT persisted on Request; it flows into <see cref="RequestSubmittedEvent"/>
    /// so downstream handlers can stamp <c>Appraisal.GroupTag</c> when the Appraisal is created.
    /// <paramref name="entrySource"/> is likewise transient — it records HOW the request entered
    /// the system (<c>UI</c> vs <c>API</c>) so the workflow can decide whether the
    /// <c>appraisal-initiation-check</c> task applies. It is distinct from the business
    /// <c>Channel</c> and is NOT persisted on Request.
    /// </summary>
    public void Submit(DateTime submittedAt, string? groupTag = null, string? entrySource = null)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Can only submit Draft or New requests.")
            .ThrowIfInvalid();

        UpdateStatus(RequestStatus.Submitted);
        RequestedAt = submittedAt;
        AddDomainEvent(new RequestSubmittedEvent(this, groupTag, entrySource));
    }

    public void Complete(DateTime completedAt)
    {
        UpdateStatus(RequestStatus.Completed);
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Adds a new document to this request.
    /// </summary>
    public RequestDocument AddDocument(RequestDocumentData data)
    {
        var document = RequestDocument.Create(Id, data);

        _documents.Add(document);

        if (data.DocumentId.HasValue)
            AddDomainEvent(new DocumentLinkedEvent(Id, data.DocumentId.Value, data.DocumentType));

        return document;
    }

    /// <summary>
    /// Updates an existing document within this request.
    /// </summary>
    public void UpdateDocument(Guid documentId, RequestDocumentData data)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this request.")
            .ThrowIfInvalid();

        var (previousDocId, newDocId) = document!.Update(data);

        // Fire appropriate domain events based on document changes
        if (previousDocId.HasValue && newDocId.HasValue)
            AddDomainEvent(new DocumentUpdatedEvent(Id, previousDocId.Value, newDocId.Value));
        else if (!previousDocId.HasValue && newDocId.HasValue)
            AddDomainEvent(new DocumentLinkedEvent(Id, newDocId.Value, data.DocumentType));
        else if (previousDocId.HasValue && !newDocId.HasValue)
            AddDomainEvent(new DocumentUnlinkedEvent(Id, previousDocId.Value));
    }

    /// <summary>
    /// Removes a document from this request.
    /// </summary>
    public void RemoveDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this request.")
            .ThrowIfInvalid();

        _documents.Remove(document!);

        if (document!.DocumentId.HasValue)
            AddDomainEvent(new DocumentUnlinkedEvent(Id, document.DocumentId.Value));
    }

    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    public RequestDocument? GetDocument(Guid documentId)
    {
        return _documents.FirstOrDefault(d => d.Id == documentId);
    }

    /// <summary>
    /// Checks if a document with the given ID exists in this request.
    /// </summary>
    public bool HasDocument(Guid documentId)
    {
        return _documents.Any(d => d.Id == documentId);
    }
}

public record RequestData(
    string? Purpose,
    string? Channel,
    UserInfo Requestor,
    UserInfo Creator,
    DateTime CreatedAt,
    string? Priority,
    bool IsPma
);