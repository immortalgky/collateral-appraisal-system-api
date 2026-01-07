namespace Appraisal.Domain.Quotations;

/// <summary>
/// Repository interface for QuotationRequest aggregate.
/// </summary>
public interface IQuotationRepository
{
    Task<QuotationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuotationRequest?> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<QuotationRequest>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<QuotationRequest>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task AddAsync(QuotationRequest quotationRequest, CancellationToken cancellationToken = default);
    void Update(QuotationRequest quotationRequest);
    void Delete(QuotationRequest quotationRequest);
}