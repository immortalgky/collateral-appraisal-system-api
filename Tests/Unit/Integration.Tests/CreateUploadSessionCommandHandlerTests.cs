using System.Globalization;
using Auth.Contracts.Clients;
using Document.Domain.UploadSessions.Model;
using FluentAssertions;
using Integration.Application.Features.UploadSessions.CreateUploadSession;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Configurations;
using Shared.Data;

namespace Integration.Tests;

public class CreateUploadSessionCommandHandlerTests
{
    private const int DefaultHours = 24;

    private readonly IRepository<UploadSession, Guid> _repository = Substitute.For<IRepository<UploadSession, Guid>>();
    private readonly IClientLookupService _clients = Substitute.For<IClientLookupService>();

    private async Task<CreateUploadSessionResult> Create(string? clientId)
    {
        var options = Options.Create(new FileStorageConfiguration
        {
            Cleanup = new CleanupConfiguration { TempSessionExpirationHours = DefaultHours }
        });
        var handler = new CreateUploadSessionCommandHandler(
            _repository, _clients, options, NullLogger<CreateUploadSessionCommandHandler>.Instance);

        return await handler.Handle(
            new CreateUploadSessionCommand(clientId, null, null), TestContext.Current.CancellationToken);
    }

    private DateTime StoredExpiry() =>
        ((UploadSession)_repository.ReceivedCalls().Single().GetArguments()[0]!).ExpiresAt;

    /// <summary>The handler reads DateTime.Now itself, so the stored expiry must fall between two readings around the call.</summary>
    private async Task AssertExpiry(string? clientId, int hours)
    {
        var before = DateTime.Now;
        await Create(clientId);
        var after = DateTime.Now;

        StoredExpiry().Should().BeOnOrAfter(before.AddHours(hours)).And.BeOnOrBefore(after.AddHours(hours));
    }

    [Fact]
    public async Task Uses_the_client_lifetime_when_it_has_one()
    {
        _clients.GetUploadSessionLifetimeAsync("los", Arg.Any<CancellationToken>()).Returns(TimeSpan.FromHours(72));

        await AssertExpiry("los", 72);
    }

    [Fact]
    public async Task Falls_back_to_config_when_the_client_has_no_lifetime()
    {
        _clients.GetUploadSessionLifetimeAsync("los", Arg.Any<CancellationToken>()).Returns((TimeSpan?)null);

        await AssertExpiry("los", DefaultHours);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Falls_back_to_config_without_a_lookup_when_the_token_has_no_client_id(string? clientId)
    {
        await AssertExpiry(clientId, DefaultHours);
        await _clients.DidNotReceiveWithAnyArgs()
            .GetUploadSessionLifetimeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Expiry_string_is_gregorian_and_culture_independent_on_a_thai_host()
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("th-TH");
        try
        {
            // Without ICU (invariant globalization) th-TH silently behaves like invariant and this test proves nothing.
            CultureInfo.CurrentCulture.Calendar.Should().BeOfType<ThaiBuddhistCalendar>();
            _clients.GetUploadSessionLifetimeAsync("los", Arg.Any<CancellationToken>()).Returns(TimeSpan.FromHours(72));

            var result = await Create("los");

            var stored = StoredExpiry();
            result.ExpiresAt.Should().Be(stored.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            result.ExpiresAt.Should().StartWith($"{stored.Year}-");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }
}
