namespace Collateral.Application.Features.CollateralMasters.Documents.ListDocuments;

/// <summary>
/// GET /collateral-masters/{id}/documents?type=&amp;isActive=
/// Returns attached documents for a CollateralMaster.
/// Defaults to active documents (isActive=true) when the parameter is omitted.
/// </summary>
public class ListCollateralDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/{id:guid}/documents",
                async (
                    Guid id,
                    string? type,
                    bool? isActive,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new ListCollateralDocumentsQuery(id, type, isActive);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ListCollateralDocuments")
            .Produces<ListCollateralDocumentsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("List documents attached to a collateral master")
            .WithDescription(
                "Returns documents attached to the specified CollateralMaster. " +
                "Filters: type (DocumentType constant), isActive (default: true). " +
                "Ordered by CreatedAt descending.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
