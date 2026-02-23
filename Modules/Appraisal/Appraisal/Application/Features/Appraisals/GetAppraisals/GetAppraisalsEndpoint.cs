using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

public class GetAppraisalsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string? status,
                    [FromQuery] string? priority,
                    [FromQuery] string? appraisalType,
                    [FromQuery] Guid? assigneeUserId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetAppraisalsFilterRequest(
                        Status: status,
                        Priority: priority,
                        AppraisalType: appraisalType,
                        AssigneeUserId: assigneeUserId
                    );

                    var query = new GetAppraisalsQuery(pagination, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAppraisalsResponse(result.Result));
                }
            )
            .WithName("GetAppraisals")
            .Produces<GetAppraisalsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all appraisals")
            .WithDescription("Retrieves all appraisals with pagination and optional filtering by status, priority, appraisal type, and assignee.")
            .WithTags("Appraisal");
    }
}
