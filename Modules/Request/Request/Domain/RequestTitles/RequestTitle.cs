namespace Request.Domain.RequestTitles;

/// <summary>
/// RequestTitle is an Aggregate Root that manages collateral title information.
/// Base class contains shared fields. Type-specific fields are in subclasses (TPH).
/// </summary>
public abstract class RequestTitle : Aggregate<Guid>
{
    public Guid RequestId { get; protected set; }
    public string? CollateralType { get; protected set; }
    public bool? CollateralStatus { get; protected set; }
    public string? OwnerName { get; protected set; }
    public Address TitleAddress { get; protected set; } = default!;
    public Address DopaAddress { get; protected set; } = default!;
    public string? Notes { get; protected set; }

    private readonly List<TitleDocument> _documents = [];
    public IReadOnlyList<TitleDocument> Documents => _documents.AsReadOnly();

    protected RequestTitle()
    {
        // For EF Core
    }

    protected RequestTitle(RequestTitleData requestTitleData)
    {
        Id = Guid.NewGuid();
        RequestId = requestTitleData.RequestId;
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        OwnerName = requestTitleData.OwnerName;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public virtual void Update(RequestTitleData requestTitleData)
    {
        CollateralStatus = requestTitleData.CollateralStatus;
        OwnerName = requestTitleData.OwnerName;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public virtual void Validate()
    {
        ArgumentNullException.ThrowIfNull(CollateralType);
        ArgumentNullException.ThrowIfNull(TitleAddress);
        ArgumentNullException.ThrowIfNull(DopaAddress);

        TitleAddress.Validate();
        DopaAddress.Validate();
    }

    #region Document Management

    /// <summary>
    /// Adds a new document to this title.
    /// </summary>
    public TitleDocument AddDocument(TitleDocumentData documentData)
    {
        var document = TitleDocument.Create(documentData);
        _documents.Add(document);

        AddDomainEvent(new TitleDocumentAttachedEvent(document));

        return document;
    }

    /// <summary>
    /// Updates an existing document within this title.
    /// </summary>
    public void UpdateDocument(Guid documentId, TitleDocumentData documentData)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this title.")
            .ThrowIfInvalid();

        document!.Update(documentData);
    }

    /// <summary>
    /// Updates an existing document as draft within this title.
    /// </summary>
    public void UpdateDocumentDraft(Guid documentId, TitleDocumentData documentData)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this title.")
            .ThrowIfInvalid();

        document!.UpdateDraft(documentData);
    }

    /// <summary>
    /// Removes a document from this title.
    /// </summary>
    public void RemoveDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this title.")
            .ThrowIfInvalid();

        _documents.Remove(document!);

        AddDomainEvent(new TitleDocumentDetachedEvent(document!.Id, Id, document.DocumentId));
    }

    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    public TitleDocument? GetDocument(Guid documentId)
    {
        return _documents.FirstOrDefault(d => d.Id == documentId);
    }

    /// <summary>
    /// Checks if a document with the given ID exists in this title.
    /// </summary>
    public bool HasDocument(Guid documentId)
    {
        return _documents.Any(d => d.Id == documentId);
    }

    #endregion
}

public static class TitleFactory
{
    public static RequestTitle Create(string code, RequestTitleData data)
    {
        var type = CollateralType.FromCode(code);
        return type.CreateTitle(data);
    }
}

/// <summary>
/// Data transfer object for creating/updating RequestTitle.
/// Contains all possible fields - subclasses use only relevant fields.
/// </summary>
public record RequestTitleData
{
    public Guid RequestId { get; init; }
    public string? CollateralType { get; init; }
    public bool? CollateralStatus { get; init; }
    public string? OwnerName { get; init; }
    public Address TitleAddress { get; init; } = default!;
    public Address DopaAddress { get; init; } = default!;
    public string? Notes { get; init; }

    // Land-related fields
    public TitleDeedInfo TitleDeedInfo { get; init; } = default!;
    public LandLocationInfo LandLocationInfo { get; init; } = default!;
    public LandArea LandArea { get; init; } = default!;

    // Building-related fields
    public BuildingInfo BuildingInfo { get; init; } = default!;

    // Condo-related fields
    public CondoInfo CondoInfo { get; init; } = default!;

    // Vehicle/Vessel/Machine fields
    public VehicleInfo VehicleInfo { get; init; } = default!;
    public VesselInfo VesselInfo { get; init; } = default!;
    public MachineInfo MachineInfo { get; init; } = default!;
}