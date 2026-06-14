using System.Text.Json;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.AssigneeSelection.Core;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow;
using Workflow.Workflow.Features.GetVersions;
using Workflow.Workflow.Features.GetWorkflowDefinitions;
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

    private static async Task<IResult> Create(
        CreateTaskAssignmentConfigurationRequest request,
        ITaskConfigurationService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (Validate(request.ActivityId, request.BankingSegment, request.PrimaryStrategies, request.RouteBackStrategies)
            is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

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
        CancellationToken ct)
    {
        if (Validate(activityId: null, request.BankingSegment, request.PrimaryStrategies, request.RouteBackStrategies)
            is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        if (await service.GetByIdAsync(id, ct) is null)
            return Results.NotFound();

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
        ISender sender,
        CancellationToken ct)
    {
        var definitionId = workflowDefinitionId;

        // Default to the single active definition when not specified.
        if (definitionId is null)
        {
            var definitions = await sender.Send(new GetWorkflowDefinitionsQuery { ActiveOnly = true }, ct);
            if (definitions.Definitions.Count == 1)
                definitionId = definitions.Definitions[0].Id;
            else
                return Results.Problem(
                    detail: "workflowDefinitionId is required (multiple or no active workflow definitions exist).",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var version = await sender.Send(new GetLatestVersionQuery { DefinitionId = definitionId.Value }, ct);
        if (version is null)
            return Results.NotFound();

        WorkflowSchema? schema;
        try
        {
            schema = JsonSerializer.Deserialize<WorkflowSchema>(
                version.JsonSchema,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return Results.Problem("Workflow definition schema could not be parsed.");
        }

        var activities = (schema?.Activities ?? [])
            .Where(a => a.Type is ActivityTypes.TaskActivity or ActivityTypes.FanOutTaskActivity)
            .Select(a => new WorkflowActivityOptionDto(
                a.Id,
                string.IsNullOrEmpty(a.Name) ? a.Id : a.Name,
                JsonPropertyReader.GetString(a.Properties, "assigneeGroup"),
                JsonPropertyReader.GetStringList(a.Properties, "initialAssignmentStrategies"),
                JsonPropertyReader.GetStringList(a.Properties, "revisitAssignmentStrategies")))
            .ToList();

        return Results.Ok(activities);
    }

    // --- validation ---

    private static string? Validate(
        string? activityId, string? bankingSegment,
        List<string>? primaryStrategies, List<string>? routeBackStrategies)
    {
        if (activityId is not null && string.IsNullOrWhiteSpace(activityId))
            return "ActivityId is required.";

        if (!string.IsNullOrWhiteSpace(bankingSegment) && !AllowedSegments.Contains(bankingSegment))
            return "BankingSegment must be 'Retail', 'IBG', or empty.";

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
}

/// <summary>Activity option for the admin picker, with the JSON-definition baseline being overridden.</summary>
public record WorkflowActivityOptionDto(
    string Id,
    string Name,
    string? AssigneeGroup,
    List<string> InitialAssignmentStrategies,
    List<string> RevisitAssignmentStrategies);
