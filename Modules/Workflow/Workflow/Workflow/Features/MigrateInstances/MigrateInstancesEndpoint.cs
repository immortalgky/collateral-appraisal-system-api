using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using Workflow.Workflow.Versioning;

namespace Workflow.Workflow.Features.MigrateInstances;

public class MigrateInstancesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/definitions/{definitionId:guid}/versions/{targetVersionId:guid}/migrate", async (
                Guid definitionId,
                Guid targetVersionId,
                MigrateInstancesRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new MigrateInstancesCommand(
                    definitionId,
                    targetVersionId,
                    request.SafeInstanceIds ?? Array.Empty<Guid>(),
                    request.ManualRemaps ?? new Dictionary<Guid, string>(),
                    request.MigratedBy);

                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("MigrateInstances")
            .WithTags("Workflows");
    }
}

public record MigrateInstancesRequest(
    Guid[]? SafeInstanceIds,
    Dictionary<Guid, string>? ManualRemaps,
    string MigratedBy);

public record MigrateInstancesCommand(
    Guid DefinitionId,
    Guid TargetVersionId,
    Guid[] SafeInstanceIds,
    Dictionary<Guid, string> ManualRemaps,
    string MigratedBy) : IRequest<MigrateInstancesResponse>;

public record MigrateInstanceResult(
    Guid InstanceId,
    bool Success,
    string? Error,
    string? NewCurrentActivityId);

public record MigrateInstancesResponse(
    int MigratedCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<MigrateInstanceResult> Results);

public class MigrateInstancesCommandHandler
    : IRequestHandler<MigrateInstancesCommand, MigrateInstancesResponse>
{
    private const int BatchSize = 100;

    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IInstanceImpactAnalyzer _analyzer;
    private readonly WorkflowDbContext _dbContext;

    public MigrateInstancesCommandHandler(
        IWorkflowDefinitionVersionRepository versionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowPersistenceService persistenceService,
        IInstanceImpactAnalyzer analyzer,
        WorkflowDbContext dbContext)
    {
        _versionRepository = versionRepository;
        _instanceRepository = instanceRepository;
        _persistenceService = persistenceService;
        _analyzer = analyzer;
        _dbContext = dbContext;
    }

    public async Task<MigrateInstancesResponse> Handle(
        MigrateInstancesCommand request, CancellationToken cancellationToken)
    {
        var results = new List<MigrateInstanceResult>();

        var targetVersion = await _versionRepository.GetByIdAsync(request.TargetVersionId, cancellationToken);
        if (targetVersion is null || targetVersion.DefinitionId != request.DefinitionId)
        {
            return new MigrateInstancesResponse(0, 0, 0, new List<MigrateInstanceResult>
            {
                new(Guid.Empty, false, "Target version not found for this definition", null)
            });
        }

        var targetSchema = await _persistenceService.GetSchemaByVersionIdAsync(targetVersion.Id, cancellationToken);
        if (targetSchema is null)
        {
            return new MigrateInstancesResponse(0, 0, 0, new List<MigrateInstanceResult>
            {
                new(Guid.Empty, false, "Target schema could not be loaded", null)
            });
        }

        var targetActivityIds = targetSchema.Activities.ToDictionary(a => a.Id);
        var startActivity = targetSchema.Activities.FirstOrDefault(a => a.IsStartActivity);
        var reachableFromStart = startActivity is not null
            ? _analyzer.ComputeReachable(targetSchema, startActivity.Id)
            : new HashSet<string>();

        // Collect ids up-front and validate no overlap.
        var safeIds = request.SafeInstanceIds.ToHashSet();
        foreach (var remapId in request.ManualRemaps.Keys)
        {
            if (safeIds.Contains(remapId))
            {
                results.Add(new MigrateInstanceResult(remapId, false,
                    "Instance appears in both SafeInstanceIds and ManualRemaps", null));
            }
        }

        var allInstanceIds = safeIds
            .Union(request.ManualRemaps.Keys)
            .Except(results.Where(r => !r.Success).Select(r => r.InstanceId))
            .ToList();

        var migrated = 0;
        var failed = results.Count;

        // Process in batches of BatchSize — each batch = one transaction.
        for (var offset = 0; offset < allInstanceIds.Count; offset += BatchSize)
        {
            var batchIds = allInstanceIds.Skip(offset).Take(BatchSize).ToList();
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var instanceId in batchIds)
                {
                    var instance = await _instanceRepository.GetByIdAsync(instanceId, cancellationToken);
                    if (instance is null)
                    {
                        results.Add(new MigrateInstanceResult(instanceId, false, "Instance not found", null));
                        failed++;
                        continue;
                    }

                    if (instance.Status != WorkflowStatus.Running)
                    {
                        results.Add(new MigrateInstanceResult(instanceId, false,
                            $"Instance is not Running (status={instance.Status})", null));
                        failed++;
                        continue;
                    }

                    if (instance.WorkflowDefinitionVersionId == request.TargetVersionId)
                    {
                        results.Add(new MigrateInstanceResult(instanceId, false,
                            "Instance is already on the target version", null));
                        failed++;
                        continue;
                    }

                    string? remap = null;
                    if (request.ManualRemaps.TryGetValue(instanceId, out var remapTarget))
                    {
                        if (!targetActivityIds.TryGetValue(remapTarget, out var targetActivity))
                        {
                            results.Add(new MigrateInstanceResult(instanceId, false,
                                $"Remap target '{remapTarget}' does not exist in target schema", null));
                            failed++;
                            continue;
                        }

                        if (targetActivity.IsEndActivity)
                        {
                            results.Add(new MigrateInstanceResult(instanceId, false,
                                $"Remap target '{remapTarget}' is a terminal/end activity", null));
                            failed++;
                            continue;
                        }

                        if (!reachableFromStart.Contains(remapTarget))
                        {
                            results.Add(new MigrateInstanceResult(instanceId, false,
                                $"Remap target '{remapTarget}' is not reachable from the start activity", null));
                            failed++;
                            continue;
                        }

                        remap = remapTarget;
                    }

                    try
                    {
                        instance.MigrateToVersion(request.TargetVersionId, remap);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);
                        results.Add(new MigrateInstanceResult(instanceId, true, null,
                            instance.CurrentActivityId));
                        migrated++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        results.Add(new MigrateInstanceResult(instanceId, false, ex.Message, null));
                        failed++;
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return new MigrateInstancesResponse(migrated, failed, 0, results);
    }
}
