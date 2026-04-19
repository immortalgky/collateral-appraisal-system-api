using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.ActivityOverrides;

/// <summary>
/// Admin endpoint: returns the full list of appraisal-scope menu items joined
/// with any existing override row for the given activity. Items without an
/// override row come back with <see cref="ActivityOverrideRowDto.HasOverride"/>
/// = false and defaults IsVisible=true / CanEdit=false — making the UI a simple
/// "mark the deviations" experience.
/// </summary>
public class GetActivityOverridesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/activity-menu-overrides/{activityId}",
                async (string activityId, AuthDbContext dbContext, CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(activityId))
                        return Results.BadRequest(new { error = "activityId is required" });

                    var menuItems = await dbContext.MenuItems
                        .AsNoTracking()
                        .Include(m => m.Translations)
                        .Where(m => m.Scope == MenuScope.Appraisal)
                        .OrderBy(m => m.SortOrder)
                        .ToListAsync(cancellationToken);

                    var overrides = await dbContext.ActivityMenuOverrides
                        .AsNoTracking()
                        .Where(o => o.ActivityId == activityId)
                        .ToDictionaryAsync(o => o.MenuItemId, cancellationToken);

                    var rows = menuItems
                        .Select(item =>
                        {
                            var label = item.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Label ?? item.ItemKey;
                            if (overrides.TryGetValue(item.Id, out var ov))
                                return new ActivityOverrideRowDto(item.Id, item.ItemKey, label, ov.IsVisible, ov.CanEdit, HasOverride: true);
                            return new ActivityOverrideRowDto(item.Id, item.ItemKey, label, IsVisible: true, CanEdit: false, HasOverride: false);
                        })
                        .ToList();

                    return Results.Ok(new ActivityOverridesResponse(activityId, rows));
                })
            .WithName("GetActivityOverrides")
            .Produces<ActivityOverridesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get activity menu overrides (admin)")
            .WithTags("ActivityOverrides")
            .RequireAuthorization("CanManageMenus");
    }
}
