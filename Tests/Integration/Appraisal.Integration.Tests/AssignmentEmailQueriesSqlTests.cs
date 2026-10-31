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

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.AssignmentEmails.AddRange(original, resend, notSent, otherCase);
            await db.SaveChangesAsync(Ct);

            // The audit interceptor stamps one time for the batch: pin distinct times so the order is defined.
            foreach (var (id, at, by) in new[]
                     {
                         (original.Id, new DateTime(2026, 10, 31, 9, 0, 0), (string?)"P0001"),
                         (resend.Id, new DateTime(2026, 10, 31, 11, 0, 0), "P0002"),
                         (notSent.Id, new DateTime(2026, 10, 31, 8, 0, 0), (string?)null)
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

            Assert.Equal([resend.Id, original.Id, notSent.Id], history.Select(h => h.Id));

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
            assignment = appraisal.Assign("External", assigneeCompanyId: companyId, assignedBy: "test");
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

            Assert.Null(await reader.GetCaseAsync(Guid.NewGuid(), Ct));
        }
    }
}
