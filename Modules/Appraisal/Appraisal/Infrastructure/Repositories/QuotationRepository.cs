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
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Appraisals)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Items)
            .Where(q => q.Status == "Finalized" &&
                        q.Appraisals.Any(a => a.AppraisalId == appraisalId))
            .OrderByDescending(q => q.RequestDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

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
