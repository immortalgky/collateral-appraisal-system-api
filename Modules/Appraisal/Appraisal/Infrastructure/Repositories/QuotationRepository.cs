using Appraisal.Domain.Quotations;

namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for QuotationRequest aggregate.
/// v2: includes QuotationRequestAppraisals join collection in all queries.
/// </summary>
public class QuotationRepository(AppraisalDbContext dbContext) : IQuotationRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Items)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByIdWithNegotiationsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .Include(q => q.SharedDocuments)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Items)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Negotiations)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByIdWithSharedDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .Include(q => q.Quotations)
            .Include(q => q.SharedDocuments)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Documents)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetForViewAccessCheckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .AsNoTracking()
            .Include(q => q.Invitations)
            .Include(q => q.Quotations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetWithAppraisalsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByNumberAsync(string quotationNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// v2: searches via the QuotationRequestAppraisals join table, not the dropped AppraisalId column.
    /// </remarks>
    public async Task<QuotationRequest?> GetFinalizedByAppraisalIdAsync(Guid appraisalId,
        Guid winnerCompanyId, CancellationToken cancellationToken = default)
    {
        var latest = await LatestFinalized(appraisalId)
            .Include(q => q.Appraisals)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Items)
            .AsSplitQuery() // sibling collections would otherwise multiply into a cartesian product
            .FirstOrDefaultAsync(cancellationToken);

        // The current round decides: if the latest finalized RFQ was won by someone else, an older one this
        // company won must not be billed.
        return latest?.SelectedCompanyId == winnerCompanyId ? latest : null;
    }

    /// <inheritdoc />
    public async Task<bool> HasFinalizedWonByAsync(Guid appraisalId, Guid winnerCompanyId,
        CancellationToken cancellationToken = default)
    {
        // Judges the SAME quotation the fee consumer will use (the latest finalized one, only if this company won
        // it), and also requires the winner to have a usable price for THIS appraisal by the consumer's rule
        // (CompanyAssignedIntegrationEventHandler.ResolveFeeSourceAsync): the consumer fails after the workflow has
        // moved on, so an unpriced winner must be refused here. Every item for the appraisal must be priced, as in
        // QuotationAutoAssignStateReader.GetPricedAppraisalIdsAsync (the consumer takes an arbitrary first item).
        return await LatestFinalized(appraisalId)
            .Select(q => q.SelectedCompanyId == winnerCompanyId &&
                         q.Quotations.Any(c =>
                             c.IsWinner && c.CompanyId == winnerCompanyId &&
                             c.Items.Any(i => i.AppraisalId == appraisalId) &&
                             !c.Items.Any(i => i.AppraisalId == appraisalId &&
                                               (i.FeeAmount > 0m
                                                   ? i.FeeAmount - i.Discount - (i.NegotiatedDiscount ?? 0m)
                                                   : i.QuotedPrice) <= 0m)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // The latest finalized quotation of the appraisal, newest FINALIZED first (SelectedAt), not the newest draft.
    // Deliberately NOT filtered by company: the current round decides who is billed. Shared by the fee lookup and
    // the assign guard so they cannot pick different quotations. The ordering ends in Id, so it is unique.
    private IOrderedQueryable<QuotationRequest> LatestFinalized(Guid appraisalId) =>
        _dbContext.QuotationRequests
            .Where(q => q.Status == "Finalized" && q.Appraisals.Any(a => a.AppraisalId == appraisalId))
            .OrderByDescending(q => q.SelectedAt)
            .ThenByDescending(q => q.RequestDate)
            .ThenBy(q => q.Id);

    /// <summary>
    /// Checks whether the given appraisal is already part of any non-terminal QuotationRequest.
    /// Used to enforce the one-active-quotation-per-appraisal invariant.
    /// Non-terminal = any status except Finalized and Cancelled.
    /// </summary>
    public async Task<bool> HasActiveQuotationForAppraisalAsync(
        Guid appraisalId,
        Guid? excludeQuotationRequestId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.QuotationRequestAppraisals
            .Where(a => a.AppraisalId == appraisalId);

        if (excludeQuotationRequestId.HasValue)
            query = query.Where(a => a.QuotationRequestId != excludeQuotationRequestId.Value);

        return await query
            .Join(
                _dbContext.QuotationRequests,
                a => a.QuotationRequestId,
                q => q.Id,
                (a, q) => q.Status)
            .AnyAsync(status => status != "Finalized" && status != "Cancelled", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuotationRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .OrderByDescending(q => q.RequestDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuotationRequest>> GetByStatusAsync(string status,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .Where(q => q.Status == status)
            .OrderByDescending(q => q.RequestDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(QuotationRequest quotationRequest, CancellationToken cancellationToken = default)
    {
        await _dbContext.QuotationRequests.AddAsync(quotationRequest, cancellationToken);
    }

    /// <inheritdoc />
    public void Update(QuotationRequest quotationRequest)
    {
        _dbContext.QuotationRequests.Update(quotationRequest);
    }

    /// <inheritdoc />
    public void Delete(QuotationRequest quotationRequest)
    {
        _dbContext.QuotationRequests.Remove(quotationRequest);
    }
}
