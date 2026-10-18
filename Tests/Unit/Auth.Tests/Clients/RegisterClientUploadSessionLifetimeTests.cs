using Auth.Application.Features.Clients.RegisterClient;
using NSubstitute;
using OpenIddict.Abstractions;

namespace Auth.Tests.Clients;

public class RegisterClientUploadSessionLifetimeTests
{
    private static RegisterClientCommand CommandWith(int? hours) =>
        new(null, "Client", "confidential", [], [], ["client_credentials"], [], null, null, null, hours);

    [Fact]
    public void Validator_applies_the_hours_rule()
    {
        var result = new RegisterClientCommandValidator().Validate(CommandWith(721));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterClientCommand.UploadSessionLifetimeHours));
    }

    [Theory]
    [InlineData(48, "2.00:00:00")]
    [InlineData(null, null)]
    public async Task Handler_passes_the_hours_into_the_stored_settings(int? hours, string? stored)
    {
        var manager = Substitute.For<IOpenIddictApplicationManager>();
        OpenIddictApplicationDescriptor? created = null;
        manager.FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((object?)null);
        manager.CreateAsync(Arg.Do<OpenIddictApplicationDescriptor>(d => created = d), Arg.Any<CancellationToken>())
            .Returns(new object());

        await new RegisterClientCommandHandler(manager)
            .Handle(CommandWith(hours), TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.Equal(
            stored,
            created.Settings.GetValueOrDefault("cas:upload_session_lifetime"));
    }
}
