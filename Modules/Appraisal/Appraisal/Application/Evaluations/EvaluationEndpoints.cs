using Appraisal.Application.Evaluations.Commands;
using Appraisal.Application.Evaluations.Queries;

namespace Appraisal.Application.Evaluations;

/// <summary>
/// Carter endpoint module for Service Quality Evaluations.
/// Groups all evaluation-related routes under /appraisal-evaluations.
/// </summary>
public class EvaluationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ── GET list ────────────────────────────────────────────────────────
        app.MapGet(
                "/appraisal-evaluations",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string? search,
                    string? appraisalNumber,
                    string? customerName,
                    string? appraisalStatus,
                    string? appraiserName,
                    string? appraiserCompanyId,
                    string? appraiserCompanyName,
                    string? evaluationStatus,
                    string? sortBy,
                    string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetEvaluationListQuery(
                        pagination,
                        search,
                        appraisalNumber,
                        customerName,
                        appraisalStatus,
                        appraiserName,
                        appraiserCompanyId,
                        appraiserCompanyName,
                        evaluationStatus,
                        sortBy,
                        sortDir);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result.Items);
                })
            .WithName("GetAppraisalEvaluationList")
            .Produces<PaginatedResult<AppraisalEvaluationListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get appraisal evaluation list")
            .WithDescription("Returns a paginated list of appraisals that have an External assignment, " +
                             "together with their evaluation status and composite score.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();

        // ── GET by appraisal ────────────────────────────────────────────────
        app.MapGet(
                "/appraisal-evaluations/by-appraisal/{appraisalId:guid}",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetEvaluationByAppraisalQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    // "No evaluation yet" is a normal state, not a missing resource — return 200 + null.
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalEvaluationByAppraisal")
            .Produces<AppraisalEvaluationDetail?>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get evaluation by appraisal")
            .WithDescription("Returns the full evaluation detail for a given appraisal, or null if none has been saved yet.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();

        // ── GET evaluation header (for detail page info section) ────────────
        app.MapGet(
                "/appraisal-evaluations/header/{appraisalId:guid}",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetEvaluationHeaderQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetAppraisalEvaluationHeader")
            .Produces<AppraisalEvaluationHeader>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get evaluation header info for an appraisal")
            .WithDescription("Returns the appraisal header fields displayed on the Service Quality Evaluation detail page " +
                             "(customer, report received date, appraiser company, collateral types, inspection dates).")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();

        // ── GET detect delivery time ────────────────────────────────────────
        app.MapGet(
                "/appraisal-evaluations/detect-delivery-time/{appraisalId:guid}",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new DetectDeliveryTimeQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    return result is null
                        ? Results.NotFound(new { message = "No qualifying External assignment with both AssignedAt and SubmittedAt found for this appraisal." })
                        : Results.Ok(result);
                })
            .WithName("DetectEvaluationDeliveryTime")
            .Produces<DetectDeliveryTimeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Auto-detects delivery time as business days between assigned and submitted; suggests a rating (1–5).")
            .WithDescription("Computes first-submission turnaround (SubmittedAt − AssignedAt) of the current External " +
                             "assignment in business hours (excluding weekends, holidays, and lunch), converts to " +
                             "8-hour business days, and returns a suggested rating (1–5). Returns 404 when no " +
                             "qualifying External assignment exists or either timestamp is absent.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();

        // ── POST create ─────────────────────────────────────────────────────
        app.MapPost(
                "/appraisal-evaluations",
                async (
                    CreateEvaluationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new CreateEvaluationCommand(
                        request.AppraisalId,
                        request.EvaluationStatus,
                        request.Criteria1Rating,
                        request.Criteria2Rating,
                        request.Criteria2IsAutoDetected,
                        request.Criteria2DetectedDays,
                        request.Criteria3Rating,
                        request.Criteria4Rating,
                        request.Criteria5Rating,
                        request.AdditionalComments,
                        request.Note);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(new { result.Id });
                })
            .WithName("CreateAppraisalEvaluation")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Create a service quality evaluation")
            .WithDescription("Creates a new Draft evaluation for an appraisal. " +
                             "One evaluation per appraisal is enforced.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();

        // ── PUT update ──────────────────────────────────────────────────────
        app.MapPut(
                "/appraisal-evaluations/{id:guid}",
                async (
                    Guid id,
                    UpdateEvaluationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdateEvaluationCommand(
                        id,
                        request.Criteria1Rating,
                        request.Criteria2Rating,
                        request.Criteria2IsAutoDetected,
                        request.Criteria2DetectedDays,
                        request.Criteria3Rating,
                        request.Criteria4Rating,
                        request.Criteria5Rating,
                        request.AdditionalComments,
                        request.Note,
                        request.EvaluationStatus);

                    await sender.Send(command, cancellationToken);

                    return Results.Ok();
                })
            .WithName("UpdateAppraisalEvaluation")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Update a service quality evaluation")
            .WithDescription("Updates all criteria ratings. " +
                             "Setting EvaluationStatus to 'Completed' stamps EvaluatedAt/EvaluatedBy.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────

public record CreateEvaluationRequest(
    Guid     AppraisalId,
    string   EvaluationStatus,
    int?     Criteria1Rating,
    int?     Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    int?     Criteria3Rating,
    int?     Criteria4Rating,
    int?     Criteria5Rating,
    string?  AdditionalComments,
    string?  Note);

public record UpdateEvaluationRequest(
    int?     Criteria1Rating,
    int?     Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    int?     Criteria3Rating,
    int?     Criteria4Rating,
    int?     Criteria5Rating,
    string?  AdditionalComments,
    string?  Note,
    string   EvaluationStatus);
