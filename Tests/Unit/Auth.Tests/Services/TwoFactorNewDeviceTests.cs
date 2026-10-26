using Auth.Application.Features.Auth.TwoFactor;
using Auth.Application.Services;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using Auth.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Exceptions;

namespace Auth.Tests.Services;

public class TwoFactorNewDeviceTests : IDisposable
{
    private readonly UserManager<ApplicationUser> _users = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

    // Never connects: the service only discards its pending changes.
    private readonly AuthDbContext _db = new(new DbContextOptionsBuilder<AuthDbContext>().UseSqlServer("Server=unused").Options);

    private readonly IPasswordPolicyProvider _policy = Substitute.For<IPasswordPolicyProvider>();
    private readonly ApplicationUser _user = new() { Id = Guid.NewGuid(), UserName = "somchai", TwoFactorEnabled = true };

    // What Identity itself answers for a code that is not on the account (as opposed to a save it refused).
    private static IdentityResult NotOneOfTheCodes =>
        IdentityResult.Failed(new IdentityErrorDescriber().RecoveryCodeRedemptionFailed());

    public TwoFactorNewDeviceTests()
    {
        _users.AccessFailedAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        _users.ResetAccessFailedCountAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
    }

    public void Dispose() => _db.Dispose();

    private TwoFactorService Service => new(_users, _policy, _db, NullLogger<TwoFactorService>.Instance);

    private void AuthenticatorCodeIs(bool valid) =>
        _users.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultAuthenticatorProvider, Arg.Any<string>()).Returns(valid);

    [Fact]
    public async Task An_authenticator_code_is_accepted_and_no_recovery_code_is_spent()
    {
        AuthenticatorCodeIs(true);

        var usedRecoveryCode = await Service.VerifyForNewDeviceAsync(_user, "123 456");

        Assert.False(usedRecoveryCode);
        await _users.DidNotReceive().RedeemTwoFactorRecoveryCodeAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task A_recovery_code_is_accepted_as_typed_and_spent()
    {
        AuthenticatorCodeIs(false);
        _users.RedeemTwoFactorRecoveryCodeAsync(_user, "ABCDE-12345").Returns(IdentityResult.Success);

        var usedRecoveryCode = await Service.VerifyForNewDeviceAsync(_user, " abcde12345 ");

        Assert.True(usedRecoveryCode);
        await _users.DidNotReceive().AccessFailedAsync(_user);
    }

    [Theory]
    [InlineData("ABCDE-99999")] // a recovery code that is not (or no longer) on the account
    [InlineData("")]
    [InlineData(null)]
    public async Task A_wrong_code_counts_toward_the_lockout(string? code)
    {
        AuthenticatorCodeIs(false);
        _users.RedeemTwoFactorRecoveryCodeAsync(_user, Arg.Any<string>()).Returns(NotOneOfTheCodes);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, code));

        Assert.Equal(TwoFactorService.InvalidCodeErrorCode, error.Code);
        await _users.Received(1).AccessFailedAsync(_user);
        // Identity throws on an empty recovery code, so it is never asked.
        if (string.IsNullOrEmpty(code))
            await _users.DidNotReceive().RedeemTwoFactorRecoveryCodeAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Without_two_factor_there_is_nothing_to_replace()
    {
        _user.TwoFactorEnabled = false;

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, "123456"));

        Assert.Equal(TwoFactorService.NotEnabledErrorCode, error.Code);
        await _users.DidNotReceive().AccessFailedAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task A_locked_out_account_is_refused_before_any_code_is_tried()
    {
        _users.IsLockedOutAsync(_user).Returns(true);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, "123456"));

        Assert.Equal(TwoFactorService.LockedOutErrorCode, error.Code);
        await _users.DidNotReceive().VerifyTwoFactorTokenAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task The_policy_is_not_asked_so_a_user_it_requires_two_factor_for_is_allowed()
    {
        AuthenticatorCodeIs(true);

        await Service.VerifyForNewDeviceAsync(_user, "123456");

        await _policy.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_wrong_code_that_locks_the_account_says_so_instead_of_invalid()
    {
        AuthenticatorCodeIs(false);
        _users.RedeemTwoFactorRecoveryCodeAsync(_user, Arg.Any<string>()).Returns(NotOneOfTheCodes);
        _users.IsLockedOutAsync(_user).Returns(false, true); // the check before the attempt, then the one after it

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, "ABCDE-99999"));

        Assert.Equal(TwoFactorService.LockedOutErrorCode, error.Code);
        await _users.Received(1).AccessFailedAsync(_user);
    }

    [Fact]
    public async Task A_save_Identity_refuses_is_a_save_failure_not_a_wrong_code()
    {
        AuthenticatorCodeIs(false);
        _users.RedeemTwoFactorRecoveryCodeAsync(_user, "ABCDE-12345").Returns(
            IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail" }));
        // What Identity left pending (the user and its token), and another account's pending token that must survive.
        var token = new IdentityUserToken<Guid> { UserId = _user.Id, LoginProvider = "[AspNetUserStore]", Name = "RecoveryCodes", Value = "A" };
        var other = new IdentityUserToken<Guid> { UserId = Guid.NewGuid(), LoginProvider = "[AspNetUserStore]", Name = "RecoveryCodes", Value = "B" };
        _db.Attach(_user).State = EntityState.Modified;
        _db.Attach(token).State = EntityState.Modified;
        _db.Add(other);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, "ABCDE-12345"));

        Assert.Equal(TwoFactorService.SaveFailedErrorCode, error.Code);
        await _users.DidNotReceive().AccessFailedAsync(Arg.Any<ApplicationUser>());
        Assert.Equal(EntityState.Unchanged, _db.Entry(_user).State); // what Identity left pending is put back
        Assert.Equal(EntityState.Unchanged, _db.Entry(token).State);
        Assert.Equal(EntityState.Added, _db.Entry(other).State); // and nothing else is touched
    }

    [Fact]
    public void The_commands_never_print_the_code()
    {
        const string code = "ABCDE-12345";
        object[] commands =
        [
            new NewTwoFactorDeviceCommand(Guid.NewGuid(), code),
            new EnableTwoFactorCommand(Guid.NewGuid(), code),
            new DisableTwoFactorCommand(Guid.NewGuid(), code),
            new RegenerateRecoveryCodesCommand(Guid.NewGuid(), code)
        ];

        foreach (var command in commands)
            Assert.DoesNotContain(code, command.ToString());
    }

    [Fact]
    public async Task A_failed_attempt_Identity_cannot_count_is_a_save_failure_not_a_wrong_code()
    {
        AuthenticatorCodeIs(false);
        _users.RedeemTwoFactorRecoveryCodeAsync(_user, Arg.Any<string>()).Returns(NotOneOfTheCodes);
        _users.AccessFailedAsync(_user).Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidEmail" }));

        // Both ways in: the new-device check and the guarded profile endpoints (here, new recovery codes).
        var newDevice = await Assert.ThrowsAsync<BadRequestException>(() => Service.VerifyForNewDeviceAsync(_user, "ABCDE-99999"));
        var guarded = await Assert.ThrowsAsync<BadRequestException>(() => Service.RegenerateRecoveryCodesAsync(_user, "000000"));

        Assert.Equal(TwoFactorService.SaveFailedErrorCode, newDevice.Code);
        Assert.Equal(TwoFactorService.SaveFailedErrorCode, guarded.Code);
    }

    [Fact]
    public async Task A_right_code_whose_failed_attempts_cannot_be_cleared_is_a_save_failure_and_leaves_nothing_pending()
    {
        _users.ResetAccessFailedCountAsync(_user).Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidEmail" }));
        _db.Attach(_user).State = EntityState.Modified; // what Identity left pending

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Service.ClearFailedAttemptsAsync(_user));

        Assert.Equal(TwoFactorService.SaveFailedErrorCode, error.Code);
        Assert.Equal(EntityState.Unchanged, _db.Entry(_user).State);
    }
}
