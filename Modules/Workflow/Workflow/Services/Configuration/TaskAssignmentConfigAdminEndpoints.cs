using System.Text.Json;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.AssigneeSelection.Core;
using Workflow.Data;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow;
using Workflow.Workflow.Models;
using Workflow.Workflow.Infrastructure.Seed;
using Workflow.Workflow.Schema;

namespace Workflow.Services.Configuration;

/// <summary>
/// Admin CRUD for <c>workflow.TaskAssignmentConfigurations</c> — the DB override that lets an
/// activity's assignee group / assignment strategies be changed per banking segment without editing
/// the workflow-definition JSON. Gated by the existing <c>workflow.admin</c> policy.
/// </summary>
public class TaskAssignmentConfigAdminEndpoints : ICarterModule
{
    private const string AdminPolicy = "workflow.admin";

    private const string DuplicateScopeMessage =
        "An active override already exists for this activity, workflow and banking segment.";

    private const string SourceKey = JsonPropertyReader.SameAssigneeAsActivityKey;
    private static readonly string SameAssigneeStrategy = AssigneeSelectionStrategy.SameAssigneeAsActivity.ToStringValue();

    private static readonly HashSet<string> AllowedSegments =
        new(StringComparer.OrdinalIgnoreCase) { "Retail", "IBG" };

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow/task-assignment-configs")
            .WithTags("Task Assignment Configuration")
            .RequireAuthorization(AdminPolicy);

        group.MapGet("/", List);
        group.MapGet("/activities", ListActivities);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> List(
        string? activityId,
        string? bankingSegment,
        ITaskConfigurationService service,
        CancellationToken ct)
    {
        var configs = await service.ListConfigurationsAsync(activityId, bankingSegment, ct);
        return Results.Ok(configs);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ITaskConfigurationService service,
        CancellationToken ct)
    {
        var config = await service.GetByIdAsync(id, ct);
        return config is null ? Results.NotFound() : Results.Ok(config);
    }

    internal static async Task<IResult> Create(
        CreateTaskAssignmentConfigurationRequest request,
        ITaskConfigurationService service,
        ICurrentUserService currentUser,
        WorkflowDbContext db,
        CancellationToken ct)
    {
        // Definition-free checks first; the definition is only loaded once they pass.
        if (ValidateBasics(request.ActivityId, request.BankingSegment, request.PrimaryStrategies,
                request.RouteBackStrategies, request.AdditionalConfiguration, request.WorkflowDefinitionId)
            is { } basicError)
            return Results.Problem(detail: basicError, statusCode: StatusCodes.Status400BadRequest);

        // A row being switched off is never blocked by the definition-side checks.
        if (request.IsActive)
        {
            var definitionActivities = await LoadDefinitionActivitiesAsync(
                request.WorkflowDefinitionId, db, ct);
            if (ValidateAgainstDefinition(
                    new OverrideToValidate(request.ActivityId, request.PrimaryStrategies, request.RouteBackStrategies,
                        request.AdditionalConfiguration, request.SpecificAssignee, request.AssigneeGroup,
                        request.TeamConstrained, request.ExcludeAssigneesFrom),
                    definitionActivities)
                is { } error)
                return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        request.CreatedBy = currentUser.UserCode ?? "system";
        try
        {
            var created = await service.CreateConfigurationAsync(request, ct);
            return Results.Created($"/api/workflow/task-assignment-configs/{created.Id}", created);
        }
        catch (DbUpdateException)
        {
            return Results.Problem(detail: DuplicateScopeMessage, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateTaskAssignmentConfigurationRequest request,
        ITaskConfigurationService service,
        ICurrentUserService currentUser,
        WorkflowDbContext db,
        CancellationToken ct)
    {
        // The update request carries no ActivityId; the stored row's is what "same as itself" is checked against.
        if (await service.GetByIdAsync(id, ct) is not { } existing)
            return Results.NotFound();

        // Definition-free checks first; the definition is only loaded once they pass.
        if (ValidateBasics(existing.ActivityId, request.BankingSegment, request.PrimaryStrategies,
                request.RouteBackStrategies, request.AdditionalConfiguration)
            is { } basicError)
            return Results.Problem(detail: basicError, statusCode: StatusCodes.Status400BadRequest);

        // A row being switched off is never blocked by the definition-side checks.
        if (request.IsActive)
        {
            var definitionActivities = await LoadDefinitionActivitiesAsync(
                existing.WorkflowDefinitionId, db, ct);
            if (ValidateAgainstDefinition(
                    new OverrideToValidate(existing.ActivityId, request.PrimaryStrategies, request.RouteBackStrategies,
                        request.AdditionalConfiguration, request.SpecificAssignee, request.AssigneeGroup,
                        request.TeamConstrained, request.ExcludeAssigneesFrom),
                    definitionActivities)
                is { } error)
                return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        request.UpdatedBy = currentUser.UserCode ?? "system";
        try
        {
            var updated = await service.UpdateConfigurationAsync(id, request, ct);
            return Results.Ok(updated);
        }
        catch (DbUpdateException)
        {
            return Results.Problem(detail: DuplicateScopeMessage, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> Delete(
        Guid id,
        ITaskConfigurationService service,
        CancellationToken ct)
    {
        if (await service.GetByIdAsync(id, ct) is null)
            return Results.NotFound();

        await service.DeleteConfigurationAsync(id, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Activity picker source: returns the TaskActivities of the workflow definition (read-only parse of the
    /// latest published version's schema) with their JSON-baseline group/strategies so the admin sees exactly
    /// what an override replaces. No definition file is modified.
    /// </summary>
    private static async Task<IResult> ListActivities(
        Guid? workflowDefinitionId,
        WorkflowDbContext db,
        CancellationToken ct)
    {
        var (allActivities, error) = await LoadActivitiesAsync(workflowDefinitionId, db, ct);
        if (error is not null)
            return error;

        var activities = allActivities!
            .Where(a => a.Type is ActivityTypes.TaskActivity or ActivityTypes.FanOutTaskActivity
                        or ActivityTypes.InternalFollowupSelectionActivity)
            .Select(a => new WorkflowActivityOptionDto(
                a.Id,
                a.Type,
                string.IsNullOrEmpty(a.Name) ? a.Id : a.Name,
                JsonPropertyReader.GetString(a.Properties, "assigneeGroup"),
                JsonPropertyReader.GetStringList(a.Properties, "initialAssignmentStrategies"),
                JsonPropertyReader.GetStringList(a.Properties, "revisitAssignmentStrategies"),
                JsonPropertyReader.GetString(a.Properties, JsonPropertyReader.SameAssigneeAsActivityKey)))
            .ToList();

        return Results.Ok(activities);
    }

    /// <summary>
    /// Loads all activities of a workflow definition's currently Published version (default: the appraisal workflow), once.
    /// The picker filters them to the assignable types; override validation uses the full list for its
    /// existence and type checks.
    /// </summary>
    private static async Task<(List<ActivityDefinition>? Activities, IResult? Error)> LoadActivitiesAsync(
        Guid? workflowDefinitionId,
        WorkflowDbContext db,
        CancellationToken ct)
    {
        var definitionId = workflowDefinitionId;

        // Default to the appraisal workflow, resolved exactly the way the engine resolves it:
        // by name, highest version (see RequestSubmittedIntegrationEventConsumer). Counting active
        // definitions does not work here — every environment has several (Quotation, Document
        // Followup, Fee Appointment Approval), plus any renamed "_bk*" backups, all left IsActive.
        // The name lookup is what excludes those backups, which is why they are renamed.
        if (definitionId is null)
        {
            // Read-only: projected, untracked reads rather than loading write-side aggregates.
            definitionId = await db.WorkflowDefinitions.AsNoTracking()
                .Where(x => x.Name == AppraisalWorkflowDefinitionSeeder.WorkflowName)
                .OrderByDescending(x => x.Version)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct);

            if (definitionId is null)
                return (null, Results.Problem(
                    detail: $"Workflow definition '{AppraisalWorkflowDefinitionSeeder.WorkflowName}' not found. " +
                            "Pass workflowDefinitionId to target a different workflow.",
                    statusCode: StatusCodes.Status400BadRequest));
        }

        // The engine runs the Published version (see WorkflowEngine.GetCurrentPublishedAsync: Published, newest
        // Version), not a newer unpublished draft.
        var jsonSchema = await db.WorkflowDefinitionVersions.AsNoTracking()
            .Where(v => v.DefinitionId == definitionId.Value && v.Status == VersionStatus.Published)
            .OrderByDescending(v => v.Version)
            .Select(v => v.JsonSchema)
            .FirstOrDefaultAsync(ct);
        if (jsonSchema is null)
            return (null, Results.NotFound());

        WorkflowSchema? schema;
        try
        {
            // Must use the engine's options, not bare PropertyNameCaseInsensitive: TransitionDefinition.Type
            // is an enum stored as the string "Conditional", so without the JsonStringEnumConverter this
            // throws and the picker silently degrades to "activity list unavailable".
            schema = JsonSerializer.Deserialize<WorkflowSchema>(
                jsonSchema,
                WorkflowDefinitionSeedHelper.EngineJsonOptions);
        }
        catch (JsonException)
        {
            return (null, Results.Problem("Workflow definition schema could not be parsed."));
        }

        return (schema?.Activities ?? [], null);
    }

    /// <summary>
    /// Activities of the definition the override applies to, for validation. Null when the definition
    /// can't be loaded, or the stored id is not a GUID (a legacy row): callers then skip the definition-side
    /// checks rather than block the save or fall back to the default workflow.
    /// </summary>
    private static async Task<List<ActivityDefinition>?> LoadDefinitionActivitiesAsync(
        string? workflowDefinitionId,
        WorkflowDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workflowDefinitionId))
            return (await LoadActivitiesAsync(null, db, ct)).Activities;

        return Guid.TryParse(workflowDefinitionId, out var id) ? (await LoadActivitiesAsync(id, db, ct)).Activities : null;
    }

    // --- validation ---

    /// <summary>Checks that need no workflow definition: ids, segment, strategy names and the source value's shape.</summary>
    internal static string? ValidateBasics(
        string? activityId, string? bankingSegment,
        List<string>? primaryStrategies, List<string>? routeBackStrategies,
        Dictionary<string, object>? additionalConfiguration,
        string? workflowDefinitionId = null)
        // The first failing check wins, so the order below is part of the behaviour.
        => ValidateActivityId(activityId)
           ?? ValidateWorkflowDefinitionId(workflowDefinitionId)
           ?? ValidateBankingSegment(bankingSegment)
           ?? ValidateStrategyNames(primaryStrategies, routeBackStrategies)
           ?? ValidateSourceKey(additionalConfiguration)
           ?? ValidateSourceShape(additionalConfiguration, activityId);

    private static string? ValidateActivityId(string? activityId)
        => activityId is not null && string.IsNullOrWhiteSpace(activityId) ? "ActivityId is required." : null;

    // The runtime lookup compares against Guid.ToString() ("D" format), so anything else would never match.
    // Only Create can send one; Update has none, so an old malformed row can still be edited or disabled.
    private static string? ValidateWorkflowDefinitionId(string? workflowDefinitionId)
        => !string.IsNullOrEmpty(workflowDefinitionId) && !Guid.TryParseExact(workflowDefinitionId, "D", out _)
            ? "WorkflowDefinitionId must be a GUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) or empty."
            : null;

    private static string? ValidateBankingSegment(string? bankingSegment)
        => !string.IsNullOrWhiteSpace(bankingSegment) && !AllowedSegments.Contains(bankingSegment)
            ? "BankingSegment must be 'Retail', 'IBG', or empty."
            : null;

    private static string? ValidateStrategyNames(List<string>? primaryStrategies, List<string>? routeBackStrategies)
    {
        foreach (var strategy in (primaryStrategies ?? []).Concat(routeBackStrategies ?? []))
        {
            try
            {
                AssignmentStrategyExtensions.FromString(strategy);
            }
            catch (ArgumentException)
            {
                return $"Invalid assignment strategy: {strategy}";
            }
        }

        return null;
    }

    // Nothing reads a differently-cased key, so it would silently skip every check that follows.
    private static string? ValidateSourceKey(Dictionary<string, object>? additionalConfiguration)
        => additionalConfiguration?.Keys.FirstOrDefault(k =>
               string.Equals(k, SourceKey, StringComparison.OrdinalIgnoreCase) && k != SourceKey)
            is { } nearMiss
            ? $"Unknown key '{nearMiss}'; the key is '{SourceKey}'."
            : null;

    private static string? ValidateSourceShape(Dictionary<string, object>? additionalConfiguration, string? activityId)
    {
        if (additionalConfiguration is null || !additionalConfiguration.TryGetValue(SourceKey, out var value))
            return null;

        var isString = value is string or JsonElement { ValueKind: JsonValueKind.String };
        if (!JsonPropertyReader.IsSet(value))
            return isString ? $"{SourceKey} must be a non-empty activity id." : null; // empty/whitespace; null means "not set"

        if (!isString)
            return $"{SourceKey} must be a string activity id.";

        return string.Equals(JsonPropertyReader.GetString(additionalConfiguration, SourceKey), activityId, StringComparison.Ordinal)
            ? $"{SourceKey} cannot be the activity being configured."
            : null;
    }

    /// <summary>The override fields the definition-side checks look at.</summary>
    internal record OverrideToValidate(
        string? ActivityId,
        List<string>? PrimaryStrategies,
        List<string>? RouteBackStrategies,
        Dictionary<string, object>? AdditionalConfiguration,
        string? SpecificAssignee = null,
        string? AssigneeGroup = null,
        bool? TeamConstrained = null,
        List<string>? ExcludeAssigneesFrom = null);

    /// <summary>
    /// Checks that need the workflow definition (all of its activities). <paramref name="definitionActivities"/> is
    /// null when it could not be loaded: skip the definition-side checks rather than block the save.
    /// </summary>
    internal static string? ValidateAgainstDefinition(
        OverrideToValidate o, IReadOnlyList<ActivityDefinition>? definitionActivities)
    {
        var definitionActivity = definitionActivities?.FirstOrDefault(a => a.Id == o.ActivityId);
        var isFollowupSelection = definitionActivity?.Type == ActivityTypes.InternalFollowupSelectionActivity;
        var source = GetSetSource(o.AdditionalConfiguration);

        // The first failing check wins, so the order below is part of the behaviour. The strategy lists are only
        // read from the definition (which may be malformed) once the checks before them have passed.
        return (isFollowupSelection ? ValidateFollowupOverride(o) : null)
               ?? ValidateSourceAgainstDefinition(source, definitionActivities)
               ?? ValidateSourceUse(o, definitionActivity, source, isFollowupSelection);
    }

    private static string? ValidateSourceUse(
        OverrideToValidate o, ActivityDefinition? definitionActivity, string? source, bool isFollowupSelection)
    {
        var usesSameAssignee =
            UsesSameAssignee(o.PrimaryStrategies, o.SpecificAssignee, definitionActivity, "initialAssignmentStrategies")
            || UsesSameAssignee(o.RouteBackStrategies, o.SpecificAssignee, definitionActivity, "revisitAssignmentStrategies");
        var jsonSource = definitionActivity is null
            ? null
            : JsonPropertyReader.GetString(definitionActivity.Properties, SourceKey);

        // The strategy needs a source: from this override, or from the definition activity's JSON. (A
        // followup-selection activity has no strategies, so the rule does not apply to it.)
        if (!isFollowupSelection && source is null && string.IsNullOrWhiteSpace(jsonSource) && usesSameAssignee)
            return $"{SameAssigneeStrategy} needs a source activity ({SourceKey}).";

        // The source only matters when it is actually used: the strategy is in an effective list (a
        // followup selection always reads it, but has no exclusions).
        return ValidateExclusionConflict(o, definitionActivity, source ?? jsonSource, isFollowupSelection || usesSameAssignee);
    }

    private static string? GetSetSource(Dictionary<string, object>? additionalConfiguration)
        => additionalConfiguration is not null
           && additionalConfiguration.TryGetValue(SourceKey, out var value)
           && JsonPropertyReader.IsSet(value)
            ? JsonPropertyReader.GetString(additionalConfiguration, SourceKey)
            : null;

    private static string? ValidateFollowupOverride(OverrideToValidate o)
    {
        if (o.PrimaryStrategies is { Count: > 0 } || o.RouteBackStrategies is { Count: > 0 })
            return "This activity has no assignment strategies; leave the strategy lists empty.";

        // Only values that would take effect: blank strings, an empty list and teamConstrained=false are "unset".
        if (!string.IsNullOrWhiteSpace(o.SpecificAssignee) || !string.IsNullOrWhiteSpace(o.AssigneeGroup)
            || o.TeamConstrained == true || o.ExcludeAssigneesFrom is { Count: > 0 })
            return "This activity only reads sameAssigneeAsActivity; leave specificAssignee, assigneeGroup, "
                   + "teamConstrained and excludeAssigneesFrom empty.";

        return null;
    }

    private static string? ValidateSourceAgainstDefinition(
        string? source, IReadOnlyList<ActivityDefinition>? definitionActivities)
    {
        if (source is null)
            return null;

        var sourceActivity = definitionActivities?.FirstOrDefault(a => a.Id == source);
        if (definitionActivities is not null && sourceActivity is null)
            return $"Unknown activity for {SourceKey}: {source}";

        // Only a TaskActivity's CompletedBy is a person: automatic activities complete as SYSTEM, and a
        // fan-out task's CompletedBy is whoever closed the last item.
        return sourceActivity is not null && sourceActivity.Type != ActivityTypes.TaskActivity
            ? $"{SourceKey} must be a TaskActivity: {source}"
            : null;
    }

    // The selector would pick exactly the person an exclusion of the same activity rejects.
    private static string? ValidateExclusionConflict(
        OverrideToValidate o, ActivityDefinition? definitionActivity, string? effectiveSource, bool sourceIsUsed)
    {
        if (!sourceIsUsed || string.IsNullOrWhiteSpace(effectiveSource))
            return null;

        var exclusions = o.ExcludeAssigneesFrom;
        if (exclusions is null && definitionActivity is not null)
            exclusions = ActivityAssignmentRules.Parse(definitionActivity.Properties).ExcludeAssigneesFrom;

        return exclusions?.Contains(effectiveSource) == true
            ? $"{SourceKey} '{effectiveSource}' is also in excludeAssigneesFrom; the person it selects would be rejected."
            : null;
    }

    // Effective strategy list, mirroring AssignmentPipeline.ResolveStrategies: the override's when non-empty, else
    // ["Manual"] when a SpecificAssignee is set, otherwise the definition JSON's.
    private static bool UsesSameAssignee(
        List<string>? overrideStrategies, string? specificAssignee, ActivityDefinition? definitionActivity, string jsonKey)
    {
        List<string> effective;
        if (overrideStrategies is { Count: > 0 })
            effective = overrideStrategies;
        else if (!string.IsNullOrEmpty(specificAssignee))
            effective = ["Manual"];
        else
            effective = definitionActivity is null ? [] : JsonPropertyReader.GetStringList(definitionActivity.Properties, jsonKey);

        return effective.Any(x => string.Equals(x, SameAssigneeStrategy, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Activity option for the admin picker, with the JSON-definition baseline being overridden.</summary>
public record WorkflowActivityOptionDto(
    string Id,
    string Type,
    string Name,
    string? AssigneeGroup,
    List<string> InitialAssignmentStrategies,
    List<string> RevisitAssignmentStrategies,
    string? SameAssigneeAsActivity);
