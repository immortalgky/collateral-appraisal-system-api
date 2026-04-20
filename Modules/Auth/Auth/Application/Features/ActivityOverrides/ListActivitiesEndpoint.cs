using Auth.Domain.Menu;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Features.ActivityOverrides;

/// <summary>
/// Admin endpoint: returns the list of known activity IDs derived from Main-scope
/// task menu items whose Path contains <c>?activityId=</c>. This keeps a single
/// source of truth for activity identifiers (the seeded task menu items) without
/// a separate activity catalog table.
/// </summary>
public class ListActivitiesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/activities",
                async (AuthDbContext dbContext, CancellationToken cancellationToken) =>
                {
                    var taskItems = await dbContext.MenuItems
                        .Include(m => m.Translations)
                        .Where(m => m.Scope == MenuScope.Main
                                    && m.Path != null
                                    && m.Path.Contains("activityId="))
                        .ToListAsync(cancellationToken);

                    var activities = taskItems
                        .Select(item => new
                        {
                            ActivityId = ExtractActivityId(item.Path!),
                            Label = item.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Label ?? item.ItemKey,
                        })
                        .Where(x => !string.IsNullOrEmpty(x.ActivityId))
                        .GroupBy(x => x.ActivityId)
                        .Select(g => new ActivitySummaryDto(g.Key!, g.First().Label))
                        .OrderBy(a => a.Label)
                        .ToList();

                    return Results.Ok(activities);
                })
            .WithName("ListActivities")
            .Produces<List<ActivitySummaryDto>>()
            .WithSummary("List known workflow activities (admin)")
            .WithDescription("Returns activity IDs derived from task menu item paths (?activityId=...).")
            .WithTags("ActivityOverrides")
            .RequireAuthorization("CanManageMenus");
    }

    private static string? ExtractActivityId(string path)
    {
        // Expected format: /tasks?activityId=appraisal-initiation
        var marker = "activityId=";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var tail = path[(idx + marker.Length)..];
        var ampIdx = tail.IndexOf('&');
        return ampIdx >= 0 ? tail[..ampIdx] : tail;
    }
}
