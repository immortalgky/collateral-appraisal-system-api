using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Workflow.Data.Seed;

/// <summary>
/// Read-only boot check for the appraisal workflow's activity pipeline.
///
/// Data seeding is disabled outside Development (see MigrationExtension.UseDataSeeding), so on
/// UAT/production these rows arrive as a one-off script in Database/Migration/Scripts/. That trade is
/// deliberate — a seeder that re-inserts missing rows also re-inserts rows an admin deleted — but it
/// removes the accidental safety net the additive seeder used to provide, and a missing row here is
/// not cosmetic: without ValidateTaskOwnership / the appraisal-creation steps, an activity silently
/// skips its gate, or no appraisal is created at workflow start at all.
///
/// So this asserts instead of writing. It logs CRITICAL naming every missing pair rather than throwing,
/// because one absent optional validation step should not stop a whole cluster from booting — the log
/// is the signal that a Migration/Scripts script was forgotten.
/// </summary>
public static class ActivityProcessConfigurationAssertion
{
    public static IApplicationBuilder UseActivityProcessAssertion(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ActivityProcessConfigurationAssertion));

        var present = context.ActivityProcessConfigurations
            .Select(c => new { c.ActivityName, c.ProcessorName })
            .ToList()
            .Select(x => (x.ActivityName, x.ProcessorName))
            .ToHashSet();

        var required = ActivityProcessConfigurationSeeder.RequiredPairs();
        var missing = required
            .Where(p => !present.Contains(p))
            .ToList();

        if (missing.Count == 0)
        {
            // Log the success case too — a silent check is indistinguishable from one that never ran.
            logger.LogInformation(
                "Activity-process configuration verified: all {Required} required row(s) present ({Total} total).",
                required.Count, present.Count);
            return app;
        }

        logger.LogCritical(
            "{Count} required activity-process configuration row(s) are missing from " +
            "workflow.ActivityProcessConfigurations — the affected activities will skip their validation " +
            "steps. Apply the Database/Migration/Scripts/ script that ships them. Missing: {Missing}",
            missing.Count,
            string.Join(", ", missing.Select(p => $"{p.ActivityName}/{p.ProcessorName}")));

        return app;
    }
}
