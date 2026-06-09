using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using Shared.Data;
using Shared.Identity;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Validation;

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
        // ── Validation field registry (for admin field picker) ────────────

        app.MapGet("/api/workflow/admin/validation-fields", () =>
            {
                var fields = AppraisalFieldRegistry.Fields.Select(f => new ValidationFieldResponse(
                    f.Key, f.Column, f.DataType, f.DisplayName));
                return Results.Ok(fields);
            })
            .WithName("GetValidationFields")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Per-property validation field picker ──────────────────────────
        // Returns the column names from vw_AppraisalPropertyValidationContext (minus the
        // meta columns) so the FE field-picker stays in sync with the view automatically.
        // If the view does not exist yet (fresh DB), returns an empty list gracefully.

        app.MapGet("/api/workflow/admin/property-validation-fields",
            async (
                ISqlConnectionFactory connectionFactory,
                ILogger<ActivityProcessAdminEndpoints> endpointLogger) =>
            {
                try
                {
                    using var connection = connectionFactory.GetOpenConnection();
                    var columns = (await connection.QueryAsync<string>(
                        """
                        SELECT COLUMN_NAME
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'appraisal'
                          AND TABLE_NAME   = 'vw_AppraisalPropertyValidationContext'
                          AND COLUMN_NAME NOT IN ('AppraisalId', 'SequenceNumber', 'PropertyType')
                        ORDER BY ORDINAL_POSITION
                        """)).ToList();

                    var fields = columns.Select(col => new PropertyValidationFieldResponse(
                        Key: col,
                        DisplayName: PrettifyColumnName(col)));

                    return Results.Ok(fields);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // SQL Server error 208 = "Invalid object name" — the view has not been
                    // deployed yet. Return an empty list so fresh-DB setups don't break.
                    endpointLogger.LogWarning(
                        "vw_AppraisalPropertyValidationContext does not exist yet (SqlError 208). " +
                        "Returning empty field list until the view is deployed.");
                    return Results.Ok(Array.Empty<PropertyValidationFieldResponse>());
                }
            })
            .WithName("GetPropertyValidationFields")
            .WithTags(Tag)
            .RequireAuthorization(AdminPolicy);

        // ── Step Catalog ───────────────────────────────────────────────────

        app.MapGet("/api/workflow/admin/step-catalog", (IStepCatalog catalog) =>
            {
                var items = catalog.GetAll().Select(d => new StepCatalogItemResponse(
                    d.Name,
                    d.DisplayName,
                    d.Kind.ToString(),
                    d.ParametersSchema.ToJson(),
                    d.Description,
                    d.ExampleParametersJson));
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

                    // Severity only applies to Validation steps; force Error for Actions.
                    var severity = kind == StepKind.Validation ? req.Severity : StepSeverity.Error;

                    if (existingByProcessorName.TryGetValue(req.ProcessorName, out var row))
                    {
                        row.Update(kind, req.SortOrder, req.ParametersJson, req.RunIfExpression,
                            req.IsActive, updatedBy, severity);
                    }
                    else
                    {
                        // B3: StepName = human-readable label from descriptor; ProcessorName = stable key.
                        var newRow = ActivityProcessConfiguration.Create(
                            activityName, descriptor.DisplayName, req.ProcessorName, kind,
                            req.SortOrder, updatedBy, req.ParametersJson, req.RunIfExpression, severity);
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
                    r.Severity.ToString(),
                    r.SortOrder,
                    r.RunIfExpressionSnapshot,
                    r.ParametersJsonSnapshot,
                    r.Outcome.ToString(),
                    r.SkipReason?.ToString(),
                    r.DurationMs,
                    r.ErrorMessage,
                    r.Acknowledged,
                    r.AcknowledgedBy,
                    r.AcknowledgedToken,
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

        // A (activity, ProcessorName) pair must be unique — the upsert keys by ProcessorName and
        // the DB enforces a unique index. Reject duplicate ProcessorNames in one payload up front.
        var dupeProcessors = requests
            .GroupBy(r => r.ProcessorName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dupe in dupeProcessors)
            errors.Add(new ConfigEntryError(-1, dupe,
                $"Step '{dupe}' appears more than once; each step may be configured at most once per activity."));

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

            // Warning severity is meaningful only for Validation steps (acknowledge-to-continue).
            if (req.Severity == StepSeverity.Warning && descriptor.Kind != StepKind.Validation)
            {
                errors.Add(new ConfigEntryError(index, req.ProcessorName,
                    "Warning severity only applies to Validation steps."));
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

    // ── Column name prettifier ─────────────────────────────────────────────

    /// <summary>
    /// Splits a PascalCase column name into space-separated words.
    /// e.g. "TitleNumber" → "Title Number", "LandOffice" → "Land Office".
    /// Falls back to the raw column name if the input is null or empty.
    /// </summary>
    private static string PrettifyColumnName(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return columnName ?? "";
        // Insert a space before each uppercase letter that follows a lowercase letter.
        var result = System.Text.RegularExpressions.Regex.Replace(
            columnName, "(?<=[a-z])(?=[A-Z])", " ");
        return result;
    }

    // ── Mapping helpers ────────────────────────────────────────────────────

    private static ProcessConfigResponse MapToResponse(ActivityProcessConfiguration c) =>
        new(c.Id, c.ActivityName, c.StepName, c.ProcessorName,
            c.Kind.ToString(), c.Severity.ToString(), c.SortOrder, c.RunIfExpression, c.ParametersJson,
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
    bool IsActive,
    StepSeverity Severity = StepSeverity.Error);

public sealed record ProcessConfigResponse(
    Guid Id,
    string ActivityName,
    string StepName,
    string ProcessorName,
    string Kind,
    string Severity,
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
    string? Description,
    string? ExampleParametersJson);

public sealed record ActivityProcessExecutionResponse(
    Guid Id,
    Guid? ConfigurationId,
    int ConfigurationVersion,
    string StepName,
    string Kind,
    string Severity,
    int SortOrder,
    string? RunIfExpressionSnapshot,
    string? ParametersJsonSnapshot,
    string Outcome,
    string? SkipReason,
    int DurationMs,
    string? ErrorMessage,
    bool Acknowledged,
    string? AcknowledgedBy,
    string? AcknowledgedToken,
    DateTime CreatedOn);

public sealed record ConfigEntryError(int Index, string StepName, string Message);

public sealed record ValidationFieldResponse(
    string Key,
    string Column,
    string DataType,
    string DisplayName);

/// <summary>
/// Response item for GET /api/workflow/admin/property-validation-fields.
/// Key = column name (== fieldKey used in requiredByType config).
/// DisplayName = light human-readable label (PascalCase split to words).
/// </summary>
public sealed record PropertyValidationFieldResponse(string Key, string DisplayName);
