namespace Appraisal.Application.Features.Project.CorrectProjectData;

public class CorrectProjectDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        const string description =
            "Admin-only correction of a Completed block appraisal's project. {target} is info, land, models/{id}, " +
            "towers/{id} or unit-prices, and data is exactly the body the block page's PUT for that part accepts " +
            "(unit-prices: { assumption, unitPriceFlags }, then the prices are recalculated). Requires a reason " +
            "and records a field-level audit entry in the same transaction. The appraisal total is never " +
            "recomputed. Deliberately does NOT carry RejectClosedAppraisalWriteFilter: it is the sanctioned way in.";

        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/data-correction/{target}",
                (Guid appraisalId, string target, CorrectProjectDataRequest request, ISender sender,
                        CancellationToken ct) =>
                    Send(sender, new CorrectProjectDataCommand(appraisalId, target, null, request.Reason, request.Data), ct))
            .WithName("CorrectProjectData")
            .Produces<CorrectProjectDataResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Correct a part of a Completed appraisal's project through the block page's write logic")
            .WithDescription(description)
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");

        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/data-correction/{target}/{targetId:guid}",
                (Guid appraisalId, string target, Guid targetId, CorrectProjectDataRequest request, ISender sender,
                        CancellationToken ct) =>
                    Send(sender, new CorrectProjectDataCommand(appraisalId, target, targetId, request.Reason, request.Data), ct))
            .WithName("CorrectProjectRowData")
            .Produces<CorrectProjectDataResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Correct one model or tower of a Completed appraisal's project")
            .WithDescription(description)
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");

        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/data-correction/unit-prices/import",
                async (Guid appraisalId, IFormFile file, [FromForm] string reason, ISender sender,
                    CancellationToken ct) =>
                {
                    if (ProjectExcelUpload.Validate(file) is { } problem) return problem;

                    var import = new ProjectCorrectionImport(
                        ProjectExcelUpload.SafeName(file), await ProjectExcelUpload.ReadAsync(file, ct));
                    return await Send(sender,
                        new CorrectProjectDataCommand(
                            appraisalId, ProjectCorrectionTargets.UnitPriceImport, null, reason, null, import),
                        ct);
                })
            .WithName("CorrectProjectUnitPriceImport")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CorrectProjectDataResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Correct a Completed appraisal's unit prices by importing a price list")
            .WithDescription(
                "Same import as the block page (file + reason as multipart form), without recomputing the " +
                "appraisal total. Records a field-level audit entry in the same transaction.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction")
            .DisableAntiforgery();
    }

    private static async Task<IResult> Send(ISender sender, CorrectProjectDataCommand command, CancellationToken ct) =>
        Results.Ok(await sender.Send(command, ct));
}
