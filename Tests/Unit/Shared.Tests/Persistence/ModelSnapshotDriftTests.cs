using FluentAssertions;
using Appraisal.Infrastructure;
using Auth.Infrastructure;
using Collateral.Data;
using Common.Infrastructure;
using Document.Data;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Notification.Data;
using OpenIddict.EntityFrameworkCore;
using Parameter.Data;
using Reporting.Data;
using Request.Infrastructure;
using Workflow.Data;

namespace Shared.Tests.Persistence;

/// <summary>
/// Fails when a module's current EF model differs from its latest migration snapshot: an entity or
/// configuration changed without a migration, or two PRs each added a migration to the same module and the
/// merged snapshot no longer matches the model. Compares in memory (no database is opened).
/// To fix a failure: dotnet ef migrations add &lt;Name&gt; --project Modules/&lt;Module&gt; --startup-project Bootstrapper/Api
/// </summary>
public class ModelSnapshotDriftTests
{
    // Every DbContext that has a Migrations folder + *ModelSnapshot.cs. Add new module contexts here.
    public static TheoryData<Type> DbContextTypes =>
    [
        typeof(AppraisalDbContext), typeof(AuthDbContext), typeof(CollateralDbContext), typeof(CommonDbContext),
        typeof(DocumentDbContext), typeof(IntegrationDbContext), typeof(NotificationDbContext),
        typeof(ParameterDbContext), typeof(ReportingDbContext), typeof(RequestDbContext), typeof(WorkflowDbContext)
    ];

    [Theory]
    [MemberData(nameof(DbContextTypes))]
    public void Model_matches_latest_migration_snapshot(Type contextType)
    {
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        builder.UseSqlServer("Server=unused;Database=unused");
        if (contextType == typeof(AuthDbContext)) builder.UseOpenIddict(); // as AuthModule does

        using var db = (DbContext)Activator.CreateInstance(contextType, builder.Options)!;

        db.Database.HasPendingModelChanges().Should().BeFalse(
            $"{contextType.Name}'s model has changes not captured by its migrations; add a migration for the module");
    }
}
