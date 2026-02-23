using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Quotations.GetQuotations;

public record GetQuotationsResponse(List<QuotationDto> Quotations);

public class GetQuotationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisal-requests/{id:guid}/quotations", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetQuotationsQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(new GetQuotationsResponse(result.Quotations));
        })
        .WithName("GetQuotationsForRequest")
        .WithTags("Integration - Quotations")
        .Produces<GetQuotationsResponse>(StatusCodes.Status200OK);
    }
}
