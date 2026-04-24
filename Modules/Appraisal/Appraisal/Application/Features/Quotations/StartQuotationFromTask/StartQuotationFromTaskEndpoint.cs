namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

public class StartQuotationFromTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/quotations/start-from-task",
                async (
                    StartQuotationFromTaskRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<StartQuotationFromTaskCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result.Adapt<StartQuotationFromTaskResponse>());
                })
            .WithName("StartQuotationFromTask")
            .Produces<StartQuotationFromTaskResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Start quotation from workflow task")
            .WithDescription(
                "Creates a QuotationRequest linked to an appraisal-assignment admin task (IBG segment). " +
                "The admin task remains InProgress for ongoing monitoring.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
