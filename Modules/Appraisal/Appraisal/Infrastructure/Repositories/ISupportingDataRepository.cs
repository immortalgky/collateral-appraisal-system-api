namespace Appraisal.Infrastructure.Repositories;

public interface ISupportingDataRepository : IRepository<SupportingData, Guid>
{
    Task<SupportingData?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<SupportingDataDetail?> GetDetailByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Loads the detail including its Images collection (needed for add/remove image handlers).</summary>
    Task<SupportingDataDetail?> GetDetailByIdWithImagesAsync(Guid id, CancellationToken ct = default);

    Task<PaginatedResult<SupportingData>> GetListAsync(
        PaginationRequest pagination,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        DateTime? lastModifiedDateFrom,
        DateTime? lastModifiedDateTo,
        string? supportingNumber,
        string? search,
        string? sortBy,
        string? sortDir,
        CancellationToken ct = default);

    Task<PaginatedResult<SupportingDataDetail>> GetDetailListAsync(
        PaginationRequest pagination,
        Guid supportingId,
        CancellationToken ct = default);
}