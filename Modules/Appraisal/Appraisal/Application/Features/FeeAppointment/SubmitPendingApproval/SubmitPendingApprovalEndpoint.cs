using Shared.Identity;

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
                var companyId = currentUser.CompanyId?.ToString()
                                ?? throw new InvalidOperationException("company_id claim is required for this endpoint");

                var requestedBy = currentUser.Username
                                  ?? throw new InvalidOperationException("User not authenticated");

                await sender.Send(new SubmitPendingApprovalCommand(
                    appraisalId,
                    body.AssignmentId,
                    companyId,
                    requestedBy), ct);

                return Results.Accepted();
            })
            .WithName("SubmitPendingFeeAppointmentApproval")
            .WithTags("FeeAppointment")
            .RequireAuthorization();
    }
}
