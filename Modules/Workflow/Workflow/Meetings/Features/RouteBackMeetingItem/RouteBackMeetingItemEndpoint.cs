using FluentValidation;
using Shared.Identity;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.RouteBackMeetingItem;

public class RouteBackMeetingItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/items/{appraisalId:guid}/routeback", async (
                Guid id,
                Guid appraisalId,
                RouteBackMeetingItemRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RouteBackMeetingItemCommand(id, appraisalId, request.Reason), ct);
                return Results.NoContent();
            })
            .WithName("RouteBackMeetingItem")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingSecretary")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record RouteBackMeetingItemRequest(string Reason);

public record RouteBackMeetingItemCommand(Guid MeetingId, Guid AppraisalId, string Reason)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RouteBackMeetingItemCommandValidator : AbstractValidator<RouteBackMeetingItemCommand>
{
    public RouteBackMeetingItemCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

public class RouteBackMeetingItemCommandHandler(
    IMeetingRepository meetingRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RouteBackMeetingItemCommand>
{
    public async Task<Unit> Handle(RouteBackMeetingItemCommand command, CancellationToken ct)
    {
        var actor = currentUserService.Username
            ?? throw new InvalidOperationException("User is not authenticated");

        var meeting = await meetingRepository.GetByIdForDecisionAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.RouteBackItem(command.AppraisalId, actor, command.Reason, dateTimeProvider.ApplicationNow);

        return Unit.Value;
    }
}
