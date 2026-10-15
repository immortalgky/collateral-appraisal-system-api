using Document.Domain.UploadSessions.Model;
using FluentAssertions;

namespace Document.Tests;

public class UploadSessionExpireTests
{
    private static UploadSession NewSession(DateTime expiresAt) => UploadSession.Create(expiresAt, null, null);

    [Fact]
    public void Expire_brings_a_later_expiry_forward()
    {
        var session = NewSession(DateTime.Now.AddHours(24));
        var now = DateTime.Now;

        session.Expire(now);

        session.ExpiresAt.Should().Be(now);
    }

    [Fact]
    public void Expire_never_extends_an_earlier_expiry()
    {
        var expiresAt = DateTime.Now.AddHours(1);
        var session = NewSession(expiresAt);

        session.Expire(expiresAt.AddHours(5));

        session.ExpiresAt.Should().Be(expiresAt);
    }
}
