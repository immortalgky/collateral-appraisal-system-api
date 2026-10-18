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
        app.MapGet("/api/v1/document-checklist", Handle)
            .WithName("Integration_GetDocumentChecklist")
            .WithTags("Integration - Document Requirements")
            .Produces<GetDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("Integration");
    }

    private static async Task<IResult> Handle(
        [FromQuery] string? propertyTypeCodes,
        [FromQuery] string? purposeCode,
        ISender sender,
        CancellationToken ct)
    {
        // Null/whitespace yields an empty list; entries are trimmed before empties are dropped.
        var codes = (propertyTypeCodes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var checklist = await sender.Send(new GetDocumentChecklistQuery(codes, purposeCode), ct);

        return Results.Ok(new GetDocumentChecklistResponse(checklist.ApplicationDocuments, checklist.PropertyTypeGroups));
    }
}
