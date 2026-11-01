using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Auth.Contracts.Users;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// #623 over real SQL Server: the history / sent queries project PrimitiveCollection JSON columns (SentTo, SentCc,
/// ExtraTo, Cc) and the OwnsMany ToJson Attachments, which InMemory never translates; and the case reader's
/// positional record picks up the appraisal status and current assignment columns.
/// </summary>
[Collection("Integration")]
public class AssignmentEmailQueriesSqlTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static ICurrentUserService Admin()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        return user;
    }

    [Fact]
    public async Task History_and_sent_queries_read_the_json_columns_back_from_sql_server()
    {
        var appraisalId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var sentAt = new DateTime(2026, 10, 31, 10, 0, 0);

        var original = AssignmentEmail.ToSend(appraisalId, assignmentId,
            ["extra@x.test"], [AssignmentEmailCcRoles.Keyer, "cc-literal@x.test"],
            "Subject 1", "<p>{{company}}</p>",
            [new AssignmentEmailAttachment(docA, AssignmentEmailAttachmentSources.Request),
             new AssignmentEmailAttachment(docB, AssignmentEmailAttachmentSources.Upload)]);
        original.Queue("ACME & Co", "Khun A", ["company@x.test", "extra@x.test"],
            ["keyer@x.test", "cc-literal@x.test", "third@x.test"]);
        original.MarkSent(sentAt);

        var resend = AssignmentEmail.Resend(appraisalId, assignmentId, [], [], "Subject 2", "<p>again</p>", []);
        var notSent = AssignmentEmail.NotSent(appraisalId, assignmentId);
        var otherCase = AssignmentEmail.NotSent(Guid.NewGuid(), assignmentId);
        var failed = AssignmentEmail.ToSend(appraisalId, assignmentId, [], [], "Subject 3", "<p>x</p>", []);
        failed.Queue(null, null, ["company@x.test"], []);
        failed.MarkFailed("SMTP 550");

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.AssignmentEmails.AddRange(original, resend, notSent, otherCase, failed);
            await db.SaveChangesAsync(Ct);

            // The audit interceptor stamps one time for the batch: pin distinct times so the order is defined.
            foreach (var (id, at, by) in new[]
                     {
                         (original.Id, new DateTime(2026, 10, 31, 9, 0, 0), (string?)"P0001"),
                         (resend.Id, new DateTime(2026, 10, 31, 11, 0, 0), "P0002"),
                         (notSent.Id, new DateTime(2026, 10, 31, 8, 0, 0), (string?)null),
                         (failed.Id, new DateTime(2026, 10, 31, 7, 0, 0), (string?)null)
                     })
                await db.AssignmentEmails.Where(e => e.Id == id).ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.CreatedAt, at)
                    .SetProperty(e => e.CreatedBy, by), Ct);
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var lookup = Substitute.For<IUserLookupService>();
            lookup.GetByUsernamesAsync(default!, default).ReturnsForAnyArgs(
                new Dictionary<string, UserLookupDto> { ["P0001"] = new("P0001", "Somchai", "Jaidee") });

            var history = await new GetAssignmentEmailHistoryQueryHandler(db, lookup, Admin())
                .Handle(new GetAssignmentEmailHistoryQuery(appraisalId), Ct);

            Assert.Equal([resend.Id, original.Id, notSent.Id, failed.Id], history.Select(h => h.Id));

            var h0 = history[0];
            Assert.True(h0.IsResend);
            Assert.Equal(AssignmentEmailStatus.Pending, h0.Status);
            Assert.Empty(h0.SentTo);
            Assert.Equal(0, h0.CcCount);
            Assert.Equal(0, h0.AttachmentCount);
            Assert.Null(h0.CreatedByName); // P0002 not in lookup

            var h1 = history[1];
            Assert.False(h1.IsResend);
            Assert.Equal(AssignmentEmailStatus.Sent, h1.Status);
            Assert.Equal(sentAt, h1.SentAt);
            Assert.Equal(["company@x.test", "extra@x.test"], h1.SentTo);
            Assert.Equal(3, h1.CcCount);
            Assert.Equal(2, h1.AttachmentCount);
            Assert.Equal("P0001", h1.CreatedBy);
            Assert.Equal("Somchai Jaidee", h1.CreatedByName);
            // #632: the interceptor stamped UpdatedAt on every row, but only a Failed one reports it.
            var stored = await db.AssignmentEmails.AsNoTracking()
                .Where(e => e.Id == original.Id || e.Id == failed.Id)
                .ToDictionaryAsync(e => e.Id, e => e.UpdatedAt, Ct);
            Assert.NotNull(stored[original.Id]); // so the null below is the status gate, not a missing stamp
            Assert.Null(h1.FailureReason);
            Assert.Null(h1.FailedAt);

            var h3 = history[3];
            Assert.Equal(AssignmentEmailStatus.Failed, h3.Status);
            Assert.Equal("SMTP 550", h3.FailureReason);
            Assert.NotNull(stored[failed.Id]);
            Assert.Equal(stored[failed.Id], h3.FailedAt);
            Assert.NotEqual(new DateTime(2026, 10, 31, 7, 0, 0), h3.FailedAt); // not the pinned CreatedAt

            var h2 = history[2];
            Assert.Equal(AssignmentEmailStatus.NotSent, h2.Status);
            Assert.Null(h2.CreatedBy);
            Assert.Empty(h2.SentTo);
            Assert.Equal(0, h2.CcCount);
            Assert.Equal(0, h2.AttachmentCount);

            var reader = scope.ServiceProvider.GetRequiredService<IAssignmentEmailCaseReader>();
            var sentHandler = new GetAssignmentEmailSentQueryHandler(db, reader, Admin());

            var sent = await sentHandler.Handle(new GetAssignmentEmailSentQuery(appraisalId, original.Id), Ct);
            Assert.Equal(AssignmentEmailStatus.Sent, sent.Status);
            Assert.Equal(sentAt, sent.SentAt);
            Assert.Equal(["company@x.test", "extra@x.test"], sent.SentTo);
            Assert.Equal(["keyer@x.test", "cc-literal@x.test", "third@x.test"], sent.SentCc);
            Assert.Equal(["extra@x.test"], sent.ExtraTo);
            Assert.Equal([AssignmentEmailCcRoles.Keyer, "cc-literal@x.test"], sent.Cc);
            Assert.Equal("Subject 1", sent.Subject);
            Assert.Equal("<p>ACME &amp; Co</p>", sent.BodyHtml);
            // No document.Documents rows for these ids: listed without a name (also runs the IN @Ids read).
            Assert.Equal(
                [(docA, AssignmentEmailAttachmentSources.Request), (docB, AssignmentEmailAttachmentSources.Upload)],
                sent.Attachments.Select(a => (a.DocumentId, a.Source)));
            Assert.All(sent.Attachments, a => { Assert.Null(a.FileName); Assert.Equal(0, a.FileSizeBytes); });

            var empty = await sentHandler.Handle(new GetAssignmentEmailSentQuery(appraisalId, notSent.Id), Ct);
            Assert.Empty(empty.SentTo);
            Assert.Empty(empty.SentCc);
            Assert.Empty(empty.ExtraTo);
            Assert.Empty(empty.Cc);
            Assert.Empty(empty.Attachments);

            // Another case's email is not reachable through this appraisal.
            await Assert.ThrowsAsync<NotFoundException>(() =>
                sentHandler.Handle(new GetAssignmentEmailSentQuery(appraisalId, otherCase.Id), Ct));
        }
    }

    [Fact]
    public async Task Case_reader_maps_the_appraisal_status_and_current_assignment()
    {
        Guid appraisalId;
        AppraisalAssignment assignment;
        string status;
        var companyId = Guid.NewGuid().ToString();

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"AE-{Guid.NewGuid():N}"[..14]);
            assignment = appraisal.Assign("External", assigneeUserId: "K0001", assigneeCompanyId: companyId,
                internalAppraiserId: "F0001", assignedBy: "test");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
            status = (await db.Appraisals.AsNoTracking().SingleAsync(a => a.Id == appraisalId, Ct)).Status.Code;
        }

        using (var scope = CreateScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IAssignmentEmailCaseReader>();

            var found = await reader.GetCaseAsync(appraisalId, Ct);

            Assert.NotNull(found);
            Assert.StartsWith("AE-", found.AppraisalNumber);
            Assert.Equal(companyId, found.AssigneeCompanyId);
            Assert.Equal(status, found.AppraisalStatus);
            Assert.Equal(assignment.Id, found.AssignmentId);
            Assert.Equal(assignment.AssignmentType.Code, found.AssignmentType);
            Assert.Equal(assignment.AssignmentStatus.Code, found.AssignmentStatus);
            Assert.Equal(assignment.AssignmentMethod, found.AssignmentMethod);
            // The usernames the resend's keyer / follow-up Cc chips expand from (#621).
            Assert.Equal("K0001", found.KeyerUserId);
            Assert.Equal("F0001", found.FollowupUserId);

            Assert.Null(await reader.GetCaseAsync(Guid.NewGuid(), Ct));
        }
    }

    [Fact]
    public async Task Case_reader_names_a_keyer_or_follower_without_a_name_by_their_username()
    {
        var services = fixture.IntegrationTestWebApplicationFactory.Services;
        var keyer = await global::Integration.Auth.Integration.Tests.Bff.BffTestSupport.CreateUserAsync(
            services, "IntAppraisalStaff", configure: u => (u.FirstName, u.LastName) = ("", ""));
        var follower = await global::Integration.Auth.Integration.Tests.Bff.BffTestSupport.CreateUserAsync(
            services, "IntAppraisalStaff", configure: u => (u.FirstName, u.LastName) = ("วิชัย", "รักงาน"));
        Guid appraisalId;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"AE-{Guid.NewGuid():N}"[..14]);
            appraisal.Assign("External", assigneeUserId: keyer.UserName, assigneeCompanyId: Guid.NewGuid().ToString(),
                internalAppraiserId: follower.UserName, assignedBy: "test");
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        using (var scope = CreateScope())
        {
            var found = await scope.ServiceProvider.GetRequiredService<IAssignmentEmailCaseReader>().GetCaseAsync(appraisalId, Ct);

            Assert.NotNull(found);
            Assert.Equal(keyer.UserName, found.KeyerName);
            Assert.Equal("วิชัย รักงาน", found.FollowupName);
        }
    }
}
