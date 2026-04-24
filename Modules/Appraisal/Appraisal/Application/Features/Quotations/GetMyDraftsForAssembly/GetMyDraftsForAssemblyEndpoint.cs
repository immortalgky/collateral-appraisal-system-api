namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

public class GetMyDraftsForAssemblyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/drafts",
                async (
                    string? bankingSegment,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMyDraftsForAssemblyQuery(bankingSegment);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetMyDraftsForAssembly")
            .Produces<GetMyDraftsForAssemblyResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get my Draft quotations for the assembly picker")
            .WithDescription("Returns the calling admin's Draft quotations with appraisal count, " +
                             "preview list (up to 5 appraisal numbers), company count, and due date. " +
                             "Used by the entry modal to let admin pick an existing draft to add an appraisal to.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
