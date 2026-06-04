using System.Security.Cryptography;
using Auth.Application.Configurations;
using Auth.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Auth.Pages.Account;

[AllowAnonymous]
public class Login(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILdapAuthenticationService ldapService,
    IOptions<LdapConfiguration> ldapOptions,
    ILogger<Login> logger,
    IDateTimeProvider dateTimeProvider)
    : PageModel
{
    private readonly LdapConfiguration _ldapConfig = ldapOptions.Value;

    [BindProperty] public string ReturnUrl { get; set; } = "/";

    [BindProperty] public string Error { get; set; } = string.Empty;

    [BindProperty] public string Username { get; set; } = string.Empty;

    [BindProperty] public string Password { get; set; } = string.Empty;

    [BindProperty] public bool RememberMe { get; set; }

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

        // LDAP authentication path
        if (_ldapConfig.Enabled)
        {
            var ldapResult = await ldapService.AuthenticateAsync(Username, Password);

            if (ldapResult.Succeeded && ldapResult.UserInfo is not null)
            {
                var user = await FindOrCreateLdapUserAsync(ldapResult.UserInfo);
                if (user is null)
                {
                    Error = "Failed to provision user account.";
                    return Page();
                }

                if (!user.IsActive)
                {
                    Error = "This account is deactivated. Contact your administrator.";
                    return Page();
                }

                await StampLastLoginAsync(user);
                await signInManager.SignInAsync(user, RememberMe);
                logger.LogInformation("User {Username} logged in via LDAP", Username);
                return Redirect(GetSafeRedirectUrl(user));
            }

            if (!_ldapConfig.FallbackToLocalAuth)
            {
                logger.LogWarning("LDAP auth failed for {Username}: {Error}", Username, ldapResult.ErrorMessage);
                Error = "Invalid login attempt.";
                return Page();
            }

            // Fall through to local auth
            logger.LogInformation("LDAP auth failed for {Username}, falling back to local auth", Username);
        }

        // Local authentication path
        // Resolve the user before issuing any session so we can reject inactive
        // accounts without ever establishing a cookie.
        var localUser = await userManager.FindByNameAsync(Username);
        if (localUser is not null && !localUser.IsActive)
        {
            Error = "This account is deactivated. Contact your administrator.";
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(Username, Password, RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (localUser is not null)
                await StampLastLoginAsync(localUser);

            logger.LogInformation("User {Username} logged in successfully", Username);
            return Redirect(GetSafeRedirectUrl(localUser));
        }

        if (result.IsLockedOut)
        {
            Error = "Account locked out.";
            return Page();
        }

        Error = "Invalid login attempt.";
        return Page();
    }

    private async Task StampLastLoginAsync(ApplicationUser user)
    {
        user.LastLoginAt = dateTimeProvider.ApplicationNow;
        try
        {
            await userManager.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            // A transient store failure must not invalidate an otherwise-valid login
            logger.LogWarning(ex, "Failed to stamp LastLoginAt for user {Username} — login proceeds", user.UserName);
        }
    }

    private async Task<ApplicationUser?> FindOrCreateLdapUserAsync(LdapUserInfo info)
    {
        var user = await userManager.FindByNameAsync(info.Username);

        if (user is null)
        {
            // Auto-provision new LDAP user
            user = new ApplicationUser
            {
                UserName = info.Username,
                Email = info.Email ?? $"{info.Username}@ldap.local",
                FirstName = info.FirstName ?? string.Empty,
                LastName = info.LastName ?? string.Empty,
                Department = info.Department,
                Position = info.Position,
                AuthSource = "LDAP",
                EmailConfirmed = true
            };

            // Generate a random password — LDAP users never use local passwords
            var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var createResult = await userManager.CreateAsync(user, randomPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to create LDAP user {Username}: {Errors}", info.Username, errors);
                return null;
            }

            logger.LogInformation("Auto-provisioned LDAP user: {Username}", info.Username);
            return user;
        }

        // Sync AD attributes on every login
        var changed = false;

        if (info.Email is not null && user.Email != info.Email) { user.Email = info.Email; changed = true; }
        if (info.FirstName is not null && user.FirstName != info.FirstName) { user.FirstName = info.FirstName; changed = true; }
        if (info.LastName is not null && user.LastName != info.LastName) { user.LastName = info.LastName; changed = true; }
        if (info.Department is not null && user.Department != info.Department) { user.Department = info.Department; changed = true; }
        if (info.Position is not null && user.Position != info.Position) { user.Position = info.Position; changed = true; }
        if (user.AuthSource != "LDAP") { user.AuthSource = "LDAP"; changed = true; }

        if (changed)
        {
            await userManager.UpdateAsync(user);
            logger.LogInformation("Synced AD attributes for user: {Username}", info.Username);
        }

        return user;
    }

    private string GetSafeRedirectUrl(ApplicationUser? user = null)
    {
        var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;

        if (!Url.IsLocalUrl(redirectUrl) && !redirectUrl.StartsWith("https://"))
        {
            logger.LogWarning("Invalid return URL: {ReturnUrl}, redirecting to home", redirectUrl);
            redirectUrl = "/";
        }

        // If the user must change their password, append a flag so the SPA can intercept
        // and redirect to the change-password screen before allowing normal navigation.
        // Only append to LOCAL urls — never leak account state to an external domain.
        if (user?.MustChangePassword == true && Url.IsLocalUrl(redirectUrl))
        {
            var separator = redirectUrl.Contains('?') ? "&" : "?";
            redirectUrl += $"{separator}mustChangePassword=true";
        }

        return redirectUrl;
    }
}
