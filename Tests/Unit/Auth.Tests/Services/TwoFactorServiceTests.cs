using Auth.Application.Services;

namespace Auth.Tests.Services;

public class TwoFactorServiceTests
{
    [Theory]
    [InlineData("123456", "123456")]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData("1 2-3 4\t5\n6", "123456")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Spaces_and_dashes_in_a_code_are_ignored(string? typed, string expected) =>
        Assert.Equal(expected, TwoFactorService.NormalizeCode(typed));

    [Theory]
    [InlineData("ABCDE-12345", "ABCDE-12345")]
    [InlineData("abcde-12345", "ABCDE-12345")]
    [InlineData("abcde12345", "ABCDE-12345")] // typed without the hyphen
    [InlineData(" abcde 12345 ", "ABCDE-12345")]
    [InlineData("ABCDE_12345", "ABCDE-12345")]
    [InlineData("ABCDE-1234", "ABCDE1234")] // too short: left as typed (letters and digits), will not match
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_recovery_code_is_matched_in_any_case_with_or_without_its_hyphen(string? typed, string expected) =>
        Assert.Equal(expected, TwoFactorService.NormalizeRecoveryCode(typed));

    [Fact]
    public void The_key_to_type_is_lower_case_in_groups_of_four() =>
        Assert.Equal("jbsw y3dp ehpk 3pxp", TwoFactorService.FormatKey("JBSWY3DPEHPK3PXP"));

    [Fact]
    public void The_uri_names_the_issuer_and_escapes_the_username()
    {
        var uri = TwoFactorService.BuildUri("somchai.j@bank", "JBSWY3DPEHPK3PXP");

        Assert.Equal(
            "otpauth://totp/Collateral%20Appraisal%20System:somchai.j%40bank?secret=JBSWY3DPEHPK3PXP" +
            "&issuer=Collateral%20Appraisal%20System&digits=6",
            uri);
    }

    // enabled, required, remembered -> step
    [Theory]
    [InlineData(false, false, false, SecondStep.None)]
    [InlineData(false, true, false, SecondStep.Enrol)] // forced and has none
    [InlineData(true, false, false, SecondStep.Code)]
    [InlineData(true, true, false, SecondStep.Code)]
    [InlineData(true, false, true, SecondStep.None)] // remembered device skips the code
    [InlineData(true, true, true, SecondStep.None)]
    [InlineData(false, true, true, SecondStep.Enrol)] // a remembered device means nothing without two-factor
    public void The_step_after_the_first_factor(bool enabled, bool required, bool remembered, SecondStep expected) =>
        Assert.Equal(expected, LoginCompletion.Decide(required, enabled, remembered));
}
