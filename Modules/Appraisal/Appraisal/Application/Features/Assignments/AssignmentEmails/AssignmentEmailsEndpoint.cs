using Appraisal.Application.Features.Assignments.ResendAssignmentEmail;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>The read-only slip's "อีเมลถึงบริษัท" (#623): history, ดูฉบับที่ส่ง and ส่งอีกครั้ง.</summary>
public class AssignmentEmailsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignment-emails",
                async (Guid appraisalId, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new GetAssignmentEmailHistoryQuery(appraisalId), ct)))
            .WithName("GetAssignmentEmailHistory")
            .Produces<List<AssignmentEmailHistoryItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Assignment email history")
            .WithDescription("Every email to the assigned company of this case, newest first.")
            .WithTags("Assignment");

        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignment-emails/{emailId:guid}",
                async (Guid appraisalId, Guid emailId, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new GetAssignmentEmailSentQuery(appraisalId, emailId), ct)))
            .WithName("GetAssignmentEmailSent")
            .Produces<AssignmentEmailSentResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("View a sent assignment email")
            .WithDescription("The resolved To/Cc, subject, body and attachments that went out, and what the admin composed.")
            .WithTags("Assignment");

        app.MapPost(
                "/appraisals/{appraisalId:guid}/assignment-emails/resend",
                async (Guid appraisalId, AssignmentEmailInput email, ISender sender, CancellationToken ct) =>
                    Results.Ok(await sender.Send(new ResendAssignmentEmailCommand(appraisalId, email), ct)))
            .WithName("ResendAssignmentEmail")
            .Produces<ResendAssignmentEmailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Send the assignment email again")
            .WithDescription("Sends the edited email to the company of the case's current assignment now, as a new email. Refused once the case is closed.")
            .WithTags("Assignment");
    }
}
