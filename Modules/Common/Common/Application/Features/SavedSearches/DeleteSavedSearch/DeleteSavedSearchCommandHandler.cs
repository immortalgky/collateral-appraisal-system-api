using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.SavedSearches.DeleteSavedSearch;

public class DeleteSavedSearchCommandHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : ICommandHandler<DeleteSavedSearchCommand, bool>
{
    public async Task<bool> Handle(
        DeleteSavedSearchCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("User must be authenticated to delete a saved search.");

        // Filter by both Id AND UserId — foreign users receive 404, not 403.
        var savedSearch = await dbContext.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == command.Id && s.UserId == userId, cancellationToken);

        if (savedSearch is null)
            return false;

        dbContext.SavedSearches.Remove(savedSearch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
