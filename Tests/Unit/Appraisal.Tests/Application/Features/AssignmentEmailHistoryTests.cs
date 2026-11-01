using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Auth.Contracts.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;

namespace Appraisal.Tests.Application.Features;

/// <summary>#623: the slip's "อีเมลถึงบริษัท" history and "ดูฉบับที่ส่ง".</summary>
public class AssignmentEmailHistoryTests
{
    private static readonly Guid AppraisalId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUserLookupService _users = Substitute.For<IUserLookupService>();
    private readonly IAssignmentEmailCaseReader _caseReader = Substitute.For<IAssignmentEmailCaseReader>();

    public AssignmentEmailHistoryTests()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(true);
        _users.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, UserLookupDto> { ["P5229"] = new("P5229", "วิภา", "รักงาน") });
    }

    /// <summary>A row as the pipeline leaves it; audit columns are stamped by an interceptor this bare context lacks.</summary>
    private async Task<AssignmentEmail> SeedAsync(
        AssignmentEmail email, DateTime createdAt, string? createdBy, DateTime? sentAt = null)
    {
        if (sentAt is not null)
        {
            email.Queue("บริษัท แอคมี จำกัด", null, ["jobs@acme.co.th", "extra@acme.co.th"], ["boss@bank.co.th"]);
            email.MarkSent(sentAt.Value);
        }

        _db.AssignmentEmails.Add(email);
        email.CreatedAt = createdAt;
        email.CreatedBy = createdBy;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return email;
    }

    private static AssignmentEmail Chosen(bool resend = false, Guid? appraisalId = null)
    {
        Func<Guid, Guid, IEnumerable<string>, IEnumerable<string>, string, string, IEnumerable<AssignmentEmailAttachment>, AssignmentEmail> create =
            resend ? AssignmentEmail.Resend : AssignmentEmail.ToSend;
        return create(appraisalId ?? AppraisalId, AssignmentId, ["extra@acme.co.th"], [AssignmentEmailCcRoles.IntAdminAll, "boss@bank.co.th"],
            "ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630", "<p>เรียน บริษัท แอคมี จำกัด</p>",
            [new AssignmentEmailAttachment(DocumentId, AssignmentEmailAttachmentSources.Request)]);
    }

    private Task<List<AssignmentEmailHistoryItem>> HistoryAsync() =>
        new GetAssignmentEmailHistoryQueryHandler(_db, _users, _user)
            .Handle(new GetAssignmentEmailHistoryQuery(AppraisalId), CancellationToken.None);

    private Task<AssignmentEmailSentResult> ViewAsync(Guid emailId) =>
        new GetAssignmentEmailSentQueryHandler(_db, _caseReader, _user)
            .Handle(new GetAssignmentEmailSentQuery(AppraisalId, emailId), CancellationToken.None);

    [Fact]
    public async Task History_lists_every_email_of_the_case_newest_first_with_status_and_actor()
    {
        var first = await SeedAsync(Chosen(), new DateTime(2026, 11, 1, 9, 0, 0), "P5229", sentAt: new DateTime(2026, 11, 1, 9, 1, 0));
        var resend = await SeedAsync(Chosen(resend: true), new DateTime(2026, 11, 2, 9, 0, 0), "P5229");
        await SeedAsync(Chosen(appraisalId: Guid.NewGuid()), new DateTime(2026, 11, 3, 9, 0, 0), "P5229"); // another case

        var history = await HistoryAsync();

        Assert.Equal([resend.Id, first.Id], history.Select(h => h.Id));
        var sent = history[1];
        Assert.False(sent.IsResend);
        Assert.Equal(AssignmentEmailStatus.Sent, sent.Status);
        Assert.Equal("P5229", sent.CreatedBy);
        Assert.Equal("วิภา รักงาน", sent.CreatedByName);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 0, 0), sent.CreatedAt);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 1, 0), sent.SentAt);
        Assert.Equal(["jobs@acme.co.th", "extra@acme.co.th"], sent.SentTo);
        Assert.Equal(1, sent.CcCount);
        Assert.Equal(1, sent.AttachmentCount);
        Assert.True(history[0].IsResend);
        Assert.Equal(AssignmentEmailStatus.Pending, history[0].Status);
    }

    [Fact]
    public async Task A_row_the_system_wrote_has_no_actor_name()
    {
        await SeedAsync(Chosen(), new DateTime(2026, 11, 1, 9, 0, 0), createdBy: null);

        var history = await HistoryAsync();

        Assert.Null(Assert.Single(history).CreatedByName);
    }

    [Fact]
    public async Task View_returns_what_went_out()
    {
        var email = await SeedAsync(Chosen(), new DateTime(2026, 11, 1, 9, 0, 0), "P5229", sentAt: new DateTime(2026, 11, 1, 9, 1, 0));
        _caseReader.GetDocumentsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new AssignmentEmailDocument(DocumentId, "deed.pdf", 2048)]);

        var view = await ViewAsync(email.Id);

        Assert.Equal(["jobs@acme.co.th", "extra@acme.co.th"], view.SentTo);
        Assert.Equal(["boss@bank.co.th"], view.SentCc);
        Assert.Equal("ขอนำส่งงานประเมิน ลูกค้าราย นายสมศักดิ์ 69042630", view.Subject);
        Assert.Equal("<p>เรียน บริษัท แอคมี จำกัด</p>", view.BodyHtml);
        Assert.Equal([new AssignmentEmailAttachmentDto(DocumentId, AssignmentEmailAttachmentSources.Request, "deed.pdf", 2048)],
            view.Attachments);
        // What the admin composed, for ส่งอีกครั้ง to pre-fill (chips, not the addresses they became).
        Assert.Equal(["extra@acme.co.th"], view.ExtraTo);
        Assert.Equal([AssignmentEmailCcRoles.IntAdminAll, "boss@bank.co.th"], view.Cc);
        Assert.Equal(AssignmentEmailStatus.Sent, view.Status);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 1, 0), view.SentAt);
    }

    [Fact]
    public async Task Another_cases_email_is_not_found()
    {
        var other = await SeedAsync(Chosen(appraisalId: Guid.NewGuid()), new DateTime(2026, 11, 1, 9, 0, 0), "P5229");

        await Assert.ThrowsAsync<NotFoundException>(() => ViewAsync(other.Id));
    }

    [Fact]
    public async Task A_non_admin_sees_neither()
    {
        _user.HasPermission("APPRAISAL_ADMINISTRATION_EDIT").Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(HistoryAsync);
        await Assert.ThrowsAsync<ForbiddenException>(() => ViewAsync(Guid.NewGuid()));
    }
}
