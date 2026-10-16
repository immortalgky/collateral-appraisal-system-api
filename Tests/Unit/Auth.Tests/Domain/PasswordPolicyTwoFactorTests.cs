using Auth.Domain.Configuration;

namespace Auth.Tests.Domain;

public class PasswordPolicyTwoFactorTests
{
    private static PasswordPolicy Updated(bool required, int rememberHours)
    {
        var policy = PasswordPolicy.CreateDefault();
        policy.Update(8, true, true, true, true, 1, 0, 0, null, true, 5, 0, 8, required, rememberHours);
        return policy;
    }

    [Fact]
    public void Defaults_are_off_and_remember_for_twelve_hours()
    {
        var policy = PasswordPolicy.CreateDefault();

        Assert.False(policy.RequireTwoFactorForExternal);
        Assert.Equal(12, policy.TwoFactorRememberHours);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)] // 0 = ask every time
    [InlineData(12, 12)]
    [InlineData(720, 720)]
    [InlineData(100000, 720)]
    public void Remember_hours_are_clamped_to_zero_through_a_month(int requested, int stored) =>
        Assert.Equal(stored, Updated(false, requested).TwoFactorRememberHours);

    [Fact]
    public void Update_stores_the_requirement_flag()
    {
        Assert.True(Updated(true, 12).RequireTwoFactorForExternal);
        Assert.False(Updated(false, 12).RequireTwoFactorForExternal);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)] // internal staff are never forced
    [InlineData(false, true, false)] // switch off
    [InlineData(false, false, false)]
    public void Two_factor_is_required_only_for_company_users_when_the_switch_is_on(
        bool switchOn, bool hasCompany, bool expected) =>
        Assert.Equal(expected, Updated(switchOn, 12).RequiresTwoFactor(hasCompany ? Guid.NewGuid() : null));
}
