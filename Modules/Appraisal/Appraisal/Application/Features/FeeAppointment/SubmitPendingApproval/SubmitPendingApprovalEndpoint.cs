using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.FeeAppointment.SubmitPendingApproval;

public record SubmitPendingApprovalRequest(Guid AssignmentId);

public class SubmitPendingApprovalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/fee-appointment/submit", async (
                Guid appraisalId,
                SubmitPendingApprovalRequest body,
                ISender sender,
                ICurrentUserService currentUser,
                CancellationToken ct) =>
            {
                var requestedBy = currentUser.Username
                                  ?? throw new InvalidOperationException("User not authenticated");

                var requestSource = currentUser.IsExternal
                    ? FeeApprovalRequestSource.External
                    : FeeApprovalRequestSource.Internal;

                await sender.Send(new SubmitPendingApprovalCommand(
                    appraisalId,
                    body.AssignmentId,
                    requestSource,
                    requestedBy), ct);

                return Results.Accepted();
            })
            .WithName("SubmitPendingFeeAppointmentApproval")
            .WithTags("FeeAppointment")
            .RequireAuthorization();
    }
}
