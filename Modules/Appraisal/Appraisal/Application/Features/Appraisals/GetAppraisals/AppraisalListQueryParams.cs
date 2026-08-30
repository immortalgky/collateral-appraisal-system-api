using Microsoft.AspNetCore.Mvc;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// The query-string surface shared by <c>GET /appraisals</c> and <c>GET /appraisals/export</c>.
///
/// The two endpoints take the same filters and build the same <see cref="GetAppraisalsFilterRequest"/>,
/// so they used to declare ~30 identical <c>[FromQuery]</c> parameters each and repeat the whole
/// initializer. Adding one filter meant editing both files identically and getting it right twice —
/// which is how the two drifted in the first place. Bound with <c>[AsParameters]</c>, the same way
/// <c>PaginationRequest</c> already is.
/// </summary>
public sealed record AppraisalListQueryParams
{
    /// <summary>Free text across AppraisalNumber, CustomerName and RequestNumber.</summary>
    [FromQuery(Name = "search")] public string? Search { get; init; }

    // Multi-value filters — comma-separated, translated to IN.
    [FromQuery(Name = "status")] public string? Status { get; init; }
    [FromQuery(Name = "priority")] public string? Priority { get; init; }
    [FromQuery(Name = "appraisalType")] public string? AppraisalType { get; init; }
    [FromQuery(Name = "slaStatus")] public string? SlaStatus { get; init; }
    [FromQuery(Name = "assignmentType")] public string? AssignmentType { get; init; }
    [FromQuery(Name = "purpose")] public string? Purpose { get; init; }

    /// <summary>Matches appraisals holding at least one property of the given type(s).</summary>
    [FromQuery(Name = "propertyType")] public string? PropertyType { get; init; }

    // Assignment. AssigneeUserId is a username such as "P5229", not a GUID.
    [FromQuery(Name = "assigneeUserId")] public string? AssigneeUserId { get; init; }
    [FromQuery(Name = "assigneeCompanyId")] public string? AssigneeCompanyId { get; init; }

    // Request metadata
    [FromQuery(Name = "channel")] public string? Channel { get; init; }
    [FromQuery(Name = "bankingSegment")] public string? BankingSegment { get; init; }
    [FromQuery(Name = "isPma")] public bool? IsPma { get; init; }

    // Geographic
    [FromQuery(Name = "province")] public string? Province { get; init; }
    [FromQuery(Name = "district")] public string? District { get; init; }
    [FromQuery(Name = "subDistrict")] public string? SubDistrict { get; init; }

    // Single-column search. Search OR-s three columns and therefore always needs the view; these
    // let the caller name the column instead. AppraisalNumber and RequestedAt* stay on the base
    // table, which keeps the cheap COUNT available.
    [FromQuery(Name = "customerName")] public string? CustomerName { get; init; }
    [FromQuery(Name = "appraisalNumber")] public string? AppraisalNumber { get; init; }
    [FromQuery(Name = "requestNumber")] public string? RequestNumber { get; init; }

    // Date ranges
    [FromQuery(Name = "createdFrom")] public DateTime? CreatedFrom { get; init; }
    [FromQuery(Name = "createdTo")] public DateTime? CreatedTo { get; init; }
    [FromQuery(Name = "slaDueDateFrom")] public DateTime? SlaDueDateFrom { get; init; }
    [FromQuery(Name = "slaDueDateTo")] public DateTime? SlaDueDateTo { get; init; }
    [FromQuery(Name = "assignedDateFrom")] public DateTime? AssignedDateFrom { get; init; }
    [FromQuery(Name = "assignedDateTo")] public DateTime? AssignedDateTo { get; init; }
    [FromQuery(Name = "appointmentDateFrom")] public DateTime? AppointmentDateFrom { get; init; }
    [FromQuery(Name = "appointmentDateTo")] public DateTime? AppointmentDateTo { get; init; }
    [FromQuery(Name = "requestedAtFrom")] public DateTime? RequestedAtFrom { get; init; }
    [FromQuery(Name = "requestedAtTo")] public DateTime? RequestedAtTo { get; init; }

    // Sorting
    [FromQuery(Name = "sortBy")] public string? SortBy { get; init; }
    [FromQuery(Name = "sortDir")] public string? SortDir { get; init; }

    public GetAppraisalsFilterRequest ToFilterRequest() =>
        new(
            Search: Search,
            Status: Status,
            Priority: Priority,
            AppraisalType: AppraisalType,
            SlaStatus: SlaStatus,
            AssignmentType: AssignmentType,
            AssigneeUserId: AssigneeUserId,
            AssigneeCompanyId: AssigneeCompanyId,
            Channel: Channel,
            BankingSegment: BankingSegment,
            IsPma: IsPma,
            Province: Province,
            District: District,
            CreatedFrom: CreatedFrom,
            CreatedTo: CreatedTo,
            SlaDueDateFrom: SlaDueDateFrom,
            SlaDueDateTo: SlaDueDateTo,
            AssignedDateFrom: AssignedDateFrom,
            AssignedDateTo: AssignedDateTo,
            AppointmentDateFrom: AppointmentDateFrom,
            AppointmentDateTo: AppointmentDateTo,
            SortBy: SortBy,
            SortDir: SortDir
        )
        {
            Purpose = Purpose,
            PropertyType = PropertyType,
            CustomerName = CustomerName,
            AppraisalNumber = AppraisalNumber,
            RequestNumber = RequestNumber,
            SubDistrict = SubDistrict,
            RequestedAtFrom = RequestedAtFrom,
            RequestedAtTo = RequestedAtTo,
        };
}
