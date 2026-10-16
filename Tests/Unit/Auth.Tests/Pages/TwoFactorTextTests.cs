using Auth.Pages.Account;

namespace Auth.Tests.Pages;

public class TwoFactorTextTests
{
    [Fact]
    public void Every_sign_in_language_has_a_complete_two_factor_text_set()
    {
        Assert.Equal(LoginText.LanguageOrder.Order(), TwoFactorText.Texts.Keys.Order());

        foreach (var (code, _) in LoginText.Languages)
        {
            var text = TwoFactorText.For(code);
            foreach (var property in typeof(TwoFactorText).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), $"{code}.{property.Name}");
            }
        }
    }
}
