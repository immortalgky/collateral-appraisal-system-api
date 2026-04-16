using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.Notes.GetMyNotes;

public class GetMyNotesQueryHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : IQueryHandler<GetMyNotesQuery, GetMyNotesResponse>
{
    public async Task<GetMyNotesResponse> Handle(
        GetMyNotesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return new GetMyNotesResponse([]);

        var items = await dbContext.DashboardNotes
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteDto(n.Id, n.Content, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new GetMyNotesResponse(items);
    }
}
