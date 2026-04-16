using Shared.CQRS;

namespace Common.Application.Features.Dashboard.Notes.CreateNote;

public record CreateNoteCommand(string Content) : ICommand<NoteDto>;
