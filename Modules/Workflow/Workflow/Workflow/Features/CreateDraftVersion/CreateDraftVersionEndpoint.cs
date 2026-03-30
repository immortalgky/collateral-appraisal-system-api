using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.CreateDraftVersion;

public class CreateDraftVersionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/definitions/{definitionId:guid}/versions", async (
                Guid definitionId,
                CreateDraftVersionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateDraftVersionCommand
                {
                    DefinitionId = definitionId,
                    CreatedBy = request.CreatedBy
                };

                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                    return Results.BadRequest(new { error = result.ErrorMessage });

                return Results.Created(
                    $"/api/workflows/definitions/{definitionId}/versions/{result.VersionId}",
                    result);
            })
            .WithName("CreateDraftVersion")
            .WithTags("Workflows");
    }
}

public record CreateDraftVersionRequest
{
    public string CreatedBy { get; init; } = default!;
}

public record CreateDraftVersionCommand : IRequest<CreateDraftVersionResponse>
{
    public Guid DefinitionId { get; init; }
    public string CreatedBy { get; init; } = default!;
}

public record CreateDraftVersionResponse
{
    public bool IsSuccess { get; init; }
    public Guid VersionId { get; init; }
    public int Version { get; init; }
    public string? ErrorMessage { get; init; }
}

public class CreateDraftVersionCommandHandler : IRequestHandler<CreateDraftVersionCommand, CreateDraftVersionResponse>
{
    private readonly IWorkflowDefinitionRepository _definitionRepository;
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;

    public CreateDraftVersionCommandHandler(
        IWorkflowDefinitionRepository definitionRepository,
        IWorkflowDefinitionVersionRepository versionRepository)
    {
        _definitionRepository = definitionRepository;
        _versionRepository = versionRepository;
    }

    public async Task<CreateDraftVersionResponse> Handle(CreateDraftVersionCommand request,
        CancellationToken cancellationToken)
    {
        // Verify definition exists
        var definition = await _definitionRepository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return new CreateDraftVersionResponse { IsSuccess = false, ErrorMessage = "Workflow definition not found" };

        // Get latest version to determine next version number and copy schema
        var latestVersion = await _versionRepository.GetLatestVersionAsync(request.DefinitionId, cancellationToken);
        var nextVersionNumber = (latestVersion?.Version ?? 0) + 1;

        // Use latest version's schema as starting point, or definition's JSON if no versions exist
        var baseSchema = latestVersion?.JsonSchema ?? definition.JsonDefinition;

        var draftVersion = WorkflowDefinitionVersion.Create(
            definitionId: request.DefinitionId,
            version: nextVersionNumber,
            name: definition.Name,
            description: definition.Description,
            jsonSchema: baseSchema,
            category: definition.Category,
            createdBy: request.CreatedBy);

        await _versionRepository.AddAsync(draftVersion, cancellationToken);

        return new CreateDraftVersionResponse
        {
            IsSuccess = true,
            VersionId = draftVersion.Id,
            Version = draftVersion.Version
        };
    }
}
