using Common.Domain.SavedSearches;
using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.SavedSearches.CreateSavedSearch;

public class CreateSavedSearchCommandHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : ICommandHandler<CreateSavedSearchCommand, CreateSavedSearchResult>
{
    public async Task<CreateSavedSearchResult> Handle(
        CreateSavedSearchCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("User must be authenticated to save a search.");

        var count = await dbContext.SavedSearches
            .CountAsync(s => s.UserId == userId, cancellationToken);

        if (count >= SavedSearch.MaxPerUser)
            throw new InvalidOperationException(
                $"Cannot exceed {SavedSearch.MaxPerUser} saved searches per user. Delete an existing one first.");

        var savedSearch = SavedSearch.Create(
            userId,
            command.Name,
            command.EntityType,
            command.FiltersJson,
            command.SortBy,
            command.SortDir);

        dbContext.SavedSearches.Add(savedSearch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateSavedSearchResult(savedSearch.Id, savedSearch.Name);
    }
}
