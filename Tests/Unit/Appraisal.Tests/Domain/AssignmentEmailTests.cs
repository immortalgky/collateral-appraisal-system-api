using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>#623: an assignment email keeps what went out, so "ดูฉบับที่ส่ง" can show it later.</summary>
public class AssignmentEmailTests
{
    private static AssignmentEmail Chosen() => AssignmentEmail.ToSend(
        Guid.NewGuid(), Guid.NewGuid(), ["extra@acme.co.th"], [AssignmentEmailCcRoles.IntAdminAll], "subject",
        "<p>เรียน {{company}}</p>", []);

    [Fact]
    public void Queue_records_the_resolved_recipients_beside_the_composed_ones()
    {
        var email = Chosen();

        email.Queue("บริษัท แอคมี จำกัด", null, ["jobs@acme.co.th", "extra@acme.co.th"], ["boss@bank.co.th"]);

        Assert.Equal(["jobs@acme.co.th", "extra@acme.co.th"], email.SentTo);
        Assert.Equal(["boss@bank.co.th"], email.SentCc);
        // The composed lists stay as the admin chose them: a resend copies chips, not addresses.
        Assert.Equal(["extra@acme.co.th"], email.ExtraTo);
        Assert.Equal([AssignmentEmailCcRoles.IntAdminAll], email.Cc);
    }

    [Fact]
    public void Mark_sent_stamps_when_the_gateway_accepted_it()
    {
        var email = Chosen();
        email.Queue(null, null, ["jobs@acme.co.th"], []);

        email.MarkSent(new DateTime(2026, 11, 1, 10, 30, 0));

        Assert.Equal(AssignmentEmailStatus.Sent, email.Status);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 30, 0), email.SentAt);
    }

    [Fact]
    public void A_redelivered_confirmation_keeps_the_first_sent_time()
    {
        var email = Chosen();
        email.Queue(null, null, ["jobs@acme.co.th"], []);
        email.MarkSent(new DateTime(2026, 11, 1, 10, 30, 0));

        email.MarkSent(new DateTime(2026, 11, 1, 11, 0, 0));

        Assert.Equal(new DateTime(2026, 11, 1, 10, 30, 0), email.SentAt);
    }

    [Fact]
    public void A_resend_is_told_apart_from_the_first_send()
    {
        Assert.False(Chosen().IsResend);
        Assert.True(AssignmentEmail.Resend(Guid.NewGuid(), Guid.NewGuid(), [], [], "subject", "<p>body</p>", []).IsResend);
    }

    [Fact]
    public void A_reported_send_failure_never_touches_an_email_still_waiting_for_its_handover()
    {
        var email = Chosen();

        email.MarkFailed("550 mailbox unavailable");

        Assert.Equal(AssignmentEmailStatus.Pending, email.Status);
        Assert.Null(email.FailureReason);
    }

    [Fact]
    public void An_email_the_handover_could_not_ready_fails_with_its_reason()
    {
        var email = Chosen();

        email.FailToPrepare("ระบบเตรียมอีเมลไม่สำเร็จ");

        Assert.Equal(AssignmentEmailStatus.Failed, email.Status);
        Assert.Equal("ระบบเตรียมอีเมลไม่สำเร็จ", email.FailureReason);
    }
}
