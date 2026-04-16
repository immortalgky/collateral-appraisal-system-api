using Common.Domain.Notes;
using Common.Infrastructure;
using Shared.CQRS;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.Notes.CreateNote;

public class CreateNoteCommandHandler(
    CommonDbContext dbContext,
    ICurrentUserService currentUserService
) : ICommandHandler<CreateNoteCommand, NoteDto>
{
    public async Task<NoteDto> Handle(
        CreateNoteCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new InvalidOperationException("User must be authenticated to create a note.");

        var note = DashboardNote.Create(userId, command.Content);

        dbContext.DashboardNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NoteDto(note.Id, note.Content, note.CreatedAt, note.UpdatedAt);
    }
}
