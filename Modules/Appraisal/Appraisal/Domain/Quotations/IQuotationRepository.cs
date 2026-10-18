namespace Appraisal.Domain.Quotations;

/// <summary>
/// Repository interface for QuotationRequest aggregate.
/// </summary>
public interface IQuotationRepository
{
    Task<QuotationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full aggregate including CompanyQuotation negotiations — used by negotiation commands.
    /// </summary>
    Task<QuotationRequest?> GetByIdWithNegotiationsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the aggregate including SharedDocuments collection — used by SetSharedDocuments command.
    /// </summary>
    Task<QuotationRequest?> GetByIdWithSharedDocumentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the aggregate including the Documents collection — used by the quotation-document
    /// </summary>
    Task<QuotationRequest?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only load of just what QuotationAccessPolicy.EnsureCanViewQuotation reads (Invitations, Quotations).
    /// </summary>
    Task<QuotationRequest?> GetForViewAccessCheckAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked load of the quotation with only its appraisal links (no items, invitations or bids).</summary>
    Task<QuotationRequest?> GetWithAppraisalsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<QuotationRequest?> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// The latest finalized QuotationRequest of the given appraisal (by SelectedAt), only if
    /// <paramref name="winnerCompanyId"/> won it; null otherwise. An older RFQ the company won is not returned once
    /// a later round chose another winner, so a fee is never taken from a superseded quotation.
    /// v2: searches via QuotationRequestAppraisals join table.
    /// </summary>
    Task<QuotationRequest?> GetFinalizedByAppraisalIdAsync(
        Guid appraisalId, Guid winnerCompanyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap existence form of <see cref="GetFinalizedByAppraisalIdAsync"/>: both judge the same latest finalized
    /// RFQ of the appraisal (and only if the company won it). This one additionally requires the winner to have a
    /// usable price for the appraisal (the fee consumer would otherwise fail).
    /// </summary>
    Task<bool> HasFinalizedWonByAsync(Guid appraisalId, Guid winnerCompanyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the given appraisal is already part of a non-terminal QuotationRequest
    /// (any status other than Finalized or Cancelled).
    /// Pass <paramref name="excludeQuotationRequestId"/> to exclude the current quotation when
    /// validating an add-appraisal operation.
    /// </summary>
    Task<bool> HasActiveQuotationForAppraisalAsync(
        Guid appraisalId,
        Guid? excludeQuotationRequestId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<QuotationRequest>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<QuotationRequest>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task AddAsync(QuotationRequest quotationRequest, CancellationToken cancellationToken = default);
    void Update(QuotationRequest quotationRequest);
    void Delete(QuotationRequest quotationRequest);
}
