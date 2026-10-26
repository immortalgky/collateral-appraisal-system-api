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

    /// <summary>
    /// Reappraisal batch this request was created in (the Initiate group number). Persisted because
    /// staff submit reappraisal requests one by one, later, from the request list — the value has to
    /// survive until <see cref="Submit"/> hands it to <c>Appraisal.GroupTag</c>. NULL otherwise.
    /// </summary>
    public string? GroupTag { get; private set; }

    /// <summary>
    /// The AS400 book (normalised survey number) this periodical reappraisal request reviews. System
    /// field, set by Initiate with <see cref="GroupTag"/> and never edited. Its presence also locks the
    /// request (<see cref="ArePurposeAndPriorLocked"/>): staff cannot change the purpose or repoint or clear
    /// the prior appraisal, and every save keeps the stored prior (id, number, value and date) as it is, so
    /// "is this book already waiting?" / "which book did this submit review?" cannot change. NULL on every
    /// other request.
    /// </summary>
    public string? ReappraisalBookNumber { get; private set; }

    /// <summary>
    /// AS400's collateral id of the COLLATREV row this request reviews — a key on the AS400 side only
    /// (CAS has no link to it). It tells the units of one block project apart: each is a different
    /// collateral under the same book. System field, set by Initiate; it goes out in the AS400 Collateral
    /// Result file (it is NOT the ExternalCaseKey, which drives the LOS webhooks).
    /// </summary>
    public string? ReappraisalCollateralId { get; private set; }

    /// <summary>
    /// AS400's own Auto Update flag ("Y"/"N") for the COLLATREV row, as it was when Initiate ran — later
    /// COLLATREV files keep refreshing a Pending candidate, but the Collateral Result file reports this
    /// frozen value. System field, set by Initiate; NULL when none was stored.
    /// </summary>
    public string? ReappraisalAutoUpdate { get; private set; }

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
        // Locked once submitted, or for a request Initiate created for a COLLATREV book: every caller is covered,
        // not just the ones that remember. Resending the stored value passes. A new request is never locked.
        EnsurePurposeUnchanged(data.Purpose);

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
            .Where(p => !RequestProperty.HasNoBuilding(p.PropertyType)) // several land rows are allowed
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
    /// The purpose and the prior appraisal are locked once the request is submitted (the appraisal is built
    /// from them), and from the start for a request Initiate created for a COLLATREV book
    /// (<see cref="ReappraisalBookNumber"/>): it exists to review that book, so staff cannot repoint it.
    /// Callers use this to keep the stored prior instead of re-resolving it.
    /// </summary>
    public bool ArePurposeAndPriorLocked() => HasBeenSubmitted() || IsPeriodicalReappraisal();

    /// <summary>Created by Initiate for a COLLATREV book (<see cref="ReappraisalBookNumber"/> is set).</summary>
    public bool IsPeriodicalReappraisal() => ReappraisalBookNumber is not null;

    /// <summary>
    /// A locked request's purpose (see <see cref="ArePurposeAndPriorLocked"/>): the appraisal is built from
    /// it at first submit and nothing re-bases it afterwards. Resending the stored value passes. Compared
    /// exactly (ordinal), because <see cref="Save"/> stores the value as given. <see cref="Save"/> calls it itself.
    /// </summary>
    private void EnsurePurposeUnchanged(string? purpose)
    {
        var changed = !string.Equals(purpose, Purpose, StringComparison.Ordinal);
        RuleCheck.Valid()
            .AddErrorIf(ArePurposeAndPriorLocked() && changed,
                HasBeenSubmitted()
                    ? "The purpose of a request that has already been submitted cannot be changed."
                    : "The purpose of a request created for a COLLATREV reappraisal book cannot be changed.")
            .ThrowIfInvalid();
    }

    /// <summary>
    /// A locked request's prior appraisal (see <see cref="ArePurposeAndPriorLocked"/>) is locked for the same
    /// reason as its purpose. A prior in CAS
    /// is identified by its id; a caller that sends only the book number (LOS does) matches it against the
    /// stored number instead, since the number is all it has. A legacy book (no stored id) is identified by
    /// its number alone. Numbers are compared normalised (<see cref="NormalizeBookNumber"/>); an omitted
    /// number on a legacy book is not a change, because the form never sends it back. The caller keeps the
    /// stored prior fields, so nothing here can rewrite them. Call before <see cref="SetDetail"/>.
    /// </summary>
    public void EnsurePriorUnchanged(
        Guid? incomingPrevAppraisalId, string? incomingPrevAppraisalNumber, bool legacyPriorCleared = false)
    {
        var storedId = Detail?.PrevAppraisalId;
        var incomingNumber = NormalizeBookNumber(incomingPrevAppraisalNumber);
        var changed = storedId.HasValue
            ? incomingPrevAppraisalId.HasValue
                ? incomingPrevAppraisalId != storedId
                : incomingNumber is null || incomingNumber != NormalizeBookNumber(Detail?.PrevAppraisalNumber)
            : incomingPrevAppraisalId.HasValue
              || (incomingNumber is not null && incomingNumber != NormalizeBookNumber(Detail?.PrevAppraisalNumber));

        // legacyPriorCleared: the form signals clearing a legacy book by sending back no value and date
        // (it never sends the number), which "changed" cannot see. On an Initiate draft that is a change too.
        RuleCheck.Valid()
            .AddErrorIf(ArePurposeAndPriorLocked() && (changed || (legacyPriorCleared && !HasBeenSubmitted())),
                HasBeenSubmitted()
                    ? "The prior appraisal of a request that has already been submitted cannot be changed."
                    : "The prior appraisal of a request created for a COLLATREV reappraisal book cannot be changed.")
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

    /// <summary>Marks this request as created by Initiate for an AS400 book, in a reappraisal batch.</summary>
    public void MarkAsPeriodicalReappraisal(
        string groupTag, string? bookNumber, string? collateralId, string? autoUpdate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupTag);
        GroupTag = groupTag;
        ReappraisalBookNumber = string.IsNullOrWhiteSpace(bookNumber) ? null : bookNumber.Trim();
        ReappraisalCollateralId = string.IsNullOrWhiteSpace(collateralId) ? null : collateralId.Trim();
        var flag = autoUpdate?.Trim().ToUpperInvariant();
        ReappraisalAutoUpdate = flag is "Y" or "N" ? flag : null;
    }

    /// <summary>
    /// The prior value and date Initiate determined for this periodical reappraisal — for a block-project
    /// unit, the unit's appraised price in the project appraisal and that appraisal's date rather than the
    /// whole project's. Kept through saves while the prior appraisal is unchanged (update handlers).
    /// </summary>
    public void SetReappraisalPriorValue(decimal? value, DateTime? date)
    {
        if (ReappraisalBookNumber is null)
            throw new InvalidOperationException("Only a periodical reappraisal created by Initiate carries its own prior value.");
        if (Detail is null)
            throw new InvalidOperationException("Request has no detail to record a prior value on.");

        Detail = Detail.WithPriorValue(value, date);
    }

    /// <summary>Prefix of an AS400-only prior book number; LOS may send one on create.</summary>
    public const string LegacyPriorBookPrefix = "99A";

    /// <summary>Purpose "07" (a new appraisal): the only purpose that must not reference a prior appraisal.</summary>
    public const string NewAppraisalPurpose = "07";

    /// <summary>
    /// A book number as CAS stores it: spaces trimmed, upper-cased, and the 'B' AS400 puts in front of a block
    /// project's number dropped (B62A00645 is our 62A00645). Same rule as Collateral's As400AppraisalNumber.Normalize
    /// and collateral.vw_HostCollateralLinkKeys.CasAppraisalNumber — keep them identical; the reappraisal flow
    /// stores books in this form, so a legacy test has to read them the same way.
    /// </summary>
    public static string? NormalizeBookNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var s = number.Trim(' ').ToUpperInvariant();
        return s.Length > 1 && s[0] == 'B' ? s[1..] : s;
    }

    public static bool IsLegacyPriorBook(string? number) =>
        NormalizeBookNumber(number)?.StartsWith(LegacyPriorBookPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Records a prior book that exists only in AS400 (legacy "99A…"): it has no PrevAppraisalId to
    /// resolve from, so its number, value and date are stored as sent. Two callers: the periodical
    /// reappraisal consumer (from the AS400 listing) and Integration create, when LOS sends a 99A
    /// number with no id (checked by PriorAppraisalNumberResolver); an Integration resubmit rebuilds
    /// the detail with the same number instead. Never from the UI form. Submit forwards the number to
    /// the new appraisal, where it links the chain and the regulatory origination.
    /// </summary>
    public void SetLegacyPriorBook(string number, decimal? value, DateTime? date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (Detail is null)
            throw new InvalidOperationException("Request has no detail to record a prior book on.");
        if (Detail.PrevAppraisalId.HasValue)
            throw new InvalidOperationException("A prior book outside CAS is never set alongside PrevAppraisalId.");

        Detail = Detail.WithLegacyPriorBook(number.Trim(), value, date);
    }

    public void SetExternalReference(string externalCaseKey, string externalSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalCaseKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSystem);

        ExternalCaseKey = externalCaseKey;
        ExternalSystem = externalSystem;
    }

    /// <summary>
    /// Submits the request. <paramref name="groupTag"/> (falling back to the persisted
    /// <see cref="GroupTag"/>) flows into <see cref="RequestSubmittedEvent"/> so downstream handlers
    /// can stamp <c>Appraisal.GroupTag</c> when the Appraisal is created.
    /// <paramref name="entrySource"/> is transient — it records HOW the request entered
    /// the system (<c>UI</c> vs <c>API</c>) so the workflow can decide whether the
    /// <c>appraisal-initiation-check</c> task applies. It is distinct from the business
    /// <c>Channel</c> and is NOT persisted on Request.
    /// </summary>
    public void Submit(DateTime submittedAt, string? groupTag = null, string? entrySource = null)
    {
        // Guards on HasBeenSubmitted() rather than Status alone, so that a row whose Status was
        // demoted back to New/Draft by the old post-submit-save bug still cannot be submitted a
        // second time. Re-submitting such a row would re-stamp RequestedAt -- the date SLA and the
        // regulatory export read -- and re-publish RequestSubmittedEvent.
        EnsureNotSubmitted("submit");

        UpdateStatus(RequestStatus.Submitted);
        RequestedAt = submittedAt;
        AddDomainEvent(new RequestSubmittedEvent(this, groupTag ?? GroupTag, entrySource));
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