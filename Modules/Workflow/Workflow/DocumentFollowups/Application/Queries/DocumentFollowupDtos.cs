namespace Workflow.DocumentFollowups.Application.Queries;

public record DocumentFollowupLineItemDto(
    Guid Id,
    string DocumentType,
    string? Notes,
    string Status,
    string? Reason,
    Guid? DocumentId,
    DateTime? ResolvedAt);

/// <summary>
/// Minimal user reference used in followup DTOs. DisplayName is best-effort; callers must
/// tolerate a null name until a user-lookup service is wired up.
/// </summary>
public record DocumentFollowupUserRef(string UserId, string? DisplayName);

/// <summary>
/// Full document followup payload returned by the detail endpoint.
/// Field names are the contract with the frontend — do not rename without updating it.
/// </summary>
public record DocumentFollowupDto(
    Guid Id,
    Guid ParentAppraisalId,
    Guid? RequestId,
    Guid RaisingWorkflowInstanceId,
    Guid RaisingTaskId,
    string RaisingActivityId,
    DocumentFollowupUserRef RaisedBy,
    Guid? FollowupWorkflowInstanceId,
    string Status,
    string? CancellationReason,
    DateTime RaisedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<DocumentFollowupLineItemDto> LineItems);

/// <summary>
/// Lightweight summary returned by the list endpoint. Excludes the full line item collection
/// in favor of counts so the request-maker inbox can render quickly.
/// </summary>
public record DocumentFollowupSummaryDto(
    Guid Id,
    Guid ParentAppraisalId,
    Guid? RequestId,
    Guid RaisingWorkflowInstanceId,
    Guid RaisingTaskId,
    string RaisingActivityId,
    DocumentFollowupUserRef RaisedBy,
    Guid? FollowupWorkflowInstanceId,
    string Status,
    DateTime RaisedAt,
    DateTime? ResolvedAt,
    int LineItemCount,
    int PendingCount);
