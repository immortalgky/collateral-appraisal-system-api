using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;
using Workflow.FeeAppointmentApprovals.Application.Queries;

namespace Workflow.FeeAppointmentApprovals.Endpoints;

public record ResolveFeeAppointmentApprovalRequest(
    string AppointmentDecision,
    string? AppointmentReason,
    string FeeDecision,
    string? FeeReason);

public class FeeAppointmentApprovalEndpoints : ICarterModule
{
    private const string Base = "/workflows/fee-appointment-approvals";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet($"{Base}/by-workflow-instance/{{workflowInstanceId:guid}}", async (
                Guid workflowInstanceId, ISender sender, CancellationToken ct) =>
            {
                var dto = await sender.Send(
                    new GetFeeAppointmentApprovalByWorkflowInstanceQuery(workflowInstanceId), ct);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .WithName("GetFeeAppointmentApprovalByWorkflowInstance")
            .WithTags("FeeAppointmentApprovals")
            .RequireAuthorization();

        app.MapPost($"{Base}/{{id:guid}}/resolve", async (
                Guid id,
                ResolveFeeAppointmentApprovalRequest body,
                ISender sender,
                ICurrentUserService currentUser,
                CancellationToken ct) =>
            {
                var actor = currentUser.Username
                            ?? throw new InvalidOperationException("User not authenticated");

                await sender.Send(new ResolveFeeAppointmentApprovalCommand(
                    id,
                    actor,
                    new FeeApprovalComponentDecision(body.AppointmentDecision, body.AppointmentReason),
                    new FeeApprovalComponentDecision(body.FeeDecision, body.FeeReason)), ct);

                return Results.NoContent();
            })
            .WithName("ResolveFeeAppointmentApproval")
            .WithTags("FeeAppointmentApprovals")
            .RequireAuthorization();
    }
}
