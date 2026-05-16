namespace Workflow.Contracts.DocumentFollowups;

/// <summary>
/// Returns all OPEN document followups for a given request. The Integration module's external
/// resubmit endpoint uses this to discover the followup to fulfill — the bank doesn't echo
/// back our internal FollowupId.
///
/// Returns 0..N matches. Today's workflow never forks, so 0 or 1 is the expected case;
/// the caller treats 2+ as an ambiguity error.
/// </summary>
public record GetOpenDocumentFollowupForRequestQuery(Guid RequestId)
    : IRequest<IReadOnlyList<DocumentFollowupExternalDto>>;

public record DocumentFollowupExternalDto(
    Guid Id,
    Guid? RequestId,
    string Status,
    Guid? FollowupWorkflowInstanceId,
    IReadOnlyList<DocumentFollowupLineItemExternalDto> LineItems);

public record DocumentFollowupLineItemExternalDto(
    Guid Id,
    string DocumentType,
    string Status);
