using Microsoft.Extensions.Options;
using Workflow.Meetings.Configuration;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.Features.CreateAcknowledgementQueueItem;

/// <summary>
/// Temporary MVP endpoint (Option B) for manually inserting an
/// <see cref="AppraisalAcknowledgementQueueItem"/> until an upstream domain event is available.
/// </summary>
public class CreateAcknowledgementQueueItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/acknowledgement-queue", async (
                CreateAcknowledgementQueueItemRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateAcknowledgementQueueItemCommand(request), ct);
                return Results.Created($"/meetings/acknowledgement-queue/{result.Id}", result);
            })
            .WithName("CreateAcknowledgementQueueItem")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<CreateAcknowledgementQueueItemResponse>(StatusCodes.Status201Created);
    }
}

public record CreateAcknowledgementQueueItemRequest(
    Guid AppraisalId,
    string? AppraisalNo,
    Guid AppraisalDecisionId,
    Guid CommitteeId,
    string CommitteeCode);

public record CreateAcknowledgementQueueItemCommand(CreateAcknowledgementQueueItemRequest Request)
    : ICommand<CreateAcknowledgementQueueItemResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateAcknowledgementQueueItemResponse(Guid Id, string AcknowledgementGroup);

public class CreateAcknowledgementQueueItemCommandHandler(
    WorkflowDbContext dbContext,
    IOptions<AcknowledgementGroupSettings> settings)
    : ICommandHandler<CreateAcknowledgementQueueItemCommand, CreateAcknowledgementQueueItemResponse>
{
    public async Task<CreateAcknowledgementQueueItemResponse> Handle(
        CreateAcknowledgementQueueItemCommand command, CancellationToken ct)
    {
        var groupMap = settings.Value.AcknowledgementGroupByCommitteeCode;

        if (!groupMap.TryGetValue(command.Request.CommitteeCode, out var ackGroup))
            throw new InvalidOperationException(
                $"Unknown committee code '{command.Request.CommitteeCode}'. " +
                "Configure mapping in Workflow:AcknowledgementGroupByCommitteeCode.");

        var item = AppraisalAcknowledgementQueueItem.Create(
            command.Request.AppraisalId,
            command.Request.AppraisalNo,
            command.Request.AppraisalDecisionId,
            command.Request.CommitteeId,
            command.Request.CommitteeCode,
            ackGroup);

        dbContext.AppraisalAcknowledgementQueueItems.Add(item);

        return new CreateAcknowledgementQueueItemResponse(item.Id, item.AcknowledgementGroup);
    }
}
