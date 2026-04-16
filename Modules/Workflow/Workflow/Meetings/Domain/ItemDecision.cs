namespace Workflow.Meetings.Domain;

public enum ItemDecision
{
    /// <summary>Secretary has not yet acted on this item.</summary>
    Pending,
    /// <summary>Secretary released the item; workflow advances to parallel member approvals.</summary>
    Released,
    /// <summary>Secretary routed the item back to the appraisal team for rework.</summary>
    RoutedBack
}
