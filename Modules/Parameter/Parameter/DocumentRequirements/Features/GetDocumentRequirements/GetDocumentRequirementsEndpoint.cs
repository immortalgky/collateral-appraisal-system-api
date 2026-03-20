namespace Parameter.DocumentRequirements.Features.GetDocumentRequirements;

public class GetDocumentRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/document-requirements", async (
                [FromQuery] string? propertyTypeCode,
                [FromQuery] string? purposeCode,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetDocumentRequirementsQuery(propertyTypeCode, purposeCode);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetDocumentRequirementsResponse(result.Requirements));
            })
            .WithName("GetDocumentRequirements")
            .WithSummary("Get all document requirements")
            .Produces<GetDocumentRequirementsResponse>()
            .WithTags("Document Requirements");
    }
}
