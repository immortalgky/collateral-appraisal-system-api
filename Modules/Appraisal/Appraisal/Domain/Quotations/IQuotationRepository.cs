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

    Task<QuotationRequest?> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the Finalized QuotationRequest linked to the given appraisal, if any.
    /// v2: searches via QuotationRequestAppraisals join table.
    /// Returns the most recently created one if multiple exist.
    /// </summary>
    Task<QuotationRequest?> GetFinalizedByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);

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
