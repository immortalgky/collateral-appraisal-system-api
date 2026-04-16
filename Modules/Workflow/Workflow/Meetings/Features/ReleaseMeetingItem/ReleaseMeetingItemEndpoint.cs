using Shared.Identity;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.ReleaseMeetingItem;

public class ReleaseMeetingItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/items/{appraisalId:guid}/release", async (
                Guid id,
                Guid appraisalId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new ReleaseMeetingItemCommand(id, appraisalId), ct);
                return Results.NoContent();
            })
            .WithName("ReleaseMeetingItem")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingSecretary")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record ReleaseMeetingItemCommand(Guid MeetingId, Guid AppraisalId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class ReleaseMeetingItemCommandHandler(
    IMeetingRepository meetingRepository,
    ICurrentUserService currentUserService)
    : ICommandHandler<ReleaseMeetingItemCommand>
{
    public async Task<Unit> Handle(ReleaseMeetingItemCommand command, CancellationToken ct)
    {
        var actor = currentUserService.Username
            ?? throw new InvalidOperationException("User is not authenticated");

        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.ReleaseItem(command.AppraisalId, actor, DateTime.UtcNow);

        return Unit.Value;
    }
}
