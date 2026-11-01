using Microsoft.AspNetCore.Mvc;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public class AssignmentEmailPreviewEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/assignment-email/preview",
                async (Guid appraisalId, [FromQuery(Name = "from")] Guid? fromEmailId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetAssignmentEmailPreviewQuery(appraisalId, fromEmailId), cancellationToken)))
            .WithName("GetAssignmentEmailPreview")
            .Produces<AssignmentEmailPreviewResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Preview the assignment email")
            .WithDescription("The bank template to the assigned company, filled from the case's current assignment. The edited email is sent with POST /assignments. With ?from={emailId}: that stored email of the case instead of the template, its Cc roles expanded the same way (ส่งอีกครั้ง's pre-fill).")
            .WithTags("Assignment");
    }
}
