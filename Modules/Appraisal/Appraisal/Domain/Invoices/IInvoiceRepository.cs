namespace Appraisal.Domain.Invoices;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    void Update(Invoice invoice);
}
