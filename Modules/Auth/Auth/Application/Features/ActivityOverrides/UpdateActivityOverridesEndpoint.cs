using Auth.Application.Services;
using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.ActivityOverrides;

/// <summary>
/// Admin endpoint: bulk-replace the override rows for a single activity.
///
/// Semantics: the request body is the intended full set of overrides for the
/// activity. Existing rows are updated in place; missing rows are deleted. Rows
/// that exactly match default behavior (IsVisible=true, CanEdit=false) are
/// omitted from storage — the caller can send them without harm and they'll be
/// absent after save. This keeps the table small and makes "revert to role
/// default" a simple UI action (untick both boxes).
/// </summary>
public class UpdateActivityOverridesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/activity-menu-overrides/{activityId}",
                async (
                    string activityId,
                    UpdateActivityOverridesRequest request,
                    AuthDbContext dbContext,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(activityId))
                        return Results.BadRequest(new { error = "activityId is required" });
                    if (request?.Rows is null)
                        return Results.BadRequest(new { error = "rows are required" });

                    // Validate: every MenuItemId must exist and be Appraisal-scope.
                    var requestedIds = request.Rows.Select(r => r.MenuItemId).Distinct().ToList();
                    var validMenuItemIds = await dbContext.MenuItems
                        .Where(m => requestedIds.Contains(m.Id) && m.Scope == MenuScope.Appraisal)
                        .Select(m => m.Id)
                        .ToListAsync(cancellationToken);

                    var invalid = requestedIds.Except(validMenuItemIds).ToList();
                    if (invalid.Count > 0)
                        return Results.BadRequest(new { error = "One or more menuItemIds are not appraisal-scope menu items", invalidMenuItemIds = invalid });

                    var existing = await dbContext.ActivityMenuOverrides
                        .Where(o => o.ActivityId == activityId)
                        .ToListAsync(cancellationToken);
                    var existingByMenuId = existing.ToDictionary(o => o.MenuItemId);

                    var desired = request.Rows
                        // Skip rows that match defaults — no need to persist.
                        .Where(r => !(r.IsVisible && !r.CanEdit))
                        .GroupBy(r => r.MenuItemId)
                        .Select(g => g.Last()) // last write wins if client dupes
                        .ToList();
                    var desiredByMenuId = desired.ToDictionary(r => r.MenuItemId);

                    // Update / insert.
                    foreach (var row in desired)
                    {
                        if (existingByMenuId.TryGetValue(row.MenuItemId, out var current))
                        {
                            current.Update(row.IsVisible, row.CanEdit);
                        }
                        else
                        {
                            dbContext.ActivityMenuOverrides.Add(
                                ActivityMenuOverride.Create(activityId, row.MenuItemId, row.IsVisible, row.CanEdit));
                        }
                    }

                    // Delete rows no longer desired (including any that collapsed to default).
                    foreach (var stale in existing.Where(o => !desiredByMenuId.ContainsKey(o.MenuItemId)))
                    {
                        dbContext.ActivityMenuOverrides.Remove(stale);
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    return Results.Ok(new { success = true, count = desired.Count });
                })
            .WithName("UpdateActivityOverrides")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Replace activity menu overrides (admin)")
            .WithTags("ActivityOverrides")
            .RequireAuthorization("CanManageMenus");
    }
}
