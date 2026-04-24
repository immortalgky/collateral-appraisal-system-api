namespace Appraisal.Domain.Quotations;

/// <summary>
/// QuotationRequest Aggregate Root.
/// Manages Request for Quotation (RFQ) process for external appraisal assignments.
///
/// Status vocabulary:
///   Draft → Sent → UnderAdminReview → PendingRmSelection → WinnerTentative → (Negotiating ↔ WinnerTentative) → Finalized
///   Any non-Finalized → Cancelled
///   Legacy path: Sent → Closed (via SelectQuotation, non-IBG)
/// </summary>
public class QuotationRequest : Aggregate<Guid>
{
    private readonly List<QuotationRequestItem> _items = [];
    private readonly List<QuotationInvitation> _invitations = [];
    private readonly List<CompanyQuotation> _quotations = [];
    private readonly List<QuotationRequestAppraisal> _appraisals = [];
    private readonly List<QuotationSharedDocument> _sharedDocuments = [];

    public IReadOnlyList<QuotationRequestItem> Items => _items.AsReadOnly();
    public IReadOnlyList<QuotationInvitation> Invitations => _invitations.AsReadOnly();
    public IReadOnlyList<CompanyQuotation> Quotations => _quotations.AsReadOnly();
    public IReadOnlyList<QuotationRequestAppraisal> Appraisals => _appraisals.AsReadOnly();
    public IReadOnlyList<QuotationSharedDocument> SharedDocuments => _sharedDocuments.AsReadOnly();

    // RFQ Information
    public string? QuotationNumber { get; private set; }
    public DateTime RequestDate { get; private set; }
    public DateTime DueDate { get; private set; }

    // Request Summary
    public int TotalAppraisals { get; private set; }
    public string? RequestDescription { get; private set; }
    public string? SpecialRequirements { get; private set; }

    // Status
    public string Status { get; private set; } = null!;
    public int TotalCompaniesInvited { get; private set; }
    public int TotalQuotationsReceived { get; private set; }

    // Selection (legacy non-IBG path)
    public Guid? SelectedCompanyId { get; private set; }
    public Guid? SelectedQuotationId { get; private set; }
    public DateTime? SelectedAt { get; private set; }
    public string? SelectionReason { get; private set; }

    // Created By
    public Guid RequestedBy { get; private set; }
    public string RequestedByName { get; private set; } = null!;

    // ── RM identity (denormalised from Request.Requestor) ────────────────────
    /// <summary>UserId of the RM who owns the linked request. Set at CreateFromTask time.</summary>
    public Guid? RmUserId { get; private set; }

    /// <summary>Username (employee ID) of the RM who owns the linked request. Set at CreateFromTask time.</summary>
    public string? RmUsername { get; private set; }

    // ── IBG workflow link fields ──────────────────────────────────────────────
    // NOTE: AppraisalId scalar removed in v2. Use Appraisals collection.
    // Retained for existing handlers that still read it via the collection's first element;
    // kept as a computed convenience below.
    public Guid? RequestId { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public Guid? TaskExecutionId { get; private set; }
    public string? BankingSegment { get; private set; }

    /// <summary>
    /// Convenience accessor: returns the first appraisal ID in the collection, or null.
    /// Used by legacy handlers that expect a single appraisalId.
    /// For multi-appraisal quotations prefer iterating <see cref="Appraisals"/>.
    /// </summary>
    public Guid? FirstAppraisalId => _appraisals.Count > 0 ? _appraisals[0].AppraisalId : null;

    // ── Shortlist tracking ────────────────────────────────────────────────────
    public DateTime? SubmissionsClosedAt { get; private set; }
    public DateTime? ShortlistSentToRmAt { get; private set; }
    public Guid? ShortlistSentByAdminId { get; private set; }
    public int TotalShortlisted { get; private set; }

    // ── Tentative winner tracking ─────────────────────────────────────────────
    public Guid? TentativeWinnerQuotationId { get; private set; }
    public DateTime? TentativelySelectedAt { get; private set; }
    public Guid? TentativelySelectedBy { get; private set; }
    public string? TentativelySelectedByRole { get; private set; } // "RM" | "Admin"

    // ── RM negotiation recommendation (set by RM at rm-pick-winner step) ─────
    public bool RmRequestsNegotiation { get; private set; }
    public string? RmNegotiationNote { get; private set; }

    // ── Quotation child workflow ───────────────────────────────────────────────
    /// <summary>WorkflowInstanceId of the spawned quotation-workflow child instance.</summary>
    public Guid? QuotationWorkflowInstanceId { get; private set; }

    // ── Concurrency token ─────────────────────────────────────────────────────
    public byte[]? RowVersion { get; private set; }

    private QuotationRequest()
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Legacy factory — creates a standalone RFQ (non-IBG path).
    /// </summary>
    public static QuotationRequest Create(
        DateTime dueDate,
        Guid requestedBy,
        string requestedByName,
        string? description = null)
    {
        return new QuotationRequest
        {
            Id = Guid.CreateVersion7(),
            RequestDate = DateTime.UtcNow,
            DueDate = dueDate,
            RequestedBy = requestedBy,
            RequestedByName = requestedByName,
            RequestDescription = description,
            Status = "Draft",
            TotalAppraisals = 0,
            TotalCompaniesInvited = 0,
            TotalQuotationsReceived = 0
        };
    }

    /// <summary>
    /// IBG factory — creates an RFQ linked to an appraisal-assignment workflow task.
    /// The initial appraisal is registered via AddAppraisal. Status stays Draft until Send() is called.
    /// </summary>
    public static QuotationRequest CreateFromTask(
        DateTime dueDate,
        Guid requestedBy,
        string requestedByName,
        Guid initialAppraisalId,
        Guid requestId,
        Guid workflowInstanceId,
        Guid? taskExecutionId,
        string bankingSegment,
        string addedBy,
        Guid? rmUserId = null,
        string? rmUsername = null,
        string? description = null,
        string? specialRequirements = null)
    {
        var rfq = new QuotationRequest
        {
            Id = Guid.CreateVersion7(),
            RequestDate = DateTime.UtcNow,
            DueDate = dueDate,
            RequestedBy = requestedBy,
            RequestedByName = requestedByName,
            RequestDescription = description,
            SpecialRequirements = specialRequirements,
            Status = "Draft",
            TotalAppraisals = 0,
            TotalCompaniesInvited = 0,
            TotalQuotationsReceived = 0,
            RequestId = requestId,
            WorkflowInstanceId = workflowInstanceId,
            TaskExecutionId = taskExecutionId,
            BankingSegment = bankingSegment,
            RmUserId = rmUserId,
            RmUsername = rmUsername
        };

        // Register the initial appraisal
        rfq.AddAppraisal(initialAppraisalId, addedBy);

        return rfq;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Appraisal management (v2 multi-appraisal)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an appraisal to this Draft quotation.
    /// Allowed only when Status = Draft.
    /// Duplicate appraisals within the same quotation are rejected (idempotent-safe).
    /// </summary>
    public void AddAppraisal(Guid appraisalId, string addedBy)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot add appraisal to quotation in status '{Status}'. Only Draft quotations accept new appraisals.");

        if (_appraisals.Any(a => a.AppraisalId == appraisalId))
            throw new InvalidOperationException($"Appraisal '{appraisalId}' is already part of this quotation.");

        var entry = QuotationRequestAppraisal.Create(Id, appraisalId, addedBy);
        _appraisals.Add(entry);
        TotalAppraisals = _appraisals.Count;
    }

    /// <summary>
    /// Removes an appraisal from this Draft quotation.
    /// Allowed only when Status = Draft.
    /// If removing the last appraisal, auto-cancels the quotation with reason "Last appraisal removed".
    /// </summary>
    public void RemoveAppraisal(Guid appraisalId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot remove appraisal from quotation in status '{Status}'. Only Draft quotations allow appraisal removal.");

        var entry = _appraisals.FirstOrDefault(a => a.AppraisalId == appraisalId)
                    ?? throw new InvalidOperationException($"Appraisal '{appraisalId}' is not part of this quotation.");

        _appraisals.Remove(entry);
        TotalAppraisals = _appraisals.Count;

        if (_appraisals.Count == 0)
        {
            Status = "Cancelled";
            SelectionReason = "Last appraisal removed";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item and invitation management
    // ─────────────────────────────────────────────────────────────────────────

    public void SetQuotationNumber(string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        QuotationNumber = number;
    }

    public QuotationRequestItem AddItem(
        Guid appraisalId,
        string appraisalNumber,
        string propertyType,
        string? propertyLocation = null,
        decimal? estimatedValue = null)
    {
        var itemNumber = _items.Count + 1;
        var item = QuotationRequestItem.Create(
            Id, appraisalId, itemNumber, appraisalNumber, propertyType, propertyLocation, estimatedValue);
        _items.Add(item);
        // TotalAppraisals is now driven by _appraisals count; items are display-only
        return item;
    }

    public QuotationInvitation InviteCompany(Guid companyId)
    {
        if (_invitations.Any(i => i.CompanyId == companyId))
            throw new InvalidOperationException("Company already invited");

        var invitation = QuotationInvitation.Create(Id, companyId);
        _invitations.Add(invitation);
        TotalCompaniesInvited = _invitations.Count;
        return invitation;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Status transitions
    // ─────────────────────────────────────────────────────────────────────────

    public void Send()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot send RFQ in status '{Status}'");

        if (_appraisals.Count == 0)
            throw new InvalidOperationException("Cannot send RFQ without at least one appraisal");

        if (!_invitations.Any())
            throw new InvalidOperationException("Cannot send RFQ without company invitations");

        Status = "Sent";
    }

    /// <summary>
    /// Closes the RFQ for submissions. Idempotent — safe to call if already UnderAdminReview.
    /// Called by admin or the QuotationAutoCloseService.
    /// </summary>
    public void Close()
    {
        if (Status == "UnderAdminReview")
            return; // idempotent

        if (Status != "Sent")
            throw new InvalidOperationException($"Cannot close RFQ in status '{Status}'");

        Status = "UnderAdminReview";
        SubmissionsClosedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// If the RFQ is still open ("Sent") and every invitation has reached a terminal
    /// state (Submitted / Declined / Expired), auto-close it to UnderAdminReview.
    /// Returns true if the transition occurred; false otherwise. Safe to call repeatedly.
    /// </summary>
    public bool TryAutoCloseAfterAllResponses()
    {
        if (Status != "Sent") return false;
        if (_invitations.Any(i => i.Status == "Pending")) return false;

        Close();
        return true;
    }

    /// <summary>
    /// Admin toggles a company quotation onto the shortlist.
    /// </summary>
    public void MarkShortlisted(Guid companyQuotationId, Guid adminId)
    {
        EnsureStatus("UnderAdminReview", "shortlist a quotation");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);
        if (quotation.IsShortlisted) return; // idempotent

        quotation.MarkShortlisted();
        TotalShortlisted = _quotations.Count(q => q.IsShortlisted);
    }

    /// <summary>
    /// Admin removes a company quotation from the shortlist.
    /// </summary>
    public void UnmarkShortlisted(Guid companyQuotationId, Guid adminId)
    {
        EnsureStatus("UnderAdminReview", "un-shortlist a quotation");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);
        if (!quotation.IsShortlisted) return; // idempotent

        quotation.ClearShortlisted();
        TotalShortlisted = _quotations.Count(q => q.IsShortlisted);
    }

    /// <summary>
    /// Admin sends the shortlist to the RM for selection.
    /// Transitions: UnderAdminReview → PendingRmSelection.
    /// </summary>
    public void SendShortlistToRm(Guid adminId)
    {
        EnsureStatus("UnderAdminReview", "send shortlist to RM");

        if (!_quotations.Any(q => q.IsShortlisted))
            throw new InvalidOperationException("Cannot send shortlist: no quotations are shortlisted");

        Status = "PendingRmSelection";
        ShortlistSentToRmAt = DateTime.UtcNow;
        ShortlistSentByAdminId = adminId;
    }

    /// <summary>
    /// Admin recalls the shortlist from RM, returning to UnderAdminReview.
    /// Fails if a tentative winner has already been picked.
    /// </summary>
    public void RecallShortlist(Guid adminId)
    {
        EnsureStatus("PendingRmSelection", "recall shortlist");

        if (TentativeWinnerQuotationId.HasValue)
            throw new InvalidOperationException("Cannot recall shortlist: a tentative winner has already been picked. Reject the tentative winner first.");

        Status = "UnderAdminReview";
        ShortlistSentToRmAt = null;
        ShortlistSentByAdminId = null;
    }

    /// <summary>
    /// Picks a tentative winner from the shortlisted quotations.
    /// Allowed from PendingRmSelection (by RM or Admin override) or WinnerTentative (Admin override replaces pick).
    /// </summary>
    public void PickTentativeWinner(Guid companyQuotationId, Guid pickedBy, string role)
    {
        if (Status != "PendingRmSelection" && Status != "WinnerTentative")
            throw new InvalidOperationException($"Cannot pick tentative winner in status '{Status}'");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);

        if (!quotation.IsShortlisted)
            throw new InvalidOperationException("Cannot pick a non-shortlisted quotation as tentative winner");

        // Clear the previous tentative winner if replacing
        if (TentativeWinnerQuotationId.HasValue && TentativeWinnerQuotationId != companyQuotationId)
        {
            var previous = _quotations.FirstOrDefault(q => q.Id == TentativeWinnerQuotationId);
            previous?.ClearTentative();
        }

        quotation.MarkTentative();
        TentativeWinnerQuotationId = companyQuotationId;
        TentativelySelectedAt = DateTime.UtcNow;
        TentativelySelectedBy = pickedBy;
        TentativelySelectedByRole = role;
        Status = "WinnerTentative";
    }

    /// <summary>
    /// Admin opens a negotiation round with the tentative winner.
    /// Transitions: WinnerTentative → Negotiating.
    /// </summary>
    public void StartNegotiation(Guid companyQuotationId, decimal proposedPrice, Guid adminUserId, string message)
    {
        EnsureStatus("WinnerTentative", "start negotiation");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);

        if (quotation.Id != TentativeWinnerQuotationId)
            throw new InvalidOperationException("Can only negotiate with the tentative winner");

        quotation.OpenNegotiationRound(proposedPrice, adminUserId, message);
        Status = "Negotiating";
    }

    /// <summary>
    /// Returns from Negotiating back to WinnerTentative when the company responds Accept or Counter.
    /// Called internally by RespondNegotiation flow.
    /// </summary>
    public void EndNegotiation()
    {
        EnsureStatus("Negotiating", "end negotiation");
        Status = "WinnerTentative";
    }

    /// <summary>
    /// Ext-company responds to the latest open negotiation round.
    /// Accept → status returns to WinnerTentative; Counter → stays Negotiating; Reject → quotation Withdrawn, request returns to UnderAdminReview.
    /// </summary>
    public void RespondNegotiation(Guid companyQuotationId, Guid negotiationId, string verb, decimal? counterPrice, string? message, Guid companyUserId)
    {
        EnsureStatus("Negotiating", "respond to negotiation");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);

        if (quotation.Id != TentativeWinnerQuotationId)
            throw new InvalidOperationException("Can only respond for the tentative winner quotation");

        quotation.RespondNegotiation(negotiationId, verb, counterPrice, message, companyUserId);

        if (verb == "Accept")
        {
            Status = "WinnerTentative";
        }
        else if (verb == "Reject")
        {
            // Withdraw tentative winner; return to admin review for re-selection
            TentativeWinnerQuotationId = null;
            TentativelySelectedAt = null;
            TentativelySelectedBy = null;
            TentativelySelectedByRole = null;
            Status = "UnderAdminReview";
        }
        // Counter: stays Negotiating
    }

    /// <summary>
    /// Admin rejects the tentative winner (e.g., after failed negotiation or change of mind).
    /// Withdrawn quotation; returns to UnderAdminReview for re-shortlist / re-send.
    /// </summary>
    public void RejectTentativeWinner(Guid companyQuotationId, string reason)
    {
        if (Status != "WinnerTentative" && Status != "Negotiating")
            throw new InvalidOperationException($"Cannot reject tentative winner in status '{Status}'");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);

        if (quotation.Id != TentativeWinnerQuotationId)
            throw new InvalidOperationException("Can only reject the current tentative winner");

        quotation.ClearTentative();
        quotation.Withdraw();

        TentativeWinnerQuotationId = null;
        TentativelySelectedAt = null;
        TentativelySelectedBy = null;
        TentativelySelectedByRole = null;
        Status = "UnderAdminReview";
    }

    /// <summary>
    /// Admin finalizes the quotation, committing a final price.
    /// Requires WinnerTentative status.
    /// </summary>
    public void Finalize(Guid companyQuotationId, decimal finalPrice, string? reason = null)
    {
        EnsureStatus("WinnerTentative", "finalize quotation");

        var quotation = GetCompanyQuotationOrThrow(companyQuotationId);

        if (quotation.Id != TentativeWinnerQuotationId)
            throw new InvalidOperationException("Can only finalize the current tentative winner");

        quotation.UpdateNegotiatedPrice(finalPrice);
        quotation.MarkAsWinner();

        SelectedCompanyId = quotation.CompanyId;
        SelectedQuotationId = companyQuotationId;
        SelectedAt = DateTime.UtcNow;
        SelectionReason = reason;
        Status = "Finalized";
    }

    /// <summary>
    /// Legacy non-IBG selection. Kept for backward compatibility.
    /// </summary>
    public void SelectQuotation(Guid companyId, Guid quotationId, string? reason = null)
    {
        if (Status != "Sent")
            throw new InvalidOperationException($"Cannot select quotation for RFQ in status '{Status}'");

        SelectedCompanyId = companyId;
        SelectedQuotationId = quotationId;
        SelectedAt = DateTime.UtcNow;
        SelectionReason = reason;
        Status = "Closed";

        var winningQuotation = _quotations.FirstOrDefault(q => q.Id == quotationId);
        winningQuotation?.MarkAsWinner();
    }

    public void Cancel(string? reason = null)
    {
        if (Status == "Finalized")
            throw new InvalidOperationException("Cannot cancel a finalized RFQ");

        if (Status == "Cancelled")
            return; // idempotent

        Status = "Cancelled";
        SelectionReason = reason; // reuse field as cancel reason
    }

    public void SetSpecialRequirements(string requirements)
    {
        SpecialRequirements = requirements;
    }

    /// <summary>
    /// Overwrites the set of documents shared with invited external companies for this quotation.
    /// Only allowed while Status = Draft (before the RFQ is sent).
    /// Full-replace semantics: clear all existing, then add the new selection.
    /// </summary>
    public void SetSharedDocuments(
        IEnumerable<(Guid AppraisalId, Guid DocumentId, string Level)> selections,
        string sharedBy)
    {
        if (Status != "Draft")
            throw new InvalidOperationException(
                $"Cannot set shared documents on a quotation in status '{Status}'. Only Draft quotations can be updated.");

        _sharedDocuments.Clear();

        foreach (var (appraisalId, documentId, level) in selections)
        {
            _sharedDocuments.Add(QuotationSharedDocument.Create(Id, appraisalId, documentId, level, sharedBy));
        }
    }

    /// <summary>
    /// Stores the WorkflowInstanceId of the spawned quotation child workflow.
    /// Called after the consumer successfully starts the child workflow.
    /// </summary>
    public void SetQuotationWorkflowInstanceId(Guid workflowInstanceId)
    {
        QuotationWorkflowInstanceId = workflowInstanceId;
    }

    /// <summary>
    /// Records the RM's negotiation recommendation when picking a tentative winner.
    /// </summary>
    public void SetRmNegotiationRecommendation(bool requestsNegotiation, string? note)
    {
        RmRequestsNegotiation = requestsNegotiation;
        RmNegotiationNote = requestsNegotiation ? note : null;
    }

    internal void AddQuotation(CompanyQuotation quotation)
    {
        _quotations.Add(quotation);
        RecalculateQuotationsReceived();
    }

    /// <summary>
    /// Recalculates TotalQuotationsReceived, excluding Draft and PendingCheckerReview rows.
    /// Call after any status transition that changes a CompanyQuotation to/from Submitted.
    /// </summary>
    internal void RecalculateQuotationsReceived()
    {
        TotalQuotationsReceived = _quotations.Count(q =>
            q.Status != "Draft" && q.Status != "PendingCheckerReview");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureStatus(string expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Cannot {operation}: RFQ is in status '{Status}', expected '{expected}'");
    }

    private CompanyQuotation GetCompanyQuotationOrThrow(Guid companyQuotationId)
    {
        return _quotations.FirstOrDefault(q => q.Id == companyQuotationId)
               ?? throw new InvalidOperationException($"Company quotation '{companyQuotationId}' not found in this RFQ");
    }
}
