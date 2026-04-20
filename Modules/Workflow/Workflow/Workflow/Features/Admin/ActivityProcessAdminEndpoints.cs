using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NJsonSchema;
using Shared.Identity;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Pipeline;

namespace Workflow.Workflow.Features.Admin;

/// <summary>
/// Admin endpoints for managing the pluggable activity process pipeline.
/// All endpoints require the "workflow.admin" authorization policy.
/// </summary>
public class ActivityProcessAdminEndpoints : ICarterModule
{
    private const string AdminPolicy = "workflow.admin";
    private const string Tag = "Workflow Admin";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ── Step Catalog ───────────────────────────────────────────────────

        app.MapGet("/api/workflow/admin/step-catalog", (IStepCatalog catalog) =>
            {
                var items = catalog.GetAll().Select(d => new StepCatalogItemResponse(
                    d.Name,
                    d.DisplayName,
                    d.Kind.ToString(),
                    d.ParametersSchema.ToJson(),
                    d.Description));
                return Results.Ok(items);
            })
            .WithName("GetStepCatalog")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Get process config for an activity ────────────────────────────

        app.MapGet("/api/workflow/admin/activities/{activityName}/process-config",
            async (string activityName, WorkflowDbContext db, CancellationToken ct) =>
            {
                var configs = await db.ActivityProcessConfigurations
                    .Where(c => c.ActivityName == activityName)
                    .OrderBy(c => c.Kind)
                    .ThenBy(c => c.SortOrder)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var items = configs.Select(MapToResponse).ToList();
                return Results.Ok(items);
            })
            .WithName("GetActivityProcessConfig")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Validate process config (dry-run) ─────────────────────────────

        app.MapPost("/api/workflow/admin/activities/{activityName}/process-config:validate",
            async (
                string activityName,
                List<UpsertProcessConfigRequest> requests,
                IStepCatalog catalog,
                IPredicateEvaluator predicateEvaluator) =>
            {
                var errors = ValidateRequests(requests, catalog, predicateEvaluator);
                if (errors.Count > 0)
                    return Results.BadRequest(new { Errors = errors });

                return Results.Ok(new { Valid = true });
            })
            .WithName("ValidateActivityProcessConfig")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Full-replace PUT ───────────────────────────────────────────────

        app.MapPut("/api/workflow/admin/activities/{activityName}/process-config",
            async (
                string activityName,
                List<UpsertProcessConfigRequest> requests,
                IStepCatalog catalog,
                IPredicateEvaluator predicateEvaluator,
                WorkflowDbContext db,
                ICurrentUserService currentUserService,
                CancellationToken ct) =>
            {
                // Validate first
                var errors = ValidateRequests(requests, catalog, predicateEvaluator);
                if (errors.Count > 0)
                    return Results.BadRequest(new { Errors = errors });

                var updatedBy = currentUserService.Username ?? "admin";

                // Load existing rows
                var existing = await db.ActivityProcessConfigurations
                    .Where(c => c.ActivityName == activityName)
                    .ToListAsync(ct);

                // B3: Key by ProcessorName (the canonical Descriptor.Name), not StepName.
                var incomingProcessorNames = requests.Select(r => r.ProcessorName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingByProcessorName = existing.ToDictionary(
                    c => c.ProcessorName, StringComparer.OrdinalIgnoreCase);

                // Delete removed rows
                foreach (var toRemove in existing.Where(c => !incomingProcessorNames.Contains(c.ProcessorName)))
                    db.ActivityProcessConfigurations.Remove(toRemove);

                // Insert or update
                foreach (var req in requests)
                {
                    // B3: Kind must match the descriptor — use server-side descriptor.Kind instead
                    // of trusting the request, to prevent semantic confusion and the W1 mismatch risk.
                    var descriptor = catalog.GetDescriptor(req.ProcessorName)!; // validated above
                    var kind = descriptor.Kind;

                    if (existingByProcessorName.TryGetValue(req.ProcessorName, out var row))
                    {
                        row.Update(kind, req.SortOrder, req.ParametersJson, req.RunIfExpression,
                            req.IsActive, updatedBy);
                    }
                    else
                    {
                        // B3: StepName = human-readable label from descriptor; ProcessorName = stable key.
                        var newRow = ActivityProcessConfiguration.Create(
                            activityName, descriptor.DisplayName, req.ProcessorName, kind,
                            req.SortOrder, updatedBy, req.ParametersJson, req.RunIfExpression);
                        db.ActivityProcessConfigurations.Add(newRow);
                    }
                }

                await db.SaveChangesAsync(ct);

                // Return the updated list
                var updated = await db.ActivityProcessConfigurations
                    .Where(c => c.ActivityName == activityName)
                    .OrderBy(c => c.Kind)
                    .ThenBy(c => c.SortOrder)
                    .AsNoTracking()
                    .ToListAsync(ct);

                return Results.Ok(updated.Select(MapToResponse).ToList());
            })
            .WithName("PutActivityProcessConfig")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Execution trace for a workflow activity execution ──────────────

        app.MapGet("/api/workflow/admin/executions/{workflowActivityExecutionId:guid}",
            async (Guid workflowActivityExecutionId, WorkflowDbContext db, CancellationToken ct) =>
            {
                var rows = await db.ActivityProcessExecutions
                    .Where(e => e.WorkflowActivityExecutionId == workflowActivityExecutionId)
                    .OrderBy(e => e.Kind)
                    .ThenBy(e => e.SortOrder)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var items = rows.Select(r => new ActivityProcessExecutionResponse(
                    r.Id,
                    r.ConfigurationId,
                    r.ConfigurationVersion,
                    r.StepName,
                    r.Kind.ToString(),
                    r.SortOrder,
                    r.RunIfExpressionSnapshot,
                    r.ParametersJsonSnapshot,
                    r.Outcome.ToString(),
                    r.SkipReason?.ToString(),
                    r.DurationMs,
                    r.ErrorMessage,
                    r.CreatedOn)).ToList();

                return Results.Ok(items);
            })
            .WithName("GetActivityProcessExecutions")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);
    }

    // ── Validation helper ─────────────────────────────────────────────────

    private static List<ConfigEntryError> ValidateRequests(
        List<UpsertProcessConfigRequest> requests,
        IStepCatalog catalog,
        IPredicateEvaluator predicateEvaluator)
    {
        var errors = new List<ConfigEntryError>();

        foreach (var (req, index) in requests.Select((r, i) => (r, i)))
        {
            // B3: Validate by ProcessorName (the canonical descriptor key).
            var descriptor = catalog.GetDescriptor(req.ProcessorName);
            if (descriptor is null)
            {
                errors.Add(new ConfigEntryError(index, req.ProcessorName,
                    $"Step '{req.ProcessorName}' is not registered in the step catalog."));
                continue;
            }

            // 2. ParametersJson must validate against the step's schema (if provided)
            if (!string.IsNullOrWhiteSpace(req.ParametersJson))
            {
                try
                {
                    var schema = descriptor.ParametersSchema;
                    var schemaErrors = schema.Validate(req.ParametersJson);
                    if (schemaErrors.Count > 0)
                    {
                        var msg = string.Join("; ", schemaErrors.Select(e => e.ToString()));
                        errors.Add(new ConfigEntryError(index, req.ProcessorName,
                            $"ParametersJson validation failed: {msg}"));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new ConfigEntryError(index, req.ProcessorName,
                        $"ParametersJson parse error: {ex.Message}"));
                }
            }

            // 3. RunIfExpression must compile in Jint (if provided)
            if (!string.IsNullOrWhiteSpace(req.RunIfExpression))
            {
                var compileError = predicateEvaluator.TryPrepare(req.RunIfExpression);
                if (compileError is not null)
                {
                    errors.Add(new ConfigEntryError(index, req.ProcessorName,
                        $"RunIfExpression compile error: {compileError}"));
                }
            }
        }

        return errors;
    }

    // ── Mapping helpers ────────────────────────────────────────────────────

    private static ProcessConfigResponse MapToResponse(ActivityProcessConfiguration c) =>
        new(c.Id, c.ActivityName, c.StepName, c.ProcessorName,
            c.Kind.ToString(), c.SortOrder, c.RunIfExpression, c.ParametersJson,
            c.IsActive, c.Version, c.CreatedAt, c.UpdatedAt);
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

/// <summary>
/// B3: ProcessorName is the canonical Descriptor.Name (stable key, e.g. "ValidateHasAppraisedValue").
/// Kind is intentionally absent — the server derives Kind from the step descriptor to prevent
/// mismatches where the caller could set Kind=Validation on an Action-typed step.
/// </summary>
public sealed record UpsertProcessConfigRequest(
    string ProcessorName,
    int SortOrder,
    string? ParametersJson,
    string? RunIfExpression,
    bool IsActive);

public sealed record ProcessConfigResponse(
    Guid Id,
    string ActivityName,
    string StepName,
    string ProcessorName,
    string Kind,
    int SortOrder,
    string? RunIfExpression,
    string? ParametersJson,
    bool IsActive,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record StepCatalogItemResponse(
    string Name,
    string DisplayName,
    string Kind,
    string ParametersSchema,
    string? Description);

public sealed record ActivityProcessExecutionResponse(
    Guid Id,
    Guid? ConfigurationId,
    int ConfigurationVersion,
    string StepName,
    string Kind,
    int SortOrder,
    string? RunIfExpressionSnapshot,
    string? ParametersJsonSnapshot,
    string Outcome,
    string? SkipReason,
    int DurationMs,
    string? ErrorMessage,
    DateTime CreatedOn);

public sealed record ConfigEntryError(int Index, string StepName, string Message);
