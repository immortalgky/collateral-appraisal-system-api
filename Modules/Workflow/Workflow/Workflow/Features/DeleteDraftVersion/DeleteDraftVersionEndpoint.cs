using Workflow.Workflow.Repositories;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Workflow.Workflow.Features.DeleteDraftVersion;

public class DeleteDraftVersionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/workflows/definitions/{definitionId:guid}/versions/{versionId:guid}", async (
                Guid definitionId,
                Guid versionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteDraftVersionCommand
                {
                    DefinitionId = definitionId,
                    VersionId = versionId
                };

                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                    return Results.BadRequest(new { error = result.ErrorMessage });

                return Results.NoContent();
            })
            .WithName("DeleteDraftVersion")
            .WithTags("Workflows");
    }
}

public record DeleteDraftVersionCommand : IRequest<DeleteDraftVersionResponse>
{
    public Guid DefinitionId { get; init; }
    public Guid VersionId { get; init; }
}

public record DeleteDraftVersionResponse
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}

public class DeleteDraftVersionCommandHandler : IRequestHandler<DeleteDraftVersionCommand, DeleteDraftVersionResponse>
{
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;

    public DeleteDraftVersionCommandHandler(IWorkflowDefinitionVersionRepository versionRepository)
    {
        _versionRepository = versionRepository;
    }

    public async Task<DeleteDraftVersionResponse> Handle(DeleteDraftVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null || version.DefinitionId != request.DefinitionId)
            return new DeleteDraftVersionResponse { IsSuccess = false, ErrorMessage = "Version not found" };

        var deleted = await _versionRepository.DeleteAsync(request.VersionId, cancellationToken);
        if (!deleted)
            return new DeleteDraftVersionResponse { IsSuccess = false, ErrorMessage = "Only draft versions can be deleted" };

        return new DeleteDraftVersionResponse { IsSuccess = true };
    }
}
