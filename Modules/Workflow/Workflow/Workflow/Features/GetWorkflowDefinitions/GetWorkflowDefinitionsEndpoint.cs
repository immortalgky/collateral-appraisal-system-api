using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.GetWorkflowDefinitions;

public class GetWorkflowDefinitionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/definitions", async (
            string? category,
            bool? activeOnly,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetWorkflowDefinitionsQuery
            {
                Category = category,
                ActiveOnly = activeOnly ?? false
            };
            
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetWorkflowDefinitions")
        .WithTags("Workflows");
    }
}

public record GetWorkflowDefinitionsQuery : IRequest<GetWorkflowDefinitionsResponse>
{
    public string? Category { get; init; }
    public bool ActiveOnly { get; init; }
}

public record GetWorkflowDefinitionsResponse
{
    public List<WorkflowDefinitionDto> Definitions { get; init; } = new();
}

public record WorkflowDefinitionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public string Category { get; init; } = default!;
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; } = default!;
}

public class GetWorkflowDefinitionsQueryHandler : IRequestHandler<GetWorkflowDefinitionsQuery, GetWorkflowDefinitionsResponse>
{
    private readonly IWorkflowDefinitionRepository _repository;

    public GetWorkflowDefinitionsQueryHandler(IWorkflowDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetWorkflowDefinitionsResponse> Handle(GetWorkflowDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var definitions = request.Category != null 
            ? await _repository.GetByCategory(request.Category, cancellationToken)
            : request.ActiveOnly 
                ? await _repository.GetActiveDefinitions(cancellationToken)
                : await _repository.GetAllAsync(cancellationToken);

        var dtos = definitions.Select(d => new WorkflowDefinitionDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            Version = d.Version,
            IsActive = d.IsActive,
            Category = d.Category,
            CreatedOn = d.CreatedOn,
            CreatedBy = d.CreatedBy
        }).ToList();

        return new GetWorkflowDefinitionsResponse { Definitions = dtos };
    }
}