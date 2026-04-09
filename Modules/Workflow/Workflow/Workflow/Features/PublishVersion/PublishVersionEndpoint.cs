using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Workflow.Workflow.Services;
using Workflow.Workflow.Versioning;
using Workflow.Workflow.Versioning.SchemaDiffing;
using BreakingChange = Workflow.Workflow.Models.BreakingChange;

namespace Workflow.Workflow.Features.PublishVersion;

public class PublishVersionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/definitions/{definitionId:guid}/versions/{versionId:guid}/publish", async (
                Guid definitionId,
                Guid versionId,
                PublishVersionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new PublishVersionCommand
                {
                    DefinitionId = definitionId,
                    VersionId = versionId,
                    PublishedBy = request.PublishedBy,
                    ConfirmedBreakingChangeHash = request.ConfirmedBreakingChangeHash
                };

                var result = await sender.Send(command, cancellationToken);

                if (result.IsHashMismatch)
                    return Results.Conflict(new
                    {
                        error = "Breaking change hash mismatch — re-preview required",
                        impactReport = result.ImpactReport
                    });

                if (!result.IsSuccess)
                    return Results.BadRequest(new { error = result.ErrorMessage });

                return Results.Ok(result);
            })
            .WithName("PublishVersion")
            .WithTags("Workflows");
    }
}

public record PublishVersionRequest
{
    public string PublishedBy { get; init; } = default!;
    public string? ConfirmedBreakingChangeHash { get; init; }
}

public record PublishVersionCommand : IRequest<PublishVersionResponse>
{
    public Guid DefinitionId { get; init; }
    public Guid VersionId { get; init; }
    public string PublishedBy { get; init; } = default!;
    public string? ConfirmedBreakingChangeHash { get; init; }
}

public record PublishVersionResponse
{
    public bool IsSuccess { get; init; }
    public bool IsHashMismatch { get; init; }
    public int Version { get; init; }
    public Guid VersionId { get; init; }
    public string? ErrorMessage { get; init; }
    public PublishImpactReport? ImpactReport { get; init; }
}

public class PublishVersionCommandHandler : IRequestHandler<PublishVersionCommand, PublishVersionResponse>
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowSchemaDiffer _differ;
    private readonly IInstanceImpactAnalyzer _analyzer;

    public PublishVersionCommandHandler(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowDefinitionVersionRepository versionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowPersistenceService persistenceService,
        IWorkflowSchemaDiffer differ,
        IInstanceImpactAnalyzer analyzer)
    {
        _definitionRepository = definitionRepository;
        _versionRepository = versionRepository;
        _instanceRepository = instanceRepository;
        _persistenceService = persistenceService;
        _differ = differ;
        _analyzer = analyzer;
    }

    public async Task<PublishVersionResponse> Handle(PublishVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null || version.DefinitionId != request.DefinitionId)
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = "Version not found" };

        var draftSchema = await _persistenceService.GetSchemaByVersionIdAsync(version.Id, cancellationToken);
        if (draftSchema is null)
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = "Draft schema could not be loaded" };

        // Resolve the currently Published version BEFORE we mutate state — needed for both the diff
        // and (later) to deprecate it.
        var priorPublished = await _versionRepository.GetCurrentPublishedAsync(request.DefinitionId, cancellationToken);

        IReadOnlyList<BreakingChange> changes = Array.Empty<BreakingChange>();
        PublishImpactReport? impactReport = null;

        if (priorPublished is not null && priorPublished.Id != version.Id)
        {
            var oldSchema =
                await _persistenceService.GetSchemaByVersionIdAsync(priorPublished.Id, cancellationToken);
            if (oldSchema is null)
                return new PublishVersionResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Prior published schema could not be loaded"
                };

            changes = _differ.Diff(oldSchema, draftSchema);
            var running =
                await _instanceRepository.ListRunningByVersionIdAsync(priorPublished.Id, cancellationToken);
            impactReport = _analyzer.Analyze(running, oldSchema, draftSchema, changes);

            // Concurrency gate — the operator must have confirmed the current hash.
            if (!string.Equals(impactReport.BreakingChangeHash, request.ConfirmedBreakingChangeHash,
                    StringComparison.Ordinal))
            {
                return new PublishVersionResponse
                {
                    IsSuccess = false,
                    IsHashMismatch = true,
                    ErrorMessage = "Breaking change hash mismatch — re-preview required",
                    ImpactReport = impactReport
                };
            }
        }

        // Stamp the (possibly empty) breaking-change list onto the version, then publish.
        try
        {
            version.SetBreakingChanges(changes.ToList());
            version.Publish(request.PublishedBy);
        }
        catch (InvalidOperationException ex)
        {
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }

        // Deprecate the prior Published version so UI / listings reflect lifecycle.
        // Running instances continue to resolve their schema by version id, so resume keeps working.
        if (priorPublished is not null && priorPublished.Id != version.Id)
        {
            try
            {
                priorPublished.Deprecate(request.PublishedBy, reason: $"Superseded by v{version.Version}");
            }
            catch (InvalidOperationException)
            {
                // Already deprecated / archived — non-fatal.
            }
        }

        // Sync to WorkflowDefinition for legacy lookups (no longer the engine's schema source).
        var definition = await _definitionRepository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = "Workflow definition not found" };

        definition.UpdateDefinition(
            name: version.Name,
            description: version.Description,
            jsonDefinition: version.JsonSchema,
            category: version.Category,
            updatedBy: request.PublishedBy);

        await _definitionRepository.SaveChangesAsync(cancellationToken);

        return new PublishVersionResponse
        {
            IsSuccess = true,
            Version = version.Version,
            VersionId = version.Id,
            ImpactReport = impactReport
        };
    }
}
