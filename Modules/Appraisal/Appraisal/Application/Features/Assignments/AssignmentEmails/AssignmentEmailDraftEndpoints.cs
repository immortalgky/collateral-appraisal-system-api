namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public record SaveAssignmentEmailDraftRequest(
    List<string>? ExtraTo,
    List<string>? Cc,
    string Subject,
    string BodyHtml,
    List<SaveAssignmentEmailAttachment>? Attachments);

public class AssignmentEmailDraftEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignment-email/draft",
                async (Guid appraisalId, bool? template, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(
                        new GetAssignmentEmailDraftQuery(appraisalId, template ?? false), cancellationToken)))
            .WithName("GetAssignmentEmailDraft")
            .Produces<AssignmentEmailDraftResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get the assignment email draft")
            .WithDescription("The saved email to the assigned company, or the bank template filled from the case when none is saved (or when template=true).")
            .WithTags("Assignment");

        app.MapPut(
                "/appraisals/{appraisalId:guid}/assignment-email/draft",
                async (Guid appraisalId, SaveAssignmentEmailDraftRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    await sender.Send(new SaveAssignmentEmailDraftCommand(
                        appraisalId,
                        request.ExtraTo ?? [],
                        request.Cc ?? [],
                        request.Subject,
                        request.BodyHtml,
                        request.Attachments ?? []), cancellationToken);
                    return Results.NoContent();
                })
            .WithName("SaveAssignmentEmailDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Save the assignment email draft")
            .WithDescription("Saves the appraisal's one email draft to the company, separately from the assignment draft.")
            .WithTags("Assignment");
    }
}
