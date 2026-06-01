using Shared.Identity;

namespace Appraisal.Application.Features.FeeAppointmentChange;

public record SubmitFeeAppointmentChangeRequest(
    Guid AssignmentId,
    Guid? AppointmentId,
    DateTime? NewAppointmentDate,
    IReadOnlyList<SubmitFeeChangeLineRequest>? FeeLines);

public record SubmitFeeChangeLineRequest(
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount);

public class SubmitFeeAppointmentChangeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/fee-appointment-change", async (
                Guid appraisalId,
                SubmitFeeAppointmentChangeRequest body,
                ISender sender,
                ICurrentUserService currentUser,
                CancellationToken ct) =>
            {
                var companyId = currentUser.CompanyId?.ToString()
                                ?? throw new InvalidOperationException("company_id claim is required for this endpoint");

                var requestedBy = currentUser.Username
                                  ?? throw new InvalidOperationException("User not authenticated");

                var feeLines = (body.FeeLines ?? [])
                    .Select(f => new SubmitFeeChangeLineDto(f.FeeCode, f.FeeDescription, f.FeeAmount))
                    .ToList();

                await sender.Send(new SubmitFeeAppointmentChangeCommand(
                    appraisalId,
                    body.AssignmentId,
                    body.AppointmentId,
                    body.NewAppointmentDate,
                    feeLines,
                    companyId,
                    requestedBy), ct);

                return Results.Accepted();
            })
            .WithName("SubmitFeeAppointmentChange")
            .WithTags("FeeAppointmentChange")
            .RequireAuthorization();
    }
}
