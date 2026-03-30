using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Features.UpdateDraftSchema;

public class UpdateDraftSchemaEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/workflows/definitions/{definitionId:guid}/versions/{versionId:guid}/schema", async (
                Guid definitionId,
                Guid versionId,
                UpdateDraftSchemaRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateDraftSchemaCommand
                {
                    DefinitionId = definitionId,
                    VersionId = versionId,
                    WorkflowSchema = request.WorkflowSchema,
                    UpdatedBy = request.UpdatedBy
                };

                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                    return Results.BadRequest(new { error = result.ErrorMessage });

                return Results.Ok(result);
            })
            .WithName("UpdateDraftSchema")
            .WithTags("Workflows");
    }
}

public record UpdateDraftSchemaRequest
{
    public WorkflowSchema WorkflowSchema { get; init; } = default!;
    public string UpdatedBy { get; init; } = default!;
}

public record UpdateDraftSchemaCommand : IRequest<UpdateDraftSchemaResponse>
{
    public Guid DefinitionId { get; init; }
    public Guid VersionId { get; init; }
    public WorkflowSchema WorkflowSchema { get; init; } = default!;
    public string UpdatedBy { get; init; } = default!;
}

public record UpdateDraftSchemaResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}

public class UpdateDraftSchemaCommandHandler : IRequestHandler<UpdateDraftSchemaCommand, UpdateDraftSchemaResponse>
{
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IWorkflowService _workflowService;

    public UpdateDraftSchemaCommandHandler(
        IWorkflowDefinitionVersionRepository versionRepository,
        IWorkflowService workflowService)
    {
        _versionRepository = versionRepository;
        _workflowService = workflowService;
    }

    public async Task<UpdateDraftSchemaResponse> Handle(UpdateDraftSchemaCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null || version.DefinitionId != request.DefinitionId)
            return new UpdateDraftSchemaResponse { IsSuccess = false, ErrorMessage = "Version not found" };

        // Validate schema
        var isValid = await _workflowService.ValidateWorkflowDefinitionAsync(request.WorkflowSchema, cancellationToken);
        if (!isValid)
            return new UpdateDraftSchemaResponse { IsSuccess = false, ErrorMessage = "Workflow schema validation failed" };

        var jsonSchema = JsonSerializer.Serialize(request.WorkflowSchema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });

        // UpdateSchema throws if version is Published
        try
        {
            version.UpdateSchema(jsonSchema, request.UpdatedBy);
        }
        catch (InvalidOperationException ex)
        {
            return new UpdateDraftSchemaResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }

        await _versionRepository.UpdateAsync(version, cancellationToken);

        return new UpdateDraftSchemaResponse { IsSuccess = true };
    }
}
