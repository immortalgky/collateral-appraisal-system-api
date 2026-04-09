using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Workflow.Workflow.Versioning;
using Workflow.Workflow.Versioning.SchemaDiffing;

namespace Workflow.Workflow.Features.PublishVersion;

/// <summary>
/// Read-only preview of the impact of publishing a draft workflow version.
/// Runs the schema diff against the currently-Published version (if any) and classifies
/// running instances as Safe or Unsafe. No state changes.
/// </summary>
public class PreviewPublishEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/definitions/{definitionId:guid}/versions/{versionId:guid}/publish/preview",
                async (
                    Guid definitionId,
                    Guid versionId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new PreviewPublishQuery(definitionId, versionId), cancellationToken);

                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("PreviewPublishVersion")
            .WithTags("Workflows");
    }
}

public record PreviewPublishQuery(Guid DefinitionId, Guid VersionId)
    : IRequest<PublishImpactReport?>;

public class PreviewPublishQueryHandler : IRequestHandler<PreviewPublishQuery, PublishImpactReport?>
{
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowSchemaDiffer _differ;
    private readonly IInstanceImpactAnalyzer _analyzer;

    public PreviewPublishQueryHandler(
        IWorkflowDefinitionVersionRepository versionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowPersistenceService persistenceService,
        IWorkflowSchemaDiffer differ,
        IInstanceImpactAnalyzer analyzer)
    {
        _versionRepository = versionRepository;
        _instanceRepository = instanceRepository;
        _persistenceService = persistenceService;
        _differ = differ;
        _analyzer = analyzer;
    }

    public async Task<PublishImpactReport?> Handle(PreviewPublishQuery request, CancellationToken cancellationToken)
    {
        var draft = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (draft is null || draft.DefinitionId != request.DefinitionId)
            return null;

        var draftSchema = await _persistenceService.GetSchemaByVersionIdAsync(draft.Id, cancellationToken);
        if (draftSchema is null)
            return null;

        var currentPublished =
            await _versionRepository.GetCurrentPublishedAsync(request.DefinitionId, cancellationToken);

        // First publish — no prior version to diff against.
        if (currentPublished is null || currentPublished.Id == draft.Id)
        {
            return new PublishImpactReport(
                BreakingChanges: new List<global::Workflow.Workflow.Models.BreakingChange>(),
                BreakingChangeHash: string.Empty,
                SafeCount: 0,
                UnsafeCount: 0,
                Sample: Array.Empty<InstanceClassification>());
        }

        var oldSchema = await _persistenceService.GetSchemaByVersionIdAsync(currentPublished.Id, cancellationToken);
        if (oldSchema is null)
            return null;

        var changes = _differ.Diff(oldSchema, draftSchema);

        var runningInstances =
            await _instanceRepository.ListRunningByVersionIdAsync(currentPublished.Id, cancellationToken);

        return _analyzer.Analyze(runningInstances, oldSchema, draftSchema, changes);
    }
}
