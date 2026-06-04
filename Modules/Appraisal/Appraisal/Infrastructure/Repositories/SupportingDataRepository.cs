namespace Appraisal.Infrastructure.Repositories;

public class SupportingDataRepository(AppraisalDbContext dbContext, ICurrentUserService currentUserService)
    : BaseRepository<SupportingData, Guid>(dbContext), ISupportingDataRepository
{
    private readonly AppraisalDbContext _db = dbContext;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public Task<SupportingData?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.SupportingData
              .Include(s => s.Details)
              .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<SupportingDataDetail?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.SupportingDataDetails.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<(SupportingDataDetail?, SupportingStatus?)> GetDetailByIdWithImagesAsync(
    Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await _db.SupportingDataDetails
            .Include(d => d.Images)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (detail is null) return (null, null);

        var status = await _db.SupportingData
            .Where(s => s.Id == detail.SupportingDataId)
            .Select(s => s.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return (detail, status);
    }


    public Task<PaginatedResult<SupportingData>> GetListAsync(
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
        CancellationToken cancellationToken = default
    )
    {
        var query = _db.SupportingData.AsNoTracking().AsQueryable();

        if (currentUserService.CompanyId is not null)
        {
            query = query.Where(s => s.AppraisalCompanyId == currentUserService.CompanyId);
        }
        else
        {
            query = query.Where(s => s.AppraisalCompanyId == null);
        }

        // If user doesn't have edit permission. they should not see the supporting data in Draft or RoutedBack status.
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"))
        {
            query = query.Where(s => s.Status != SupportingStatus.Draft && s.Status != SupportingStatus.RoutedBack);
        }
        else
        {
            query = query.Where(s => s.Status != SupportingStatus.Pending);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var targetStatus = SupportingStatus.FromString(status);
            query = query.Where(s => s.Status == targetStatus);
        }

        if (dateFrom.HasValue)
            query = query.Where(s => s.CreatedAt >= dateFrom.Value.Date);

        if (dateTo.HasValue)
            query = query.Where(s => s.CreatedAt < dateTo.Value.Date.AddDays(1));

        if (lastModifiedDateFrom.HasValue)
            query = query.Where(s => s.UpdatedAt >= lastModifiedDateFrom.Value.Date);

        if (lastModifiedDateTo.HasValue)
            query = query.Where(s => s.UpdatedAt < lastModifiedDateTo.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(supportingNumber))
            query = query.Where(s =>
                s.SupportingNumber != null &&
                s.SupportingNumber.Value.Contains(supportingNumber));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s =>
                s.SupportingNumber != null &&
                s.SupportingNumber.Value.Contains(search));

        var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy switch
        {
            "supportingNumber" => isDesc
                ? query.OrderByDescending(s => s.SupportingNumber!.Value)
                : query.OrderBy(s => s.SupportingNumber!.Value),
            "status" => isDesc
                ? query.OrderByDescending(s => s.Status)
                : query.OrderBy(s => s.Status),
            "createdDate" => isDesc
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
            _ => query.OrderBy(s => s.CreatedAt),
        };

        return query.ToPaginatedResultAsync(pagination, cancellationToken);
    }

    public Task<PaginatedResult<SupportingDataDetail>> GetDetailListAsync(
        PaginationRequest pagination,
        Guid supportingId,
        CancellationToken cancellationToken = default)
    {
        return _db.SupportingDataDetails
            .AsNoTracking()
            .Where(d => d.SupportingDataId == supportingId)
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .ToPaginatedResultAsync(pagination, cancellationToken);
    }
}