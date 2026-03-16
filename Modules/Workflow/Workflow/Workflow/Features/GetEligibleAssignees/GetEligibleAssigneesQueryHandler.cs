using System.Text.Json;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.GetEligibleAssignees;

public class GetEligibleAssigneesQueryHandler
    : IRequestHandler<GetEligibleAssigneesQuery, GetEligibleAssigneesResponse>
{
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IAssignmentPipeline _pipeline;
    private readonly ITeamService _teamService;

    public GetEligibleAssigneesQueryHandler(
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowDefinitionRepository definitionRepository,
        IAssignmentPipeline pipeline,
        ITeamService teamService)
    {
        _instanceRepository = instanceRepository;
        _definitionRepository = definitionRepository;
        _pipeline = pipeline;
        _teamService = teamService;
    }

    public async Task<GetEligibleAssigneesResponse> Handle(
        GetEligibleAssigneesQuery request, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(request.WorkflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {request.WorkflowInstanceId} not found");

        // Build activity properties from JSON definition
        var properties = ExtractActivityProperties(instance, request.ActivityId);

        var activityContext = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = request.ActivityId,
            Properties = properties,
            Variables = instance.Variables,
            InputData = new Dictionary<string, object>(),
            CurrentAssignee = instance.CurrentAssignee,
            CancellationToken = cancellationToken,
            WorkflowInstance = instance
        };

        // Reuse pipeline stages 1-2
        var pipelineCtx = await _pipeline.GetEligibleAssigneesAsync(activityContext, cancellationToken);

        // Look up team name if TeamId is set
        string? teamName = null;
        if (!string.IsNullOrEmpty(pipelineCtx.TeamId))
        {
            var team = await _teamService.GetTeamByIdAsync(pipelineCtx.TeamId, cancellationToken);
            teamName = team?.Name;
        }

        return new GetEligibleAssigneesResponse
        {
            ActivityId = request.ActivityId,
            TeamId = pipelineCtx.TeamId,
            TeamName = teamName,
            EligibleAssignees = pipelineCtx.CandidatePool
                .Select(m => new EligibleAssigneeDto(m.UserId, m.DisplayName))
                .ToList()
        };
    }

    private static Dictionary<string, object> ExtractActivityProperties(WorkflowInstance instance, string activityId)
    {
        var definition = instance.WorkflowDefinition;
        if (definition is null || string.IsNullOrEmpty(definition.JsonDefinition))
            return new Dictionary<string, object>();

        try
        {
            using var doc = JsonDocument.Parse(definition.JsonDefinition);
            var root = doc.RootElement;

            if (!root.TryGetProperty("activities", out var activities) || activities.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, object>();

            foreach (var activity in activities.EnumerateArray())
            {
                if (activity.TryGetProperty("id", out var idProp) && idProp.GetString() == activityId)
                {
                    var props = new Dictionary<string, object>();

                    if (activity.TryGetProperty("properties", out var propElement))
                    {
                        foreach (var p in propElement.EnumerateObject())
                        {
                            props[p.Name] = p.Value.Clone();
                        }
                    }

                    // Also extract assignmentRules into properties
                    if (activity.TryGetProperty("assignmentRules", out var rulesElement))
                    {
                        props["assignmentRules"] = rulesElement.Clone();
                    }

                    return props;
                }
            }
        }
        catch
        {
            // If JSON parsing fails, return empty
        }

        return new Dictionary<string, object>();
    }
}
