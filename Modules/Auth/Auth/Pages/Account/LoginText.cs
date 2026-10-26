using Microsoft.Net.Http.Headers;

namespace Auth.Pages.Account;

/// <summary>Every string on the sign-in page, per language.</summary>
public sealed record LoginText(
    string Label,
    string Title,
    string Subtitle,
    string Heading,
    string UsernameLabel,
    string UsernamePlaceholder,
    string PasswordLabel,
    string PasswordPlaceholder,
    string TogglePassword,
    string SignIn,
    string SigningIn,
    string ChooseOtherMethod,
    string LanguageLabel,
    string RequiredError,
    string InvalidAttemptError,
    string DeactivatedError,
    string LockedOutError)
{
    public const string CookieName = "cas-lang";

    /// <summary>The languages the SPA also offers (its i18next resources), shown in the switcher in this order.</summary>
    public static IEnumerable<(string Code, string Label)> Languages => LanguageOrder.Select(c => (c, Texts[c].Label));

    internal static readonly string[] LanguageOrder = ["en", "th", "zh"];

    internal static readonly Dictionary<string, LoginText> Texts = new()
    {
        ["en"] = new LoginText(
            Label: "English",
            Title: "Login",
            Subtitle: "Collateral appraisal",
            Heading: "Enter your username and password",
            UsernameLabel: "Username",
            UsernamePlaceholder: "Enter your username",
            PasswordLabel: "Password",
            PasswordPlaceholder: "Enter your password",
            TogglePassword: "Toggle password visibility",
            SignIn: "Sign In",
            SigningIn: "Signing in...",
            ChooseOtherMethod: "← Choose another way to sign in",
            LanguageLabel: "Language",
            RequiredError: "Username and password are required.",
            InvalidAttemptError: "Invalid login attempt.",
            DeactivatedError: "This account is deactivated. Contact your administrator.",
            LockedOutError: "Account locked out."),
        ["th"] = new LoginText(
            Label: "ไทย",
            Title: "เข้าสู่ระบบ",
            Subtitle: "ระบบประเมินราคาหลักประกัน",
            Heading: "กรอกชื่อผู้ใช้และรหัสผ่าน",
            UsernameLabel: "ชื่อผู้ใช้",
            UsernamePlaceholder: "กรอกชื่อผู้ใช้",
            PasswordLabel: "รหัสผ่าน",
            PasswordPlaceholder: "กรอกรหัสผ่าน",
            TogglePassword: "แสดงหรือซ่อนรหัสผ่าน",
            SignIn: "เข้าสู่ระบบ",
            SigningIn: "กำลังเข้าสู่ระบบ...",
            ChooseOtherMethod: "← เลือกวิธีเข้าสู่ระบบอื่น",
            LanguageLabel: "เปลี่ยนภาษา",
            RequiredError: "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน",
            InvalidAttemptError: "ข้อมูลการเข้าสู่ระบบไม่ถูกต้อง",
            DeactivatedError: "บัญชีนี้ถูกปิดใช้งาน กรุณาติดต่อผู้ดูแลระบบ",
            LockedOutError: "บัญชีถูกล็อก"),
        ["zh"] = new LoginText(
            Label: "中文",
            Title: "登录",
            Subtitle: "抵押品评估系统",
            Heading: "请输入用户名和密码",
            UsernameLabel: "用户名",
            UsernamePlaceholder: "输入用户名",
            PasswordLabel: "密码",
            PasswordPlaceholder: "输入密码",
            TogglePassword: "显示或隐藏密码",
            SignIn: "登录",
            SigningIn: "正在登录...",
            ChooseOtherMethod: "← 选择其他登录方式",
            LanguageLabel: "切换语言",
            RequiredError: "请输入用户名和密码。",
            InvalidAttemptError: "登录信息无效。",
            DeactivatedError: "此账号已被停用，请联系管理员。",
            LockedOutError: "账号已被锁定。"),
    };

    public static LoginText For(string lang) => Texts[lang];

    /// <summary>
    /// The page language: the <c>cas-lang</c> cookie (shared with the SPA) when it names a supported language,
    /// else the best supported match from <c>Accept-Language</c>, else English (the SPA's fallback too).
    /// Both inputs are untrusted; only a key of <see cref="Texts"/> ever comes out.
    /// </summary>
    public static string Resolve(string? cookie, string? acceptLanguage)
    {
        // Like the SPA: "th-TH" and "TH" both mean "th".
        var fromCookie = cookie?.Split('-')[0].ToLowerInvariant();
        if (fromCookie is not null && Texts.ContainsKey(fromCookie)) return fromCookie;

        if (StringWithQualityHeaderValue.TryParseList(
                acceptLanguage is null ? [] : [acceptLanguage], out var preferred))
        {
            foreach (var p in preferred.Where(p => p.Quality is not 0 && p.Value.HasValue)
                         .OrderByDescending(p => p.Quality ?? 1))
            {
                // "zh-CN", "th-TH" -> "zh", "th"
                var primary = p.Value.Value!.Split('-')[0].ToLowerInvariant();
                if (Texts.ContainsKey(primary)) return primary;
            }
        }

        return "en";
    }
}
