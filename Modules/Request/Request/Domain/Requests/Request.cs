namespace Request.Domain.Requests;

public class Request : Aggregate<Guid>
{
    public RequestNumber? RequestNumber { get; private set; }
    public RequestStatus Status { get; private set; } = default!;
    public string? Purpose { get; private set; }
    public string? Channel { get; private set; }
    public UserInfo Requestor { get; private set; } = default!;
    public DateTime? RequestedAt { get; private set; }
    public UserInfo Creator { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Priority { get; private set; }
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
        Id = Guid.NewGuid();
        Status = RequestStatus.Draft;
        Priority = "NORMAL";
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
        foreach (var property in _properties) property.Validate();

        // validate documents
        foreach (var document in _documents) document.Validate();
    }

    public void Save(RequestData data)
    {
        ArgumentNullException.ThrowIfNull(data.Purpose);
        ArgumentNullException.ThrowIfNull(data.Channel);
        ArgumentNullException.ThrowIfNull(data.Requestor);
        ArgumentNullException.ThrowIfNull(data.Creator);
        ArgumentNullException.ThrowIfNull(data.Priority);

        Purpose = data.Purpose;
        Channel = data.Channel;
        Requestor = data.Requestor;
        Creator = data.Creator;
        Priority = data.Priority;
        IsPma = data.IsPma;
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

    public void UpdateStatus(RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Status = status;
    }

    /// <summary>
    /// Sets the request number. Called automatically during SaveChanges.
    /// </summary>
    internal void SetRequestNumber(RequestNumber requestNumber)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        RequestNumber = requestNumber;
    }

    public void Delete(string deletedBy, DateTime deletedAt)
    {
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
    /// Submits the request.
    /// </summary>
    public void Submit(DateTime submittedAt)
    {
        RuleCheck.Valid()
            .AddErrorIf(Status != RequestStatus.Draft && Status != RequestStatus.New,
                "Can only submit Draft or New requests.")
            .ThrowIfInvalid();

        UpdateStatus(RequestStatus.Submitted);
        RequestedAt = submittedAt;
        AddDomainEvent(new RequestSubmittedEvent(this));
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
            AddDomainEvent(new DocumentLinkedEvent(Id, data.DocumentId.Value));

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
            AddDomainEvent(new DocumentLinkedEvent(Id, newDocId.Value));
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