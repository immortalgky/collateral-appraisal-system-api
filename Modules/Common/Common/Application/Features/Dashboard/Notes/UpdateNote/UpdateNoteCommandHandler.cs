using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.Notes.UpdateNote;

public class UpdateNoteCommandHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : ICommandHandler<UpdateNoteCommand, NoteDto>
{
    public async Task<NoteDto> Handle(
        UpdateNoteCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("User must be authenticated to update a note.");

        // Load and verify ownership — return 404 if note not found or belongs to another user.
        var note = await dbContext.DashboardNotes
            .FirstOrDefaultAsync(n => n.Id == command.Id && n.UserId == userId, cancellationToken);

        if (note is null)
            return null!; // Endpoint maps null → 404 (see UpdateNoteEndpoint)

        note.UpdateContent(command.Content);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NoteDto(note.Id, note.Content, note.CreatedAt, note.UpdatedAt);
    }
}
