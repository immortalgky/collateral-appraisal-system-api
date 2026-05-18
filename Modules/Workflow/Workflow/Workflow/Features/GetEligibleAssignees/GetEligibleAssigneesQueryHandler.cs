using Dapper;
using Shared.Data;
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
    private readonly ISqlConnectionFactory _connectionFactory;

    public GetEligibleAssigneesQueryHandler(
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowDefinitionRepository definitionRepository,
        IAssignmentPipeline pipeline,
        ITeamService teamService,
        ISqlConnectionFactory connectionFactory)
    {
        _instanceRepository = instanceRepository;
        _definitionRepository = definitionRepository;
        _pipeline = pipeline;
        _teamService = teamService;
        _connectionFactory = connectionFactory;
    }

    public async Task<GetEligibleAssigneesResponse> Handle(
        GetEligibleAssigneesQuery request, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByIdAsync(request.WorkflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow instance {request.WorkflowInstanceId} not found");

        // Build activity properties from JSON definition
        var properties = ActivityPropertiesExtractor.Extract(instance, request.ActivityId);

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

        // Fetch role descriptions per candidate username so the FE picker can show "Appraisal Staff" etc.
        var usernames = pipelineCtx.CandidatePool.Select(m => m.UserId).Distinct().ToList();
        var rolesByUser = await GetRolesByUsernameAsync(usernames, cancellationToken);

        return new GetEligibleAssigneesResponse
        {
            ActivityId = request.ActivityId,
            TeamId = pipelineCtx.TeamId,
            TeamName = teamName,
            EligibleAssignees = pipelineCtx.CandidatePool
                .Select(m => new EligibleAssigneeDto(m.UserId, m.DisplayName)
                {
                    UserName = m.UserId,
                    Roles = rolesByUser.TryGetValue(m.UserId, out var roles) ? roles : []
                })
                .ToList()
        };
    }

    private async Task<Dictionary<string, List<string>>> GetRolesByUsernameAsync(
        List<string> usernames, CancellationToken cancellationToken)
    {
        if (usernames.Count == 0)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        using var connection = _connectionFactory.GetOpenConnection();
        const string sql = """
            SELECT u.UserName AS UserName,
                   COALESCE(NULLIF(r.Description, ''), r.Name) AS RoleLabel
            FROM auth.AspNetUsers u
            INNER JOIN auth.AspNetUserRoles ur ON ur.UserId = u.Id
            INNER JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
            WHERE u.UserName IN @UserNames
            """;

        var rows = await connection.QueryAsync<(string UserName, string RoleLabel)>(sql, new { UserNames = usernames });

        return rows
            .GroupBy(r => r.UserName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.RoleLabel).Distinct().OrderBy(s => s).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }
}
