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
    [FromQuery] public string? Search { get; init; }

    // Multi-value filters — comma-separated, translated to IN.
    [FromQuery] public string? Status { get; init; }
    [FromQuery] public string? Priority { get; init; }
    [FromQuery] public string? AppraisalType { get; init; }
    [FromQuery] public string? SlaStatus { get; init; }
    [FromQuery] public string? AssignmentType { get; init; }
    [FromQuery] public string? Purpose { get; init; }

    /// <summary>Matches appraisals holding at least one property of the given type(s).</summary>
    [FromQuery] public string? PropertyType { get; init; }

    // Assignment. AssigneeUserId is a username such as "P5229", not a GUID.
    [FromQuery] public string? AssigneeUserId { get; init; }
    [FromQuery] public string? AssigneeCompanyId { get; init; }

    // Request metadata
    [FromQuery] public string? Channel { get; init; }
    [FromQuery] public string? BankingSegment { get; init; }
    [FromQuery] public bool? IsPma { get; init; }

    // Geographic
    [FromQuery] public string? Province { get; init; }
    [FromQuery] public string? District { get; init; }
    [FromQuery] public string? SubDistrict { get; init; }

    // Single-column search. Search OR-s three columns and therefore always needs the view; these
    // let the caller name the column instead. AppraisalNumber and RequestedAt* stay on the base
    // table, which keeps the cheap COUNT available.
    [FromQuery] public string? CustomerName { get; init; }
    [FromQuery] public string? AppraisalNumber { get; init; }
    [FromQuery] public string? RequestNumber { get; init; }

    // Date ranges
    [FromQuery] public DateTime? CreatedFrom { get; init; }
    [FromQuery] public DateTime? CreatedTo { get; init; }
    [FromQuery] public DateTime? SlaDueDateFrom { get; init; }
    [FromQuery] public DateTime? SlaDueDateTo { get; init; }
    [FromQuery] public DateTime? AssignedDateFrom { get; init; }
    [FromQuery] public DateTime? AssignedDateTo { get; init; }
    [FromQuery] public DateTime? AppointmentDateFrom { get; init; }
    [FromQuery] public DateTime? AppointmentDateTo { get; init; }
    [FromQuery] public DateTime? RequestedAtFrom { get; init; }
    [FromQuery] public DateTime? RequestedAtTo { get; init; }

    // Sorting
    [FromQuery] public string? SortBy { get; init; }
    [FromQuery] public string? SortDir { get; init; }

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
