namespace Appraisal.Infrastructure.Repositories;

public class SupportingDataRepository(AppraisalDbContext dbContext)
    : BaseRepository<SupportingData, Guid>(dbContext), ISupportingDataRepository
{
    private readonly AppraisalDbContext _db = dbContext;

    public Task<SupportingData?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.SupportingData
              .Include(s => s.Details)
              .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SupportingDataDetail?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.SupportingDataDetails.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);


    public Task<PaginatedResult<SupportingData>> GetListAsync(
    PaginationRequest pagination, string? status, DateTime? importDate,
    string? supportingNumber, CancellationToken cancellationToken = default)
    {
        var query = _db.SupportingData.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status.Code == status);

        if (importDate.HasValue)
            query = query.Where(s => s.ImportDate.Date == importDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(supportingNumber))
            query = query.Where(s =>
                s.SupportingNumber != null &&
                s.SupportingNumber.Value.Contains(supportingNumber));

        return query
            .OrderByDescending(s => s.ImportDate)
            .ToPaginatedResultAsync(pagination, cancellationToken);
    }

    public Task<PaginatedResult<SupportingDataDetail>> GetDetailListAsync(
        PaginationRequest pagination,
        Guid supportingId,
        CancellationToken cancellationToken = default)
    {
        return _db.SupportingDataDetails
            .AsNoTracking()
            .Where(d => d.SupportingDataId == supportingId)
            .OrderByDescending(d => d.InformationDate)
            .ThenBy(d => d.Id)
            .ToPaginatedResultAsync(pagination, cancellationToken);
    }
}