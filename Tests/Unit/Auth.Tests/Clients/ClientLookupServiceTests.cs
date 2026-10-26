using System.Collections.Immutable;
using Auth.Application.Services;
using NSubstitute;
using OpenIddict.Abstractions;

namespace Auth.Tests.Clients;

public class ClientLookupServiceTests
{
    private const string Key = "cas:upload_session_lifetime";

    private readonly IOpenIddictApplicationManager _manager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly object _app = new();

    private ClientLookupService Service() => new(_manager);

    private void ClientWithSettings(params (string Key, string Value)[] settings)
    {
        _manager.FindByClientIdAsync("los", Arg.Any<CancellationToken>()).Returns(_app);
        _manager.GetSettingsAsync(_app, Arg.Any<CancellationToken>())
            .Returns(settings.ToImmutableDictionary(s => s.Key, s => s.Value));
    }

    [Fact]
    public async Task Unknown_client_is_null_without_reading_settings()
    {
        _manager.FindByClientIdAsync("nobody", Arg.Any<CancellationToken>()).Returns((object?)null);

        var lifetime = await Service().GetUploadSessionLifetimeAsync("nobody", TestContext.Current.CancellationToken);

        Assert.Null(lifetime);
        await _manager.DidNotReceiveWithAnyArgs().GetSettingsAsync(default!, default);
    }

    [Fact]
    public async Task Stored_value_is_returned()
    {
        ClientWithSettings((Key, "2.00:00:00"));

        var lifetime = await Service().GetUploadSessionLifetimeAsync("los", TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromHours(48), lifetime);
    }

    [Fact]
    public async Task Client_without_the_key_is_null()
    {
        ClientWithSettings(("tkn_lft:act", "00:30:00"));

        var lifetime = await Service().GetUploadSessionLifetimeAsync("los", TestContext.Current.CancellationToken);

        Assert.Null(lifetime);
    }
}
