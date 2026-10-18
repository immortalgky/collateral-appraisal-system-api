using Document.Domain.UploadSessions.Model;
using FluentAssertions;
using Integration.Application.Features.UploadSessions.CancelUploadSession;
using NSubstitute;
using Shared.Data;
using Shared.Exceptions;
using Shared.Time;

namespace Integration.Tests;

public class CancelUploadSessionCommandHandlerTests
{
    private static readonly DateTime Clock = DateTime.Now;

    private readonly IRepository<UploadSession, Guid> _repository = Substitute.For<IRepository<UploadSession, Guid>>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private CancelUploadSessionCommandHandler Handler()
    {
        _clock.ApplicationNow.Returns(Clock);
        return new CancelUploadSessionCommandHandler(_repository, _clock);
    }

    private Task Cancel(UploadSession session)
    {
        _repository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        return Handler().Handle(new CancelUploadSessionCommand(session.Id), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Active_session_expires_now()
    {
        var session = UploadSession.Create(Clock.AddHours(24), null, null);

        await Cancel(session);

        session.ExpiresAt.Should().Be(Clock);
    }

    [Fact]
    public async Task Already_expired_session_is_left_as_it_was()
    {
        var session = UploadSession.Create(Clock.AddHours(24), null, null);
        session.Expire(Clock.AddHours(-1));
        var earlier = session.ExpiresAt;

        await Cancel(session);

        session.ExpiresAt.Should().Be(earlier);
    }

    [Fact]
    public async Task Completed_session_is_a_conflict()
    {
        var session = UploadSession.Create(Clock.AddHours(24), null, null);
        session.Complete(Clock);

        var act = () => Cancel(session);

        await act.Should().ThrowAsync<ConflictException>();
        session.ExpiresAt.Should().Be(Clock.AddHours(24));
    }

    [Fact]
    public async Task Missing_session_is_not_found()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((UploadSession?)null);

        var act = () => Handler().Handle(new CancelUploadSessionCommand(id), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
