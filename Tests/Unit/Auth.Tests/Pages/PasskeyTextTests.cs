using Auth.Pages.Account;

namespace Auth.Tests.Pages;

public class PasskeyTextTests
{
    [Fact]
    public void Every_sign_in_language_has_a_complete_passkey_text_set()
    {
        Assert.Equal(LoginText.LanguageOrder.Order(), PasskeyText.Texts.Keys.Order());

        foreach (var (code, _) in LoginText.Languages)
        {
            var text = PasskeyText.For(code);
            foreach (var property in typeof(PasskeyText).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), $"{code}.{property.Name}");
            }
        }
    }
}
