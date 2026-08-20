using Shared.DDD;

namespace Workflow.Data.Entities;

/// <summary>
/// The routing decision a matched <see cref="AutoAssignmentRule"/> produces. Each value maps onto
/// one of the decision strings RoutingActivity emits from the initial-routing activity.
/// </summary>
public static class RoutingDecisions
{
    /// <summary>Stop at appraisal-assignment so an internal admin decides. → admin_review</summary>
    public const string AdminReview = "AdminReview";

    /// <summary>Straight to int-appraisal-execution. → auto_assign_internal</summary>
    public const string Internal = "Internal";

    /// <summary>Weighted round-robin over the configured company pool. → auto_assign_external</summary>
    public const string ExternalRoundRobin = "ExternalRoundRobin";

    /// <summary>PMA property input first. → pma_flow</summary>
    public const string Pma = "Pma";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AdminReview, Internal, ExternalRoundRobin, Pma
        };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>
/// Admin-configurable rule that decides which path a new appraisal takes at initial-routing.
/// Rules are evaluated in ascending <see cref="Priority"/> and the first match wins; when nothing
/// matches, RoutingActivity falls back to the routingConditions/defaultDecision declared in
/// appraisal-workflow.json — so an empty table reproduces the pre-rule-table behaviour exactly.
///
/// Every condition below is matched against a variable that already exists on the workflow
/// instance at initial-routing time, so evaluation needs no request lookup and no cross-module read.
/// A null/empty condition means "matches anything".
/// </summary>
public class AutoAssignmentRule : Entity<Guid>
{
    public string RuleName { get; private set; } = default!;

    /// <summary>Lower runs first. Ties are broken by RuleName for determinism.</summary>
    public int Priority { get; private set; }

    public bool IsActive { get; private set; }

    // ── Conditions (CSV; null/empty = matches anything) ──────────────────────────────────────

    /// <summary>Matched against the <c>channel</c> variable, e.g. "LOS,CAS,SIBS,MANUAL".</summary>
    public string? Channels { get; private set; }

    /// <summary>Matched against the <c>entrySource</c> variable — "UI" or "API".</summary>
    public string? EntrySources { get; private set; }

    /// <summary>Matched against the <c>bankingSegment</c> variable, e.g. "Retail,IBG".</summary>
    public string? LoanTypes { get; private set; }

    /// <summary>Matched against the <c>priority</c> variable, e.g. "normal,high".</summary>
    public string? Priorities { get; private set; }

    /// <summary>Inclusive lower bound on the <c>facilityLimit</c> variable.</summary>
    public decimal? MinFacilityLimit { get; private set; }

    /// <summary>Inclusive upper bound on the <c>facilityLimit</c> variable.</summary>
    public decimal? MaxFacilityLimit { get; private set; }

    /// <summary>
    /// Optional sandboxed JavaScript predicate evaluated against the workflow variables, for
    /// conditions that do not warrant a column of their own — e.g. <c>isPma === true</c> or
    /// <c>hasAppraisalBook === true</c>. Same mechanism as ActivityProcessConfigurations.
    /// Must return a boolean; a rule whose expression throws is skipped, never treated as a match.
    /// </summary>
    public string? ConditionExpression { get; private set; }

    // ── Action ───────────────────────────────────────────────────────────────────────────────

    /// <summary>One of <see cref="RoutingDecisions"/>.</summary>
    public string RoutingDecision { get; private set; } = default!;

    public new DateTime CreatedAt { get; private set; }
    public new DateTime UpdatedAt { get; private set; }
    public new string CreatedBy { get; private set; } = default!;
    public new string UpdatedBy { get; private set; } = default!;

    private AutoAssignmentRule()
    {
        // For EF Core
    }

    public static AutoAssignmentRule Create(
        string ruleName,
        int priority,
        string routingDecision,
        string createdBy,
        string? channels = null,
        string? entrySources = null,
        string? loanTypes = null,
        string? priorities = null,
        decimal? minFacilityLimit = null,
        decimal? maxFacilityLimit = null,
        string? conditionExpression = null,
        bool isActive = true)
    {
        Validate(routingDecision, minFacilityLimit, maxFacilityLimit);

        return new AutoAssignmentRule
        {
            Id = Guid.CreateVersion7(),
            RuleName = ruleName,
            Priority = priority,
            RoutingDecision = routingDecision,
            Channels = channels,
            EntrySources = entrySources,
            LoanTypes = loanTypes,
            Priorities = priorities,
            MinFacilityLimit = minFacilityLimit,
            MaxFacilityLimit = maxFacilityLimit,
            ConditionExpression = conditionExpression,
            IsActive = isActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };
    }

    public void Update(
        string ruleName,
        int priority,
        string routingDecision,
        string updatedBy,
        string? channels = null,
        string? entrySources = null,
        string? loanTypes = null,
        string? priorities = null,
        decimal? minFacilityLimit = null,
        decimal? maxFacilityLimit = null,
        string? conditionExpression = null,
        bool isActive = true)
    {
        Validate(routingDecision, minFacilityLimit, maxFacilityLimit);

        RuleName = ruleName;
        Priority = priority;
        RoutingDecision = routingDecision;
        Channels = channels;
        EntrySources = entrySources;
        LoanTypes = loanTypes;
        Priorities = priorities;
        MinFacilityLimit = minFacilityLimit;
        MaxFacilityLimit = maxFacilityLimit;
        ConditionExpression = conditionExpression;
        IsActive = isActive;
        UpdatedAt = DateTime.Now;
        UpdatedBy = updatedBy;
    }

    public void SetActive(bool isActive, string updatedBy)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.Now;
        UpdatedBy = updatedBy;
    }

    private static void Validate(string routingDecision, decimal? min, decimal? max)
    {
        if (!RoutingDecisions.IsValid(routingDecision))
            throw new ArgumentException(
                $"Unknown routing decision '{routingDecision}'. Expected one of: {string.Join(", ", RoutingDecisions.All)}.",
                nameof(routingDecision));

        if (min.HasValue && max.HasValue && min.Value > max.Value)
            throw new ArgumentException(
                "MinFacilityLimit cannot be greater than MaxFacilityLimit.", nameof(min));
    }
}
