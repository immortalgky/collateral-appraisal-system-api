using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Parameter.DocumentRequirements.Features.GetDocumentChecklist;

namespace Integration.Application.Features.DocumentRequirements.GetDocumentChecklist;

// Mirrors legacy GET /document-checklist for LOS (Parameter/DocumentRequirements/Features/GetDocumentChecklist/GetDocumentChecklistEndpoint.cs).
public class GetDocumentChecklistIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/document-checklist", async (
                [FromQuery] string? propertyTypeCodes,
                [FromQuery] string? purposeCode,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var codes = string.IsNullOrWhiteSpace(propertyTypeCodes)
                    ? []
                    : propertyTypeCodes
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();

                var query = new GetDocumentChecklistQuery(codes, purposeCode);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetDocumentChecklistResponse(
                    result.ApplicationDocuments,
                    result.PropertyTypeGroups));
            })
            .WithName("Integration_GetDocumentChecklist")
            .WithTags("Integration - Document Requirements")
            .Produces<GetDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("Integration");
    }
}
