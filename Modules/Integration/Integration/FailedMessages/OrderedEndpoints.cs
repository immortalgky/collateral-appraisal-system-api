namespace Integration.FailedMessages;

/// <summary>
/// The 7 partitioned (ordered) receive endpoints, docs/failed-messages/api-contract.md "Ordered (partitioned)
/// receive endpoints". This is the ONE place the names live: <c>Bootstrapper/Api/Program.cs</c> registers each
/// <c>ReceiveEndpoint(OrderedEndpoints.X, ...)</c> from these constants, and the collector's discover-fallback
/// step and the API's <c>isOrderedQueue</c>/<c>isOrdered</c> fields read <see cref="Names"/>. Adding an
/// ordered endpoint means adding a constant here (and to <see cref="Names"/>) — <c>OrderedEndpointsTests</c>
/// fails if a constant is missing from <see cref="Names"/>.
/// </summary>
public static class OrderedEndpoints
{
    public const string WebhookDispatch = "webhook-dispatch";
    public const string AppraisalExtCycle = "appraisal-ext-cycle";
    public const string AppraisalSync = "appraisal-sync";
    public const string AppraisalStatusDashboard = "appraisal-status-dashboard";
    public const string AppraisalSlaRecalc = "appraisal-sla-recalc";
    public const string WorkflowInstanceVariables = "workflow-instance-variables";
    public const string PmaSyncStatus = "pma-sync-status";

    public static readonly string[] Names =
    [
        WebhookDispatch, AppraisalExtCycle, AppraisalSync, AppraisalStatusDashboard,
        AppraisalSlaRecalc, WorkflowInstanceVariables, PmaSyncStatus
    ];

    public static bool IsOrdered(string sourceQueue) => Array.IndexOf(Names, sourceQueue) >= 0;
}
