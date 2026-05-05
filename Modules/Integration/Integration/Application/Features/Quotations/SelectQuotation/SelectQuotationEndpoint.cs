using Appraisal.Application.Features.Quotations.SelectQuotationFromIntegration;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Quotations.SelectQuotation;

public class SelectQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/quotations/{quotationId:guid}/select", async (
            Guid quotationId,
            SelectQuotationRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.CompanyQuotationId, out var companyQuotationId))
                return Results.BadRequest(new SelectQuotationResponse("ERROR", "companyQuotationId must be a valid GUID"));

            if (string.IsNullOrWhiteSpace(request.RmUsername))
                return Results.BadRequest(new SelectQuotationResponse("ERROR", "rmUsername is required"));

            var command = new SelectQuotationFromIntegrationCommand(
                QuotationRequestId: quotationId,
                CompanyQuotationId: companyQuotationId,
                RmUsername: request.RmUsername,
                RequestNegotiation: request.RequestNegotiation,
                NegotiationNote: request.NegotiationNote);

            await sender.Send(command, cancellationToken);

            return Results.Ok(new SelectQuotationResponse("OK", "Quotation selection submitted successfully"));
        })
        .WithName("SelectQuotation")
        .WithTags("Integration - Quotations")
        .RequireAuthorization("Integration")
        .Produces<SelectQuotationResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
