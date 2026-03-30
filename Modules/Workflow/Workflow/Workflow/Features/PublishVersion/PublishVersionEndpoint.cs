using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
                    PublishedBy = request.PublishedBy
                };

                var result = await sender.Send(command, cancellationToken);

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
}

public record PublishVersionCommand : IRequest<PublishVersionResponse>
{
    public Guid DefinitionId { get; init; }
    public Guid VersionId { get; init; }
    public string PublishedBy { get; init; } = default!;
}

public record PublishVersionResponse
{
    public bool IsSuccess { get; init; }
    public int Version { get; init; }
    public string? ErrorMessage { get; init; }
}

public class PublishVersionCommandHandler : IRequestHandler<PublishVersionCommand, PublishVersionResponse>
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;

    public PublishVersionCommandHandler(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowDefinitionVersionRepository versionRepository)
    {
        _definitionRepository = definitionRepository;
        _versionRepository = versionRepository;
    }

    public async Task<PublishVersionResponse> Handle(PublishVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null || version.DefinitionId != request.DefinitionId)
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = "Version not found" };

        // Publish the version (throws if already published)
        try
        {
            version.Publish(request.PublishedBy);
        }
        catch (InvalidOperationException ex)
        {
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }

        // Sync to WorkflowDefinition for execution engine compatibility
        var definition = await _definitionRepository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return new PublishVersionResponse { IsSuccess = false, ErrorMessage = "Workflow definition not found" };

        definition.UpdateDefinition(
            name: version.Name,
            description: version.Description,
            jsonDefinition: version.JsonSchema,
            category: version.Category,
            updatedBy: request.PublishedBy);

        // Save both in a single transaction
        await _definitionRepository.SaveChangesAsync(cancellationToken);

        return new PublishVersionResponse
        {
            IsSuccess = true,
            Version = version.Version
        };
    }
}
