using System.Text.Json.Serialization;

namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

// Same names as AssignmentDefaultsEntry, the JSON file and the AppraisalAssignment columns.
// The API drops null properties app-wide; PreviousCompany is agreed with the front end as always present
// ("null when there is none"), so it is pinned to be written.
public record GetAssignmentDefaultsResult(
    string AssignmentType,
    string AssignmentMethod,
    string InternalFollowupAssignmentMethod,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] PreviousCompanyDto? PreviousCompany = null
);

/// <summary>
/// The external company that did the picked prior appraisal of a construction-inspection (Progressive) request.
/// <paramref name="MouActive"/>: its MOU window covers today, i.e. it can be assigned again.
/// <paramref name="InspectionNumber"/> is the prior's own round (null when the prior is the original appraisal).
/// </summary>
public record PreviousCompanyDto(
    Guid CompanyId,
    string CompanyName,
    Guid AppraisalId,
    string? AppraisalNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? InspectionNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? CompletedAt,
    bool MouActive);
