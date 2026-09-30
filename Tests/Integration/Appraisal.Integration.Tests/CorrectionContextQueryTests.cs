using Appraisal.Application.Features.Appraisals.NotifyExternalSystem;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// AppraisalExternalSourceQuery is static Dapper over three modules' tables (appraisal, request, integration),
/// bound to a positional record. Run it against the real schema so a reordered SELECT, a renamed column or a
/// missing table fails here rather than taking down the page header, notify and regenerate at runtime.
/// </summary>
[Collection("Integration")]
public class CorrectionContextQueryTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Returns_the_approval_time_and_no_source_for_an_appraisal_without_a_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var approvedAt = new DateTime(2026, 9, 15, 14, 30, 0);
        Guid appraisalId;

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"CTX-{Guid.NewGuid():N}"[..18]);
            appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
            appraisal.MarkApprovedByCommittee("COM01", approvedAt);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(ct);
            appraisalId = appraisal.Id;
        }

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();

            var context = await AppraisalExternalSourceQuery.GetAsync(connection, appraisalId, ct);

            Assert.NotNull(context);
            Assert.Null(context.ExternalSystem); // no request row → nobody to notify
            Assert.Equal(approvedAt, context.CompletedAt);

            Assert.Null(await AppraisalExternalSourceQuery.GetAsync(connection, Guid.NewGuid(), ct));
        }
    }
}
