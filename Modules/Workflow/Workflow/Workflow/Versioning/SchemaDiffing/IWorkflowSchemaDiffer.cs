using Workflow.Workflow.Schema;
using DomainBreakingChange = Workflow.Workflow.Models.BreakingChange;

namespace Workflow.Workflow.Versioning.SchemaDiffing;

/// <summary>
/// Computes breaking-change diffs between two workflow schemas.
/// Pure, stateless, unit-testable — no I/O, no DI-owned state.
/// </summary>
public interface IWorkflowSchemaDiffer
{
    IReadOnlyList<DomainBreakingChange> Diff(WorkflowSchema oldSchema, WorkflowSchema newSchema);
}
