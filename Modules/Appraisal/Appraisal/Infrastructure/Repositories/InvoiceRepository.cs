using Appraisal.Domain.Invoices;

namespace Appraisal.Infrastructure.Repositories;

public class InvoiceRepository(AppraisalDbContext dbContext) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Invoice?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<List<Invoice>> GetByIdsWithItemsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => await dbContext.Invoices
            .Include(i => i.Items)
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(ct);

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
        => await dbContext.Invoices.AddAsync(invoice, ct);

    public void Update(Invoice invoice)
        => dbContext.Invoices.Update(invoice);

    public void Remove(Invoice invoice)
        => dbContext.Invoices.Remove(invoice);
}
