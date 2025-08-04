namespace OAuth2OpenId.Pages.Account;

public class Login(
    SignInManager<ApplicationUser> signInManager,
    ILogger<Login> logger)
    : PageModel
{
    [BindProperty] public string ReturnUrl { get; set; } = "/";

    [BindProperty] public string Error { get; set; } = string.Empty;

    [BindProperty] public string Username { get; set; } = string.Empty;

    [BindProperty] public string Password { get; set; } = string.Empty;

    public void OnGet(string returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        logger.LogInformation("Login attempt for user: {Username}", Username);

        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            Error = "Username and password are required.";
            return Page();
        }

        // Validate credentials
        var result = await signInManager.PasswordSignInAsync(Username, Password, true, false);
        if (result.Succeeded)
        {
            logger.LogInformation("User {Username} logged in successfully", Username);

            // If returnUrl is empty or null, use default "/"
            var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;

            // Make sure the URL is safe to redirect to
            if (!Url.IsLocalUrl(redirectUrl) && !redirectUrl.StartsWith("https://"))
            {
                logger.LogWarning("Invalid return URL: {ReturnUrl}, redirecting to home", redirectUrl);
                redirectUrl = "/";
            }

            return Redirect(redirectUrl);
        }

        if (result.IsLockedOut)
        {
            Error = "Account locked out.";
            return Page();
        }

        Error = "Invalid login attempt.";
        return Page();
    }
}