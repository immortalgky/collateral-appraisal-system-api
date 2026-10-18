using System.Collections.Immutable;
using Auth.Application.Features.Clients;
using Auth.Application.Features.Clients.UpdateClient;
using NSubstitute;
using OpenIddict.Abstractions;

namespace Auth.Tests.Clients;

public class UploadSessionLifetimeTests
{
    private static readonly ClientTokenLifetimes NoTokenLifetimes = new(null, null, null);

    private static UpdateClientCommand UpdateWith(int? hours) =>
        new("id", "Client", [], [], ["client_credentials"], [], null, null, null, hours);

    private static OpenIddictApplicationDescriptor Apply(int? hours, OpenIddictApplicationDescriptor? descriptor = null)
    {
        descriptor ??= new OpenIddictApplicationDescriptor();
        ClientPermissionMapper.ApplyToDescriptor(
            descriptor, "confidential", ["client_credentials"], [], [], [], NoTokenLifetimes, hours);
        return descriptor;
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public void Hours_are_bounded_to_one_hour_through_thirty_days(int hours, bool valid)
    {
        var result = new UpdateClientCommandValidator().Validate(UpdateWith(hours));

        Assert.Equal(valid, result.Errors.All(e => e.PropertyName != nameof(UpdateClientCommand.UploadSessionLifetimeHours)));
    }

    [Fact]
    public void Null_hours_is_valid()
    {
        var result = new UpdateClientCommandValidator().Validate(UpdateWith(null));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(UpdateClientCommand.UploadSessionLifetimeHours));
    }

    [Theory]
    [InlineData(48, "2.00:00:00")]
    [InlineData(720, "30.00:00:00")]
    public void Hours_are_stored_as_a_timespan_string(int hours, string stored)
    {
        var descriptor = Apply(hours);

        Assert.Equal(stored, descriptor.Settings[ClientPermissionMapper.UploadSessionLifetime.SettingKey]);
    }

    [Fact]
    public void Null_hours_removes_the_key()
    {
        var descriptor = Apply(48);

        Apply(null, descriptor);

        Assert.DoesNotContain(ClientPermissionMapper.UploadSessionLifetime.SettingKey, descriptor.Settings.Keys);
    }

    [Theory]
    [InlineData("1:00:00", 1)]
    [InlineData("30.00:00:00", 720)]
    [InlineData("00:00:30", null)]
    [InlineData("00:20:00", null)]
    [InlineData("01:30:00", null)]
    [InlineData("31.00:00:00", null)]
    [InlineData("3650.00:00:00", null)]
    [InlineData("-1:00:00", null)]
    [InlineData("garbage", null)]
    [InlineData("24", null)]
    [InlineData("7", null)]
    public void Stored_value_outside_the_admin_domain_reads_as_no_setting(string stored, int? hours)
    {
        var settings = new Dictionary<string, string> { [ClientPermissionMapper.UploadSessionLifetime.SettingKey] = stored };

        var lifetime = ClientPermissionMapper.ReadUploadSessionLifetime(settings);

        Assert.Equal(hours is { } h ? TimeSpan.FromHours(h) : null, lifetime);
    }

    [Fact]
    public void Absent_key_reads_as_no_setting()
    {
        Assert.Null(ClientPermissionMapper.ReadUploadSessionLifetime(new Dictionary<string, string>()));
    }

    [Fact]
    public void Huge_hours_do_not_overflow_into_the_valid_range()
    {
        var result = new UpdateClientCommandValidator().Validate(UpdateWith(int.MaxValue));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateClientCommand.UploadSessionLifetimeHours));
    }

    [Fact]
    public async Task Detail_dto_reads_the_stored_value_back_as_hours()
    {
        var manager = Substitute.For<IOpenIddictApplicationManager>();
        var app = new object();
        var settings = Apply(48).Settings;
        manager.GetSettingsAsync(app, Arg.Any<CancellationToken>())
            .Returns(settings.ToImmutableDictionary());
        manager.GetPermissionsAsync(app, Arg.Any<CancellationToken>()).Returns([]);
        manager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>()).Returns([]);
        manager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>()).Returns([]);

        var dto = await ClientPermissionMapper.ToDetailDtoAsync(manager, app, TestContext.Current.CancellationToken);

        Assert.Equal(48, dto.UploadSessionLifetimeHours);
    }
}
