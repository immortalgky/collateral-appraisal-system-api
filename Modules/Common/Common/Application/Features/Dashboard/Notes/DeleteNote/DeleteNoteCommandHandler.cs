using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.Notes.DeleteNote;

public class DeleteNoteCommandHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : ICommandHandler<DeleteNoteCommand, bool>
{
    public async Task<bool> Handle(
        DeleteNoteCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("User must be authenticated to delete a note.");

        // Load and verify ownership — return false (→ 404) if not found or belongs to another user.
        var note = await dbContext.DashboardNotes
            .FirstOrDefaultAsync(n => n.Id == command.Id && n.UserId == userId, cancellationToken);

        if (note is null)
            return false;

        dbContext.DashboardNotes.Remove(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
