using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.Services.Configuration.Models;

namespace Workflow.Services.Configuration;

/// <summary>
/// Admin CRUD for <c>workflow.CompanyRoundRobinConfigurations</c> — the pool of external companies
/// (and their weights) the round-robin may auto-assign to, scoped by loan type. Gated by the existing
/// <c>workflow.admin</c> policy. The company picker for the UI reuses the Auth <c>GET /companies</c>
/// endpoint, so no picker endpoint is defined here.
/// </summary>
public class CompanyRoundRobinConfigAdminEndpoints : ICarterModule
{
    private const string AdminPolicy = "workflow.admin";

    private const string DuplicateScopeMessage =
        "An active pool already exists for this loan-type scope.";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow/company-roundrobin-configs")
            .WithTags("Company Round-Robin Configuration")
            .RequireAuthorization(AdminPolicy);

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> List(
        ICompanyRoundRobinConfigService service,
        CancellationToken ct)
    {
        var configs = await service.ListAsync(ct);
        return Results.Ok(configs);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ICompanyRoundRobinConfigService service,
        CancellationToken ct)
    {
        var config = await service.GetByIdAsync(id, ct);
        return config is null ? Results.NotFound() : Results.Ok(config);
    }

    private static async Task<IResult> Create(
        CreateCompanyRoundRobinConfigurationRequest request,
        ICompanyRoundRobinConfigService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (Validate(request.Entries) is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        request.CreatedBy = currentUser.UserCode ?? "system";
        try
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/workflow/company-roundrobin-configs/{created.Id}", created);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // 2601/2627 = the filtered unique index on (LoanType) where IsActive=1. Any other
            // DbUpdateException propagates to the global handler (500), not mislabelled as a conflict.
            return Results.Problem(detail: DuplicateScopeMessage, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateCompanyRoundRobinConfigurationRequest request,
        ICompanyRoundRobinConfigService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (Validate(request.Entries) is { } error)
            return Results.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        if (await service.GetByIdAsync(id, ct) is null)
            return Results.NotFound();

        request.UpdatedBy = currentUser.UserCode ?? "system";
        try
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return Results.Ok(updated);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Results.Problem(detail: DuplicateScopeMessage, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> Delete(
        Guid id,
        ICompanyRoundRobinConfigService service,
        CancellationToken ct)
    {
        if (await service.GetByIdAsync(id, ct) is null)
            return Results.NotFound();

        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static string? Validate(List<CompanyWeightDto>? entries)
    {
        if (entries is null || entries.Count == 0)
            return "At least one company must be in the pool.";

        if (entries.Any(e => e.CompanyId == Guid.Empty))
            return "Each pool entry must reference a company.";

        if (entries.Any(e => e.Weight < 1))
            return "Weight must be a positive integer.";

        if (entries.Select(e => e.CompanyId).Distinct().Count() != entries.Count)
            return "A company can appear in the pool only once.";

        return null;
    }
}
