using Auth.Pages.Account;

namespace Auth.Tests.Pages;

public class LoginTextTests
{
    [Theory]
    [InlineData("th", "en-US,en;q=0.9", "th")] // cookie beats Accept-Language
    [InlineData("zh", null, "zh")]
    [InlineData(null, "th-TH,th;q=0.9,en;q=0.8", "th")]
    [InlineData(null, "zh-CN,zh;q=0.9", "zh")]
    [InlineData(null, "fr;q=0.9,th;q=0.5", "th")] // first supported match by quality
    [InlineData(null, "en;q=0.5,zh;q=0.9", "zh")]
    [InlineData(null, "th;q=0,zh;q=0.1", "zh")] // q=0 is "not acceptable"
    [InlineData(null, "fr-FR,de", "en")]
    [InlineData(null, null, "en")]
    [InlineData(null, "", "en")]
    [InlineData(null, "<script>;;;q=", "en")] // garbage header
    [InlineData("fr", "th", "th")] // invalid cookie is ignored
    [InlineData("TH", null, "th")] // case and region tag are normalised like the SPA does
    [InlineData("th-TH", "zh", "th")]
    [InlineData("zh-CN", null, "zh")]
    [InlineData("-th", null, "en")]
    [InlineData("thai", null, "en")]
    [InlineData("<script>", null, "en")]
    public void Resolves_cookie_then_accept_language_then_english(string? cookie, string? acceptLanguage, string expected) =>
        Assert.Equal(expected, LoginText.Resolve(cookie, acceptLanguage));

    [Fact]
    public void The_switcher_order_and_the_texts_name_the_same_languages() =>
        Assert.Equal(LoginText.Texts.Keys.Order(), LoginText.LanguageOrder.Order());

    [Fact]
    public void Every_language_has_a_complete_text_set()
    {
        foreach (var (code, _) in LoginText.Languages)
        {
            var text = LoginText.For(code);
            foreach (var property in typeof(LoginText).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), $"{code}.{property.Name}");
            }
        }
    }
}
