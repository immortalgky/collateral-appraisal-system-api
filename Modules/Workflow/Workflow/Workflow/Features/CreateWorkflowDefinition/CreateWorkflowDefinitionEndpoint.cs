using Workflow.Workflow.Services;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

namespace Workflow.Workflow.Features.CreateWorkflowDefinition;

public class CreateWorkflowDefinitionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/definitions", async (
                CreateWorkflowDefinitionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateWorkflowDefinitionCommand
                {
                    Name = request.Name,
                    Description = request.Description,
                    Category = request.Category,
                    WorkflowSchema = request.WorkflowSchema,
                    CreatedBy = request.CreatedBy
                };

                var result = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/workflows/definitions/{result.Id}", result);
            })
            .AllowAnonymous()
            .WithName("CreateWorkflowDefinition")
            .WithTags("Workflows");
    }
}

public record CreateWorkflowDefinitionRequest
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public WorkflowSchema WorkflowSchema { get; init; } = default!;
    public string CreatedBy { get; init; } = default!;
}

public record CreateWorkflowDefinitionCommand : IRequest<CreateWorkflowDefinitionResponse>
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public WorkflowSchema WorkflowSchema { get; init; } = default!;
    public string CreatedBy { get; init; } = default!;
}

public record CreateWorkflowDefinitionResponse
{
    public Guid Id { get; init; }
    public Guid VersionId { get; init; }
    public string Name { get; init; } = default!;
    public int Version { get; init; }
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
}

public class
    CreateWorkflowDefinitionCommandHandler : IRequestHandler<CreateWorkflowDefinitionCommand,
    CreateWorkflowDefinitionResponse>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IWorkflowService _workflowService;

    public CreateWorkflowDefinitionCommandHandler(
        IWorkflowDefinitionRepository repository,
        IWorkflowDefinitionVersionRepository versionRepository,
        IWorkflowService workflowService)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _workflowService = workflowService;
    }

    public async Task<CreateWorkflowDefinitionResponse> Handle(CreateWorkflowDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        // Validate workflow definition
        var isValid = await _workflowService.ValidateWorkflowDefinitionAsync(request.WorkflowSchema, cancellationToken);
        if (!isValid)
        {
            return new CreateWorkflowDefinitionResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Workflow definition validation failed" }
            };
        }

        // Serialize workflow schema with camelCase to match frontend expectations
        var jsonDefinition = JsonSerializer.Serialize(request.WorkflowSchema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

        // Create workflow definition (header + execution copy)
        var workflowDefinition = WorkflowDefinition.Create(
            request.Name,
            request.Description,
            jsonDefinition,
            request.Category,
            request.CreatedBy);

        await _repository.AddAsync(workflowDefinition, cancellationToken);

        // Create v1 Draft version (source of truth for the builder)
        var draftVersion = WorkflowDefinitionVersion.Create(
            definitionId: workflowDefinition.Id,
            version: 1,
            name: request.Name,
            description: request.Description,
            jsonSchema: jsonDefinition,
            category: request.Category,
            createdBy: request.CreatedBy);

        await _versionRepository.AddAsync(draftVersion, cancellationToken);

        return new CreateWorkflowDefinitionResponse
        {
            Id = workflowDefinition.Id,
            VersionId = draftVersion.Id,
            Name = workflowDefinition.Name,
            Version = workflowDefinition.Version,
            IsValid = true
        };
    }
}