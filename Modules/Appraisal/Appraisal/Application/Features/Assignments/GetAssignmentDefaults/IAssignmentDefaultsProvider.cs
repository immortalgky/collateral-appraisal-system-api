namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

/// <summary>Validated per-Appraisal-Type assignment defaults. Implemented in Infrastructure.</summary>
public interface IAssignmentDefaultsProvider
{
    AssignmentDefaultsSnapshot Current { get; }
}
