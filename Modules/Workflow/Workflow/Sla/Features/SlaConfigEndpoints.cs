using Carter;
using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Sla.Models;

namespace Workflow.Sla.Features;

public class SlaConfigEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sla/configurations").WithTags("SLA Configuration");

        group.MapGet("/", GetConfigurations);
        group.MapPost("/", CreateConfiguration);
        group.MapPut("/{id:guid}", UpdateConfiguration);
        group.MapDelete("/{id:guid}", DeleteConfiguration);

        // Workflow SLA configs
        var wfGroup = app.MapGroup("/api/sla/workflow-configurations").WithTags("SLA Configuration");
        wfGroup.MapGet("/", GetWorkflowConfigurations);
        wfGroup.MapPost("/", CreateWorkflowConfiguration);

        // Holidays
        var holidays = app.MapGroup("/api/sla/holidays").WithTags("SLA Configuration");
        holidays.MapGet("/", GetHolidays);
        holidays.MapPost("/", CreateHoliday);
        holidays.MapDelete("/{id:guid}", DeleteHoliday);

        // Business hours
        var bh = app.MapGroup("/api/sla/business-hours").WithTags("SLA Configuration");
        bh.MapGet("/", GetBusinessHours);
        bh.MapPost("/", UpsertBusinessHours);
    }

    // --- SLA Configurations ---
    private static async Task<IResult> GetConfigurations(WorkflowDbContext db)
    {
        var configs = await db.SlaConfigurations
            .AsNoTracking()
            .OrderBy(c => c.Priority)
            .Select(c => new SlaConfigDto(
                c.Id, c.ActivityId, c.WorkflowDefinitionId, c.CompanyId,
                c.LoanType, c.DurationHours, c.UseBusinessDays, c.Priority))
            .ToListAsync();
        return Results.Ok(configs);
    }

    private static async Task<IResult> CreateConfiguration(CreateSlaConfigRequest request, WorkflowDbContext db)
    {
        var config = SlaConfiguration.Create(
            request.ActivityId, request.DurationHours, request.UseBusinessDays,
            request.Priority, request.WorkflowDefinitionId, request.CompanyId, request.LoanType);
        db.SlaConfigurations.Add(config);
        await db.SaveChangesAsync();
        return Results.Created($"/api/sla/configurations/{config.Id}",
            new SlaConfigDto(config.Id, config.ActivityId, config.WorkflowDefinitionId,
                config.CompanyId, config.LoanType, config.DurationHours,
                config.UseBusinessDays, config.Priority));
    }

    private static async Task<IResult> UpdateConfiguration(Guid id, UpdateSlaConfigRequest request, WorkflowDbContext db)
    {
        var config = await db.SlaConfigurations.FindAsync(id);
        if (config is null) return Results.NotFound();
        config.Update(request.DurationHours, request.UseBusinessDays, request.Priority,
            request.LoanType, request.CompanyId);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteConfiguration(Guid id, WorkflowDbContext db)
    {
        var config = await db.SlaConfigurations.FindAsync(id);
        if (config is null) return Results.NotFound();
        db.SlaConfigurations.Remove(config);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // --- Workflow SLA Configurations ---
    private static async Task<IResult> GetWorkflowConfigurations(WorkflowDbContext db)
    {
        var configs = await db.WorkflowSlaConfigurations
            .AsNoTracking()
            .OrderBy(c => c.Priority)
            .ToListAsync();
        return Results.Ok(configs);
    }

    private static async Task<IResult> CreateWorkflowConfiguration(
        CreateWorkflowSlaConfigRequest request, WorkflowDbContext db)
    {
        var config = WorkflowSlaConfiguration.Create(
            request.WorkflowDefinitionId, request.TotalDurationHours,
            request.UseBusinessDays, request.Priority, request.LoanType);
        db.WorkflowSlaConfigurations.Add(config);
        await db.SaveChangesAsync();
        return Results.Created($"/api/sla/workflow-configurations/{config.Id}", config);
    }

    // --- Holidays ---
    private static async Task<IResult> GetHolidays(WorkflowDbContext db, int? year)
    {
        var query = db.Holidays.AsNoTracking();
        if (year.HasValue)
            query = query.Where(h => h.Year == year.Value);
        var holidays = await query.OrderBy(h => h.Date).ToListAsync();
        return Results.Ok(holidays);
    }

    private static async Task<IResult> CreateHoliday(CreateHolidayRequest request, WorkflowDbContext db)
    {
        var holiday = Holiday.Create(request.Date, request.Description);
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync();
        return Results.Created($"/api/sla/holidays/{holiday.Id}", holiday);
    }

    private static async Task<IResult> DeleteHoliday(Guid id, WorkflowDbContext db)
    {
        var holiday = await db.Holidays.FindAsync(id);
        if (holiday is null) return Results.NotFound();
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // --- Business Hours ---
    private static async Task<IResult> GetBusinessHours(WorkflowDbContext db)
    {
        var config = await db.BusinessHoursConfigs.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive);
        return config is null ? Results.NotFound() : Results.Ok(config);
    }

    private static async Task<IResult> UpsertBusinessHours(UpsertBusinessHoursRequest request, WorkflowDbContext db)
    {
        var existing = await db.BusinessHoursConfigs.FirstOrDefaultAsync(b => b.IsActive);
        if (existing is not null)
        {
            existing.Update(request.StartTime, request.EndTime, request.TimeZone, true);
        }
        else
        {
            var config = BusinessHoursConfig.Create(request.StartTime, request.EndTime, request.TimeZone);
            db.BusinessHoursConfigs.Add(config);
        }
        await db.SaveChangesAsync();
        return Results.Ok();
    }
}

// Request DTOs
public record CreateSlaConfigRequest(
    string ActivityId, int DurationHours, bool UseBusinessDays, int Priority,
    Guid? WorkflowDefinitionId = null, Guid? CompanyId = null, string? LoanType = null);

public record UpdateSlaConfigRequest(
    int DurationHours, bool UseBusinessDays, int Priority,
    string? LoanType = null, Guid? CompanyId = null);

public record SlaConfigDto(
    Guid Id, string ActivityId, Guid? WorkflowDefinitionId, Guid? CompanyId,
    string? LoanType, int DurationHours, bool UseBusinessDays, int Priority);

public record CreateWorkflowSlaConfigRequest(
    Guid WorkflowDefinitionId, int TotalDurationHours, bool UseBusinessDays, int Priority,
    string? LoanType = null);

public record CreateHolidayRequest(DateOnly Date, string Description);

public record UpsertBusinessHoursRequest(TimeOnly StartTime, TimeOnly EndTime, string TimeZone);
