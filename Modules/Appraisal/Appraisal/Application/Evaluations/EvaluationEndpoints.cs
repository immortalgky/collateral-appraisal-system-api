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
                    string? appraisalNumber,
                    string? customerName,
                    string? appraisalStatus,
                    string? appraiserName,
                    string? evaluationStatus,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetEvaluationListQuery(
                        pagination,
                        appraisalNumber,
                        customerName,
                        appraisalStatus,
                        appraiserName,
                        evaluationStatus);

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

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetAppraisalEvaluationByAppraisal")
            .Produces<AppraisalEvaluationDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get evaluation by appraisal")
            .WithDescription("Returns the full evaluation detail for a given appraisal, or 404 if none exists.")
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
                        ? Results.NotFound(new { message = "No ext-* activity executions found for this appraisal." })
                        : Results.Ok(result);
                })
            .WithName("DetectEvaluationDeliveryTime")
            .Produces<DetectDeliveryTimeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Detect delivery time for criterion 2")
            .WithDescription("Calculates the total elapsed days across ext-* workflow activities and " +
                             "suggests a rating (1–4) for the delivery-time criterion.")
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
                        request.Criteria1Description,
                        request.Criteria2Rating,
                        request.Criteria2IsAutoDetected,
                        request.Criteria2DetectedDays,
                        request.Criteria2Description,
                        request.Criteria3Rating,
                        request.Criteria3Description,
                        request.Criteria4Rating,
                        request.Criteria4Description,
                        request.Criteria5Rating,
                        request.Criteria5Description,
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
                        request.Criteria1Description,
                        request.Criteria2Rating,
                        request.Criteria2IsAutoDetected,
                        request.Criteria2DetectedDays,
                        request.Criteria2Description,
                        request.Criteria3Rating,
                        request.Criteria3Description,
                        request.Criteria4Rating,
                        request.Criteria4Description,
                        request.Criteria5Rating,
                        request.Criteria5Description,
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
            .WithDescription("Updates all criteria ratings and descriptions. " +
                             "Setting EvaluationStatus to 'Completed' stamps EvaluatedAt/EvaluatedBy.")
            .WithTags("AppraisalEvaluation")
            .RequireAuthorization();
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────

public record CreateEvaluationRequest(
    Guid     AppraisalId,
    string   EvaluationStatus,
    int      Criteria1Rating,
    string?  Criteria1Description,
    int      Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    string?  Criteria2Description,
    int      Criteria3Rating,
    string?  Criteria3Description,
    int      Criteria4Rating,
    string?  Criteria4Description,
    int      Criteria5Rating,
    string?  Criteria5Description,
    string?  AdditionalComments,
    string?  Note);

public record UpdateEvaluationRequest(
    int      Criteria1Rating,
    string?  Criteria1Description,
    int      Criteria2Rating,
    bool     Criteria2IsAutoDetected,
    decimal? Criteria2DetectedDays,
    string?  Criteria2Description,
    int      Criteria3Rating,
    string?  Criteria3Description,
    int      Criteria4Rating,
    string?  Criteria4Description,
    int      Criteria5Rating,
    string?  Criteria5Description,
    string?  AdditionalComments,
    string?  Note,
    string   EvaluationStatus);
