using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.SavedSearches.GetSavedSearches;

public class GetSavedSearchesQueryHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : IQueryHandler<GetSavedSearchesQuery, GetSavedSearchesResponse>
{
    public async Task<GetSavedSearchesResponse> Handle(
        GetSavedSearchesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return new GetSavedSearchesResponse([]);

        var dbQuery = dbContext.SavedSearches
            .Where(s => s.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            dbQuery = dbQuery.Where(s => s.EntityType == query.EntityType.Trim().ToLowerInvariant());

        var items = await dbQuery
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SavedSearchDto(
                s.Id,
                s.Name,
                s.EntityType,
                s.FiltersJson,
                s.SortBy,
                s.SortDir,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new GetSavedSearchesResponse(items);
    }
}
