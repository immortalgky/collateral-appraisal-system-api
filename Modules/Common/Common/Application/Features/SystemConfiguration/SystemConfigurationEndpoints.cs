using Carter;
using Common.Application.Features.SystemConfiguration.GetSystemConfigurationByKey;
using Common.Application.Features.SystemConfiguration.GetSystemConfigurations;
using Common.Application.Features.SystemConfiguration.UpdateSystemConfiguration;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SystemConfiguration;

/// <summary>
/// Admin endpoints for managing SystemConfiguration rows.
/// Login-only authorization — no additional permission policies required.
/// </summary>
public class SystemConfigurationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ── GET all ───────────────────────────────────────────────────────────
        app.MapGet(
                "/system-configurations",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetSystemConfigurationsQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetSystemConfigurations")
            .Produces<List<SystemConfigurationDto>>()
            .WithSummary("List all system configuration entries")
            .WithTags("SystemConfiguration")
            .RequireAuthorization();

        // ── GET by key ────────────────────────────────────────────────────────
        app.MapGet(
                "/system-configurations/{key}",
                async (string key, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetSystemConfigurationByKeyQuery(key), cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetSystemConfigurationByKey")
            .Produces<SystemConfigurationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single system configuration entry by key")
            .WithTags("SystemConfiguration")
            .RequireAuthorization();

        // ── PUT update ────────────────────────────────────────────────────────
        app.MapPut(
                "/system-configurations/{key}",
                async (
                    string key,
                    UpdateSystemConfigurationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdateSystemConfigurationCommand(
                        key,
                        request.Value,
                        request.Description,
                        request.IsActive);

                    await sender.Send(command, cancellationToken);
                    return Results.Ok();
                })
            .WithName("UpdateSystemConfiguration")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a system configuration entry's value, description, or active state")
            .WithTags("SystemConfiguration")
            .RequireAuthorization();
    }
}

// ── Request DTO ───────────────────────────────────────────────────────────────

public record UpdateSystemConfigurationRequest(
    string Value,
    string? Description,
    bool? IsActive);
