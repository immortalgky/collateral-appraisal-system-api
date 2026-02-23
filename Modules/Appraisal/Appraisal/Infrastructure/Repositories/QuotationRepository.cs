using Appraisal.Domain.Quotations;

namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for QuotationRequest aggregate
/// </summary>
public class QuotationRepository(AppraisalDbContext dbContext) : IQuotationRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .Include(q => q.Quotations)
            .ThenInclude(cq => cq.Items)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotationRequest?> GetByNumberAsync(string quotationNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Items)
            .Include(q => q.Invitations)
            .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuotationRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
            .Include(q => q.Items)
            .OrderByDescending(q => q.RequestDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuotationRequest>> GetByStatusAsync(string status,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QuotationRequests
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