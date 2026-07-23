namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module (via outbox in RaiseDocumentFollowupCommandHandler) when a
/// user requests additional documents from the customer. Consumed by the Notification module,
/// which resolves the RM (recipient) and the acting user's display name via <c>IUserLookupService</c>,
/// renders the email, and delivers it via SMTP.
/// </summary>
public record DocumentFollowupEmailIntegrationEvent : IntegrationEvent
{
    /// <summary>The document-followup that triggered this send — used as the send-log ReferenceId.</summary>
    public Guid FollowupId { get; init; }

    /// <summary>RM bank code (request maker / workflow StartedBy). The consumer resolves email + display name.</summary>
    public string? RmUsername { get; init; }

    /// <summary>Bank code of the user who raised the followup. The consumer resolves this to the signature name.</summary>
    public string? ActingUsername { get; init; }

    public string? CustomerName { get; init; }
    public string? AppraisalNumber { get; init; }

    /// <summary>One entry per requested document: the resolved document name + its remark/notes.</summary>
    public IReadOnlyList<DocumentFollowupEmailItem> Items { get; init; } = [];
}

/// <summary>A requested document line: display name + the remark wording from the decision screen.</summary>
public sealed record DocumentFollowupEmailItem(string DocumentName, string? Notes);
