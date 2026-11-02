using Shared.Exceptions;

namespace Workflow.Workflow.Activities.Approval;

/// <summary>
/// The approval round about to open could never resolve (too few members left once Conflicted members
/// are excluded, or a rule no remaining member can meet).
///
/// <para>
/// Thrown instead of returning <c>ActivityResult.Failed</c>: the engine turns a failed activity into
/// <c>WorkflowStatus.Failed</c>, which is terminal. The engine rethrows this exception instead, so the
/// submit that led here rolls back and the task stays with the submitter until an admin fixes the committee.
/// </para>
/// </summary>
public class ApprovalRoundUnresolvableException(string message)
    : BadRequestException(message, null, "APPROVAL_ROUND_UNRESOLVABLE");
