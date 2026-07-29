using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Services.Configuration;

/// <summary>
/// Admin CRUD for <c>workflow.AutoAssignmentRules</c> — the rules that decide, at initial-routing,
/// whether a new appraisal goes to internal admin review, straight to an internal appraiser, to
/// external round-robin, or to the PMA input step. Gated by the existing <c>workflow.admin</c> policy.
///
/// Deactivating every rule is a supported state: RoutingActivity then falls back to the routing
/// conditions declared in appraisal-workflow.json.
/// </summary>
public class AutoAssignmentRuleAdminEndpoints : ICarterModule
{
    private const string AdminPolicy = "workflow.admin";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow/auto-assignment-rules")
            .WithTags("Auto Assignment Rules")
            .RequireAuthorization(AdminPolicy);

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> List(WorkflowDbContext db, CancellationToken ct)
    {
        var rules = await db.AutoAssignmentRules
            .AsNoTracking()
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RuleName)
            .ToListAsync(ct);

        return Results.Ok(rules.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetById(Guid id, WorkflowDbContext db, CancellationToken ct)
    {
        var rule = await db.AutoAssignmentRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return rule is null ? Results.NotFound() : Results.Ok(ToDto(rule));
    }

    private static async Task<IResult> Create(
        CreateAutoAssignmentRuleRequest request,
        WorkflowDbContext db,
        IExpressionEvaluator evaluator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (Validate(request.RuleName, request.RoutingDecision, request.MinFacilityLimit,
                request.MaxFacilityLimit, request.ConditionExpression, evaluator) is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var rule = AutoAssignmentRule.Create(
            request.RuleName,
            request.Priority,
            request.RoutingDecision,
            currentUser.UserCode ?? "system",
            request.Channels,
            request.EntrySources,
            request.LoanTypes,
            request.Priorities,
            request.MinFacilityLimit,
            request.MaxFacilityLimit,
            request.ConditionExpression,
            request.IsActive);

        db.AutoAssignmentRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/workflow/auto-assignment-rules/{rule.Id}", ToDto(rule));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateAutoAssignmentRuleRequest request,
        WorkflowDbContext db,
        IExpressionEvaluator evaluator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (Validate(request.RuleName, request.RoutingDecision, request.MinFacilityLimit,
                request.MaxFacilityLimit, request.ConditionExpression, evaluator) is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var rule = await db.AutoAssignmentRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return Results.NotFound();

        rule.Update(
            request.RuleName,
            request.Priority,
            request.RoutingDecision,
            currentUser.UserCode ?? "system",
            request.Channels,
            request.EntrySources,
            request.LoanTypes,
            request.Priorities,
            request.MinFacilityLimit,
            request.MaxFacilityLimit,
            request.ConditionExpression,
            request.IsActive);

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(rule));
    }

    private static async Task<IResult> Delete(Guid id, WorkflowDbContext db, CancellationToken ct)
    {
        var rule = await db.AutoAssignmentRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return Results.NotFound();

        db.AutoAssignmentRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Rejects a rule the routing engine could not act on. The expression is compile-checked here
    /// so a typo surfaces at save time rather than silently skipping the rule at routing time.
    /// </summary>
    private static string? Validate(
        string? ruleName,
        string? routingDecision,
        decimal? min,
        decimal? max,
        string? conditionExpression,
        IExpressionEvaluator evaluator)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
            return "Rule name is required.";

        if (!RoutingDecisions.IsValid(routingDecision))
            return $"Routing decision must be one of: {string.Join(", ", RoutingDecisions.All)}.";

        if (min.HasValue && max.HasValue && min.Value > max.Value)
            return "Minimum facility limit cannot be greater than the maximum.";

        if (!string.IsNullOrWhiteSpace(conditionExpression)
            && !evaluator.ValidateExpression(conditionExpression, out var expressionError))
            return $"Condition expression is invalid: {expressionError}";

        return null;
    }

    private static AutoAssignmentRuleDto ToDto(AutoAssignmentRule r) => new(
        r.Id, r.RuleName, r.Priority, r.IsActive,
        r.Channels, r.EntrySources, r.LoanTypes, r.Priorities,
        r.MinFacilityLimit, r.MaxFacilityLimit, r.ConditionExpression,
        r.RoutingDecision, r.CreatedAt, r.UpdatedAt, r.CreatedBy, r.UpdatedBy);
}
