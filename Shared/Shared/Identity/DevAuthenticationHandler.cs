using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Identity;

public class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevBypass";
    public const string DevHeaderName = "X-Dev-Auth";
    public const string DevHeaderValue = "dev-bypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DevHeaderName, out var headerValue)
            || headerValue != DevHeaderValue)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var devId = Guid.Empty.ToString();

        var claims = new[]
        {
            new Claim("sub", devId),
            new Claim("name", "dev-user"),
            new Claim("preferred_username", "dev-user"), // bank code (UserCode) — used for actor/audit stamping
            // NOT a company id. `Guid.TryParse` succeeds on the all-zero GUID, so
            // CurrentUserService.CompanyId came back non-null and every company-scoped read
            // treated the dev identity as an external firm numbered 00000000-… — which matches no
            // assignment, so the list came back empty and every by-id read 404'd. Omitting the
            // claim makes the dev user internal, which is what local work actually wants.
            // new Claim("company_id", devId),
            // Permissions
            new Claim("permissions", "auth:read"),
            new Claim("permissions", "auth:write"),
            new Claim("permissions", "document:read"),
            new Claim("permissions", "document:write"),
            new Claim("permissions", "notification:read"),
            new Claim("permissions", "notification:write"),
            new Claim("permissions", "request:read"),
            new Claim("permissions", "request:write"),
            new Claim("permissions", "PERMISSION_MANAGE"),
            new Claim("permissions", "ROLE_MANAGE"),
            new Claim("permissions", "GROUP_MANAGE"),
            new Claim("permissions", "USER_MANAGE"),
            new Claim("permissions", "USER_CHANGE_PASSWORD"),
            new Claim("permissions", "USER_RESET_PASSWORD"),
            new Claim("permissions", "JOB_SCHEDULE_MANAGE"),
            // Both appraisal codes, because this identity is a superuser: it holds everything, so
            // that calling an endpoint with X-Dev-Auth never fails for a permission reason.
            //
            // APPRAISAL_VIEW is the one that matters — `appraisal.browse` accepts either, but
            // holding TRACKING alone would make the dev identity read as CREDIT
            // (`AppraisalFieldScope.IsTrackingOnly` = has TRACKING and NOT VIEW), masking values
            // and refusing the export.
            new Claim("permissions", "APPRAISAL_VIEW"),
            new Claim("permissions", "APPRAISAL_TRACKING_VIEW"),
            // To exercise the CREDIT responses locally, comment out APPRAISAL_VIEW and keep the
            // tracking code — that is the exact shape the revoke script leaves a credit user in.
            // Removing both is neither audience: nothing masks, and `appraisal.browse` answers 403.
            new Claim("permissions", "ADDRESS_MASTER_MANAGE"),
            // Roles
            new Claim("roles", "Admin"),
            // Scopes
            new Claim("scope", "appraisal.read"),
            new Claim("scope", "request.write"),
            new Claim("scope", "document.read"),
            new Claim("scope", "document.write"),
            new Claim("scope", "integration"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
