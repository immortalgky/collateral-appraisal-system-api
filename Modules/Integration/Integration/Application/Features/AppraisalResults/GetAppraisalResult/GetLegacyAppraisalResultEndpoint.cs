using System.Text.Json;
using System.Text.Json.Serialization;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

// Legacy (AS400) request body. AssetTypeId (RequestTitle collateral type) is accepted but ignored;
// the collateral is located by ApplicationNo (= AppraisalNumber) + Filter1/Filter2.
public record LegacyAppraisalResultRequest(
    string ApplicationNo,
    int AssetTypeId,
    string? Filter1,
    string? Filter2);

public class GetLegacyAppraisalResultEndpoint : ICarterModule
{
    // The legacy (AS400) contract uses PascalCase property names and keeps nulls, unlike the API's
    // global camelCase / omit-null policy. Serialize this endpoint's response with these options only.
    private static readonly JsonSerializerOptions LegacyJsonOptions = new()
    {
        PropertyNamingPolicy = null, // PascalCase — use the DTO property names as-is
        DefaultIgnoreCondition = JsonIgnoreCondition.Never, // keep nulls (e.g. Decorate: null)
    };

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/appraisals/result", async (
            LegacyAppraisalResultRequest body,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetLegacyAppraisalResultQuery(
                    body.ApplicationNo, body.AssetTypeId, body.Filter1, body.Filter2),
                cancellationToken);

            // Always 200: the { ResultCode, ResultValue } envelope carries success (1) / not-found (0).
            // PascalCase, nulls kept — per the legacy contract (overrides the global camelCase policy).
            return Results.Json(result, LegacyJsonOptions, statusCode: StatusCodes.Status200OK);
        })
        .WithName("GetLegacyAppraisalResult")
        .WithTags("Integration - Appraisal Results")
        .AllowAnonymous()
        .Produces<LegacyAppraisalResultEnvelope>(StatusCodes.Status200OK);
    }
}
