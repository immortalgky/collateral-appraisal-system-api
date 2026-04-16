using Shared.CQRS;

namespace Common.Application.Features.Dashboard.Notes.UpdateNote;

public record UpdateNoteCommand(Guid Id, string Content) : ICommand<NoteDto>;
