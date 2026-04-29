using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetEligibleAppraisalsForQuotation;

/// <summary>
/// Carter endpoint: GET /appraisals/eligible-for-quotation
/// Returns appraisals that have no active assignment and are not in any non-terminal quotation.
/// Exposes the full "Search By" panel from the quotation-creation picker design.
/// </summary>
public class GetEligibleAppraisalsForQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/eligible-for-quotation",
                async (
                    [AsParameters] PaginationRequest pagination,
                    // "Search By" panel fields
                    [FromQuery] string? customerName,
                    [FromQuery] string? appraisalNumber,
                    [FromQuery] string? purpose,
                    [FromQuery] DateTime? requestedAtFrom,
                    [FromQuery] DateTime? requestedAtTo,
                    [FromQuery] string? channel,
                    [FromQuery] string? status,
                    [FromQuery] string? bankingSegment,
                    [FromQuery] string? subDistrict,
                    [FromQuery] string? district,
                    [FromQuery] string? province,
                    // Sorting
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new GetAppraisalsFilterRequest(
                        Search: null,
                        Status: status,
                        Priority: null,
                        AppraisalType: null,
                        SlaStatus: null,
                        AssignmentType: null,
                        AssigneeUserId: null,
                        AssigneeCompanyId: null,
                        Channel: channel,
                        BankingSegment: bankingSegment,
                        IsPma: null,
                        Province: province,
                        District: district,
                        CreatedFrom: null,
                        CreatedTo: null,
                        SlaDueDateFrom: null,
                        SlaDueDateTo: null,
                        AssignedDateFrom: null,
                        AssignedDateTo: null,
                        AppointmentDateFrom: null,
                        AppointmentDateTo: null,
                        SortBy: sortBy,
                        SortDir: sortDir)
                    {
                        CustomerName = customerName,
                        AppraisalNumber = appraisalNumber,
                        Purpose = purpose,
                        SubDistrict = subDistrict,
                        RequestedAtFrom = requestedAtFrom,
                        RequestedAtTo = requestedAtTo,
                    };

                    var result = await sender.Send(
                        new GetEligibleAppraisalsForQuotationQuery(pagination, filter),
                        cancellationToken);

                    return Results.Ok(result);
                })
            .WithName("GetEligibleAppraisalsForQuotation")
            .Produces<PaginatedResult<AppraisalDto>>()
            .WithSummary("Get appraisals eligible for a new standalone quotation")
            .WithDescription(
                "Returns appraisals that have no active assignment and are not in any non-terminal quotation. " +
                "Search-By criteria: customerName, appraisalNumber (report no.), purpose, requestedAtFrom/To, " +
                "channel, status (multi-value, comma-separated), bankingSegment, subDistrict, district, province. " +
                "Supports pagination (pageNumber, pageSize) and sorting (sortBy, sortDir).")
            .WithTags("Quotation");
    }
}
