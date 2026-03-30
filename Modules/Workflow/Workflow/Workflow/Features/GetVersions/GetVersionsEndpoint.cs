using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.GetVersions;

public class GetVersionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // List all versions for a definition
        app.MapGet("/api/workflows/definitions/{definitionId:guid}/versions", async (
                Guid definitionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetVersionsQuery { DefinitionId = definitionId };
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetVersions")
            .WithTags("Workflows");

        // Get the latest version for a definition
        app.MapGet("/api/workflows/definitions/{definitionId:guid}/latest-version", async (
                Guid definitionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetLatestVersionQuery { DefinitionId = definitionId };
                var result = await sender.Send(query, cancellationToken);

                if (result is null)
                    return Results.NotFound();

                return Results.Ok(result);
            })
            .WithName("GetLatestVersion")
            .WithTags("Workflows");

        // Get a specific version by ID
        app.MapGet("/api/workflows/definitions/{definitionId:guid}/versions/{versionId:guid}", async (
                Guid definitionId,
                Guid versionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetVersionByIdQuery { DefinitionId = definitionId, VersionId = versionId };
                var result = await sender.Send(query, cancellationToken);

                if (result is null)
                    return Results.NotFound();

                return Results.Ok(result);
            })
            .WithName("GetVersionById")
            .WithTags("Workflows");
    }
}

// --- DTOs ---

public record VersionDto
{
    public Guid Id { get; init; }
    public Guid DefinitionId { get; init; }
    public int Version { get; init; }
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string Category { get; init; } = default!;
    public DateTime? PublishedAt { get; init; }
    public string? PublishedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string CreatedBy { get; init; } = default!;
}

public record VersionDetailDto : VersionDto
{
    public string JsonSchema { get; init; } = default!;
}

// --- Queries ---

public record GetVersionsQuery : IRequest<GetVersionsResponse>
{
    public Guid DefinitionId { get; init; }
}

public record GetVersionsResponse
{
    public List<VersionDto> Versions { get; init; } = new();
}

public record GetVersionByIdQuery : IRequest<VersionDetailDto?>
{
    public Guid DefinitionId { get; init; }
    public Guid VersionId { get; init; }
}

public record GetLatestVersionQuery : IRequest<VersionDetailDto?>
{
    public Guid DefinitionId { get; init; }
}

// --- Handlers ---

public class GetVersionsQueryHandler : IRequestHandler<GetVersionsQuery, GetVersionsResponse>
{
    private readonly IWorkflowDefinitionVersionRepository _repository;

    public GetVersionsQueryHandler(IWorkflowDefinitionVersionRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetVersionsResponse> Handle(GetVersionsQuery request, CancellationToken cancellationToken)
    {
        var versions = await _repository.GetAllVersionsAsync(request.DefinitionId, cancellationToken);

        var dtos = versions.Select(v => new VersionDto
        {
            Id = v.Id,
            DefinitionId = v.DefinitionId,
            Version = v.Version,
            Name = v.Name,
            Description = v.Description,
            Status = v.Status.ToString(),
            Category = v.Category,
            PublishedAt = v.PublishedAt,
            PublishedBy = v.PublishedBy,
            CreatedAt = v.CreatedAt,
            CreatedBy = v.CreatedBy ?? string.Empty
        }).ToList();

        return new GetVersionsResponse { Versions = dtos };
    }
}

public class GetVersionByIdQueryHandler : IRequestHandler<GetVersionByIdQuery, VersionDetailDto?>
{
    private readonly IWorkflowDefinitionVersionRepository _repository;

    public GetVersionByIdQueryHandler(IWorkflowDefinitionVersionRepository repository)
    {
        _repository = repository;
    }

    public async Task<VersionDetailDto?> Handle(GetVersionByIdQuery request, CancellationToken cancellationToken)
    {
        var version = await _repository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null || version.DefinitionId != request.DefinitionId)
            return null;

        return new VersionDetailDto
        {
            Id = version.Id,
            DefinitionId = version.DefinitionId,
            Version = version.Version,
            Name = version.Name,
            Description = version.Description,
            Status = version.Status.ToString(),
            Category = version.Category,
            PublishedAt = version.PublishedAt,
            PublishedBy = version.PublishedBy,
            CreatedAt = version.CreatedAt,
            CreatedBy = version.CreatedBy ?? string.Empty,
            JsonSchema = version.JsonSchema
        };
    }
}

public class GetLatestVersionQueryHandler : IRequestHandler<GetLatestVersionQuery, VersionDetailDto?>
{
    private readonly IWorkflowDefinitionVersionRepository _repository;

    public GetLatestVersionQueryHandler(IWorkflowDefinitionVersionRepository repository)
    {
        _repository = repository;
    }

    public async Task<VersionDetailDto?> Handle(GetLatestVersionQuery request, CancellationToken cancellationToken)
    {
        var version = await _repository.GetLatestVersionAsync(request.DefinitionId, cancellationToken);
        if (version is null)
            return null;

        return new VersionDetailDto
        {
            Id = version.Id,
            DefinitionId = version.DefinitionId,
            Version = version.Version,
            Name = version.Name,
            Description = version.Description,
            Status = version.Status.ToString(),
            Category = version.Category,
            PublishedAt = version.PublishedAt,
            PublishedBy = version.PublishedBy,
            CreatedAt = version.CreatedAt,
            CreatedBy = version.CreatedBy ?? string.Empty,
            JsonSchema = version.JsonSchema
        };
    }
}
