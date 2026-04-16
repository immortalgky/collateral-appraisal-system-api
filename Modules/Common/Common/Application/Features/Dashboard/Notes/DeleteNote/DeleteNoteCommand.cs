using Shared.CQRS;

namespace Common.Application.Features.Dashboard.Notes.DeleteNote;

public record DeleteNoteCommand(Guid Id) : ICommand<bool>;
