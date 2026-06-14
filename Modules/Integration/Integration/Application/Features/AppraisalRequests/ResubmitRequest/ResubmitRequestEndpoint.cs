using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

/// <summary>
/// Bank-facing request body. All request-data fields are optional — the followup branch
/// sends only Documents/Titles + Mode="Followup"; the data-fix branch sends the full request
/// data (Mode optional, defaults to "DataFix" for back-compat).
/// </summary>
public record ResubmitRequestRequest(
    string? Purpose,
    string? Channel,
    UserInfoDto? Requestor,
    UserInfoDto? Creator,
    string? Priority,
    bool? IsPma,
    RequestDetailDto? Detail,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents,
    string? Mode = null);

public class ResubmitRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/requests/{requestId:guid}/resubmit",
                async (Guid requestId, ResubmitRequestRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new ResubmitRequestCommand(
                        requestId,
                        request.Purpose,
                        request.Channel,
                        request.Requestor,
                        request.Creator,
                        request.Priority,
                        request.IsPma,
                        request.Detail,
                        request.Customers,
                        request.Properties,
                        request.Titles,
                        request.Documents,
                        request.Mode);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ResubmitRequest")
            .Produces<ResubmitRequestResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Integration - Appraisal Requests")
            .WithSummary("Resubmit an existing request")
            .WithDescription("Resubmits an existing request in the system.");
    }
}