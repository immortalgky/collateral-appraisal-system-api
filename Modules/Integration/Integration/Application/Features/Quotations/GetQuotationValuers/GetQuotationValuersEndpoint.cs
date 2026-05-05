using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Quotations.GetQuotationValuers;

public class GetQuotationValuersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/quotations/{quotationId:guid}/valuers", async (
            Guid quotationId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetQuotationValuersQuery(quotationId), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetQuotationValuers")
        .WithTags("Integration - Quotations")
        .RequireAuthorization("Integration")
        .Produces<GetQuotationValuersResult>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
