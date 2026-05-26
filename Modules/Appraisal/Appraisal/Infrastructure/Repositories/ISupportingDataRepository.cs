namespace Appraisal.Infrastructure.Repositories;

public interface ISupportingDataRepository : IRepository<SupportingData, Guid>
{
    Task<SupportingData?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<SupportingDataDetail?> GetDetailByIdAsync(Guid id, CancellationToken ct = default);

    Task<PaginatedResult<SupportingData>> GetListAsync(
        PaginationRequest pagination,
        string? status,
        DateTime? importDate,
        string? supportingNumber,
        CancellationToken ct = default);

    Task<PaginatedResult<SupportingDataDetail>> GetDetailListAsync(
        PaginationRequest pagination,
        Guid supportingId,
        CancellationToken ct = default);
}