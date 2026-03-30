namespace Parameter.DocumentRequirements.Features.GetDocumentChecklist;

public class GetDocumentChecklistEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-checklist", async (
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
            .WithName("GetDocumentChecklist")
            .WithSummary("Get document checklist for specified property types")
            .Produces<GetDocumentChecklistResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}
