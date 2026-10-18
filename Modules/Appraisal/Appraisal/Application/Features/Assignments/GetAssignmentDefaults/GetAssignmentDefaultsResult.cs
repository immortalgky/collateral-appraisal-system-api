namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

// Same names as AssignmentDefaultsEntry, the JSON file and the AppraisalAssignment columns.
public record GetAssignmentDefaultsResult(
    string AssignmentType,
    string AssignmentMethod,
    string InternalFollowupAssignmentMethod
);
