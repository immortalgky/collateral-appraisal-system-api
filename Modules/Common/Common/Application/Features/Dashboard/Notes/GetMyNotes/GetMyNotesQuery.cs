using Shared.CQRS;

namespace Common.Application.Features.Dashboard.Notes.GetMyNotes;

public record GetMyNotesQuery : IQuery<GetMyNotesResponse>;
