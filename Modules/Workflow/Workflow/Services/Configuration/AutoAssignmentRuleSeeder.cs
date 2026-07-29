using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Data.Entities;

namespace Workflow.Services.Configuration;

/// <summary>
/// Seeds the initial-routing rules. Idempotent: skipped entirely if any rows already exist, so an
/// admin's later edits are never reverted.
///
/// The table is seeded with the COMPLETE picture — rules 10-30 and 50 transcribe what
/// appraisal-workflow.json does today, and rule 40 is the phase-1 go-live override layered on top.
/// Nothing here changes the workflow definition, so there is no new workflow version.
///
/// <para><b>Ending the go-live window takes TWO changes, not one:</b></para>
/// <list type="number">
///   <item>Deactivate rule 40 via <c>/api/workflow/auto-assignment-rules</c> (and activate rule 50,
///     though the workflow definition's own default already covers it if you forget).</item>
///   <item>Set SystemConfiguration <c>ExternalCompanyAssignmentEnabled</c> to <c>true</c>.</item>
/// </list>
///
/// <para>
/// Both are required because they close different holes, and neither can close the other's.
/// Rule 40 stops a case being ROUTED toward a company — but routing happens before
/// <c>int-pma-input</c>, and a PMA case reaches <c>company-selection</c> afterwards no matter what
/// was decided at routing time. Only the config switch (read by
/// <see cref="Workflow.Workflow.Activities.CompanySelectionActivity.CompanyAssignmentEnabledKey"/>) blocks
/// that, and it is also what stops an admin assigning a company by hand. Conversely the config
/// switch alone would let cases route toward a company and then bounce back off the escalation
/// path, which is noisier than never routing them there.
/// Leaving the config switch on its own is a working safety net; leaving rule 40 on its own reopens
/// both the PMA path and admin-initiated assignment.
/// </para>
/// </summary>
public class AutoAssignmentRuleSeeder(
    WorkflowDbContext context,
    ILogger<AutoAssignmentRuleSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        if (await context.AutoAssignmentRules.AnyAsync())
        {
            logger.LogInformation("AutoAssignmentRules already seeded, skipping");
            return;
        }

        var rules = BuildRules();

        context.AutoAssignmentRules.AddRange(rules);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} AutoAssignmentRule rows", rules.Length);
    }

    /// <summary>
    /// The seeded rule set. Exposed so tests can pin the precedence and active flags without a
    /// database — the ordering between hasAppraisalBook and isPma in particular is load-bearing
    /// (see the comment on rule 10) and was previously seeded inverted.
    /// </summary>
    public static AutoAssignmentRule[] BuildRules()
    {
        return new[]
        {
            // ── Rules 10-30: today's behaviour, transcribed from appraisal-workflow.json ────────
            // These are ACTIVE because they are path selection, not auto-assignment — they must
            // keep working during the go-live window exactly as they do now.
            //
            // Order mirrors the JSON exactly: routingConditions is iterated in insertion order and
            // ResolveFromDefinition breaks on the first match, where auto_assign_internal
            // (hasAppraisalBook) is listed BEFORE pma_flow (isPma). A request carrying both flags
            // therefore went to internal execution with the book it already had, so hasAppraisalBook
            // keeps the lower priority number here.
            AutoAssignmentRule.Create(
                ruleName: "Cases that already have an appraisal book go to internal execution",
                priority: 10,
                routingDecision: RoutingDecisions.Internal,
                createdBy: "system",
                conditionExpression: "hasAppraisalBook == true"),

            AutoAssignmentRule.Create(
                ruleName: "PMA cases go to PMA property input",
                priority: 20,
                routingDecision: RoutingDecisions.Pma,
                createdBy: "system",
                conditionExpression: "isPma == true"),

            // Expressed as a ConditionExpression rather than the typed Channels/Priorities/LoanTypes
            // columns on purpose: those columns AND together, but the JSON condition is an OR.
            AutoAssignmentRule.Create(
                ruleName: "High priority, IBG or manual-channel cases go to admin review",
                priority: 30,
                routingDecision: RoutingDecisions.AdminReview,
                createdBy: "system",
                conditionExpression:
                    "priority == 'high' || bankingSegment == 'IBG' || channel == 'MANUAL'"),

            // ── Rule 40: the go-live control. Deactivate this ONE row to end the window. ───────
            // Sits above rule 50 so it shadows it. Rules 10-30 have lower numbers and still match
            // first, so PMA cases keep reaching int-pma-input and appraisal-book cases keep
            // reaching internal execution — only what would otherwise have auto-assigned externally
            // is diverted here.
            AutoAssignmentRule.Create(
                ruleName: "Phase-1 go-live: every other case goes to admin review",
                priority: 40,
                routingDecision: RoutingDecisions.AdminReview,
                createdBy: "system"),

            // ── Rule 50: normal steady-state behaviour, seeded INACTIVE. ──────────────────────
            // This is appraisal-workflow.json's defaultDecision (auto_assign_external) written as a
            // rule so the table documents the full picture. Activate it when rule 40 is switched
            // off. Forgetting to is not fatal: with no rule matching, RoutingActivity falls back to
            // the workflow definition, whose default is the same auto_assign_external.
            AutoAssignmentRule.Create(
                ruleName: "Steady state: everything else auto-assigns to an external company",
                priority: 50,
                routingDecision: RoutingDecisions.ExternalRoundRobin,
                createdBy: "system",
                isActive: false)
        };
    }
}
