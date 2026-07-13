namespace Appraisal.Application.Features.Assignments.SaveAssignmentDraft;

public class SaveAssignmentDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/assignments/draft",
                async (
                    Guid appraisalId,
                    SaveAssignmentDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SaveAssignmentDraftCommand(
                        appraisalId,
                        request.AssignmentType,
                        request.AssigneeUserId,
                        request.AssigneeCompanyId,
                        request.AssignmentMethod ?? "Manual",
                        request.InternalAppraiserId,
                        request.InternalFollowupAssignmentMethod,
                        request.Remark);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("SaveAssignmentDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save assignment draft")
            .WithDescription("Persist the in-progress assignment decision (selections + remark) onto the pending assignment without assigning.")
            .WithTags("Assignment");
    }
}
