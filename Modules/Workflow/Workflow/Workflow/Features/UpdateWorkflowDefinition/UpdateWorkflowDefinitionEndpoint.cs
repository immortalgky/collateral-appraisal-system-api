using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.UpdateWorkflowDefinition;

public class UpdateWorkflowDefinitionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/workflows/definitions/{definitionId:guid}", async (
                Guid definitionId,
                UpdateWorkflowDefinitionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateWorkflowDefinitionCommand
                {
                    DefinitionId = definitionId,
                    Name = request.Name,
                    Description = request.Description,
                    Category = request.Category,
                    IsActive = request.IsActive,
                    UpdatedBy = request.UpdatedBy
                };

                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                    return Results.BadRequest(new { error = result.ErrorMessage });

                return Results.Ok(result);
            })
            .WithName("UpdateWorkflowDefinition")
            .WithTags("Workflows");
    }
}

public record UpdateWorkflowDefinitionRequest
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public bool IsActive { get; init; }
    public string UpdatedBy { get; init; } = default!;
}

public record UpdateWorkflowDefinitionCommand : IRequest<UpdateWorkflowDefinitionResponse>
{
    public Guid DefinitionId { get; init; }
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public bool IsActive { get; init; }
    public string UpdatedBy { get; init; } = default!;
}

public record UpdateWorkflowDefinitionResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}

public class UpdateWorkflowDefinitionCommandHandler : IRequestHandler<UpdateWorkflowDefinitionCommand, UpdateWorkflowDefinitionResponse>
{
    private readonly IWorkflowDefinitionRepository _repository;

    public UpdateWorkflowDefinitionCommandHandler(IWorkflowDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<UpdateWorkflowDefinitionResponse> Handle(UpdateWorkflowDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var definition = await _repository.GetByIdAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return new UpdateWorkflowDefinitionResponse { IsSuccess = false, ErrorMessage = "Workflow definition not found" };

        // Update metadata only (not the JSON schema — that's managed via versions)
        definition.UpdateDefinition(
            name: request.Name,
            description: request.Description,
            jsonDefinition: definition.JsonDefinition, // Keep existing schema
            category: request.Category,
            updatedBy: request.UpdatedBy);

        definition.SetActive(request.IsActive);

        await _repository.SaveChangesAsync(cancellationToken);

        return new UpdateWorkflowDefinitionResponse { IsSuccess = true };
    }
}
