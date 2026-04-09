using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.ListRunningInstances;

/// <summary>
/// Lists Running workflow instances for a definition, optionally filtered to a specific version.
/// Used by the workflow builder migration screen.
/// </summary>
public class ListRunningInstancesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/definitions/{definitionId:guid}/instances", async (
                Guid definitionId,
                Guid? versionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListRunningInstancesQuery(definitionId, versionId), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("ListRunningInstances")
            .WithTags("Workflows");
    }
}

public record ListRunningInstancesQuery(Guid DefinitionId, Guid? VersionId)
    : IRequest<IReadOnlyList<RunningInstanceDto>>;

public record RunningInstanceDto(
    Guid Id,
    string Name,
    string CurrentActivityId,
    DateTime StartedOn,
    Guid WorkflowDefinitionVersionId,
    string Status);

public class ListRunningInstancesQueryHandler
    : IRequestHandler<ListRunningInstancesQuery, IReadOnlyList<RunningInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _instanceRepository;

    public ListRunningInstancesQueryHandler(IWorkflowInstanceRepository instanceRepository)
    {
        _instanceRepository = instanceRepository;
    }

    public async Task<IReadOnlyList<RunningInstanceDto>> Handle(
        ListRunningInstancesQuery request, CancellationToken cancellationToken)
    {
        var instances = request.VersionId.HasValue
            ? await _instanceRepository.ListRunningByVersionIdAsync(request.VersionId.Value, cancellationToken)
            : await _instanceRepository.ListRunningByDefinitionIdAsync(request.DefinitionId, cancellationToken);

        return instances
            .Select(i => new RunningInstanceDto(
                i.Id,
                i.Name,
                i.CurrentActivityId,
                i.StartedOn,
                i.WorkflowDefinitionVersionId,
                i.Status.ToString()))
            .ToList();
    }
}
