using Assignment.Workflow.Engine;
using Assignment.Workflow.Models;
using Assignment.Workflow.Repositories;
using Assignment.Workflow.Schema;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

namespace Assignment.Workflow.Features.CreateWorkflowDefinition;

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
                CreatedBy = request.CreatedBy // In real app, get from current user context
            };
            
            var result = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/workflows/definitions/{result.Id}", result);
        })
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
    public string Name { get; init; } = default!;
    public int Version { get; init; }
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
}

public class CreateWorkflowDefinitionCommandHandler : IRequestHandler<CreateWorkflowDefinitionCommand, CreateWorkflowDefinitionResponse>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly IWorkflowEngine _workflowEngine;

    public CreateWorkflowDefinitionCommandHandler(
        IWorkflowDefinitionRepository repository,
        IWorkflowEngine workflowEngine)
    {
        _repository = repository;
        _workflowEngine = workflowEngine;
    }

    public async Task<CreateWorkflowDefinitionResponse> Handle(CreateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // Validate workflow definition
        var isValid = await _workflowEngine.ValidateWorkflowDefinitionAsync(request.WorkflowSchema, cancellationToken);
        if (!isValid)
        {
            return new CreateWorkflowDefinitionResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Workflow definition validation failed" }
            };
        }

        // Check if name already exists
        var existingDefinition = await _repository.GetLatestVersion(request.Name, cancellationToken);
        var version = existingDefinition?.Version + 1 ?? 1;

        // Serialize workflow schema
        var jsonDefinition = JsonSerializer.Serialize(request.WorkflowSchema);

        // Create workflow definition
        var workflowDefinition = WorkflowDefinition.Create(
            request.Name,
            request.Description,
            jsonDefinition,
            request.Category,
            request.CreatedBy);

        await _repository.AddAsync(workflowDefinition, cancellationToken);

        return new CreateWorkflowDefinitionResponse
        {
            Id = workflowDefinition.Id,
            Name = workflowDefinition.Name,
            Version = workflowDefinition.Version,
            IsValid = true
        };
    }
}