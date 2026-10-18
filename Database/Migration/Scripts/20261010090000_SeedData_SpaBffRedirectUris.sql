-- Registers the API's own OIDC callback URIs on the `spa` OAuth client.
--
-- The SPA no longer signs in itself: the API is now the OIDC client of our own OpenIddict and uses the
-- existing public `spa` client (PKCE, no secret). The browser is sent back to the API, so the client
-- needs, per environment:
--   redirect URI           {API}/signin-oidc
--   post-logout URI        {API}/signout-callback-oidc
-- The old `{SPA}/callback` and `{SPA}/` URIs are left alone until the cutover is complete.
--
-- Why a script: OpenIddict compares these URIs exactly, and code reads the client by name, so every
-- database needs them — seeders do not run outside Development.
--
-- Where the API origin comes from: a script has no configuration, so it is supplied when the scripts
-- are applied, as the DbUp variable $ApiBaseUrl$ (the API's public base URL, i.e. the environment's
-- AppBaseUrl, e.g. https://host:7111):
--   Database.exe migrate        Environments:<env>:Variables:ApiBaseUrl in appsettings.Database.json, or the
--                               environment variable Environments__<env>__Variables__ApiBaseUrl
--                               (Development ships with https://localhost:7111)
--   SQL bundle (db/)            New-DbDeploymentScripts.ps1 -ApiBaseUrl https://host:7111
-- When the `spa` client exists and no value was supplied the script FAILS with a clear message rather
-- than guessing: a wrong guess would leave nobody able to sign in. A database without the `spa` client
-- (a fresh install, CI) needs no value; the Development seeder registers the URIs with the client.
--
-- Idempotent: a URI that is already registered is left alone, so re-running is harmless and an admin's
-- later edit is never reverted.

-- Required by JSON_MODIFY. SqlClient (and therefore DbUp) already defaults this ON, but sqlcmd does
-- not.
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM auth.OpenIddictApplications WHERE ClientId = N'spa')
BEGIN
    PRINT 'The `spa` OAuth client does not exist — nothing to configure. A fresh install seeds these URIs with the client.';
END
ELSE
BEGIN
    DECLARE @Redirects nvarchar(max) = (SELECT RedirectUris FROM auth.OpenIddictApplications WHERE ClientId = N'spa');
    DECLARE @PostLogout nvarchar(max) = (SELECT PostLogoutRedirectUris FROM auth.OpenIddictApplications WHERE ClientId = N'spa');

    -- Treat NULL, blank, non-JSON or non-array as "none registered" rather than failing: JSON_MODIFY
    -- errors on a malformed document and would take the whole migration down with it.
    IF @Redirects IS NULL OR ISJSON(@Redirects) = 0 OR LEFT(LTRIM(@Redirects), 1) <> N'[' SET @Redirects = N'[]';
    IF @PostLogout IS NULL OR ISJSON(@PostLogout) = 0 OR LEFT(LTRIM(@PostLogout), 1) <> N'[' SET @PostLogout = N'[]';

    DECLARE @ApiBaseUrl nvarchar(400) = N'$ApiBaseUrl$';
    -- Database.exe always passes ApiBaseUrl (blank when unset); a script run outside it keeps the literal $ApiBaseUrl$ token.
    IF @ApiBaseUrl IS NULL OR LTRIM(RTRIM(@ApiBaseUrl)) = N'' OR @ApiBaseUrl LIKE N'$%$'
       OR @ApiBaseUrl NOT LIKE N'http%://%'
        THROW 50001, N'The `spa` OAuth client needs the API''s public base URL (AppBaseUrl) to register its sign-in callbacks, but ApiBaseUrl was not supplied. Database.exe: set Environments:<env>:Variables:ApiBaseUrl (or env var Environments__<env>__Variables__ApiBaseUrl). SQL bundle: regenerate with New-DbDeploymentScripts.ps1 -ApiBaseUrl https://host:port.', 1;
    SET @ApiBaseUrl = RTRIM(@ApiBaseUrl);
    WHILE RIGHT(@ApiBaseUrl, 1) = N'/' SET @ApiBaseUrl = LEFT(@ApiBaseUrl, LEN(@ApiBaseUrl) - 1);

    DECLARE @Uri nvarchar(450) = @ApiBaseUrl + N'/signin-oidc';
    IF EXISTS (SELECT 1 FROM OPENJSON(@Redirects) WHERE [value] = @Uri)
        PRINT 'The `spa` client already has redirect URI ' + @Uri + ' - leaving it untouched.';
    ELSE
    BEGIN
        SET @Redirects = JSON_MODIFY(@Redirects, 'append $', @Uri);
        PRINT 'Added redirect URI ' + @Uri + ' to the `spa` client.';
    END

    SET @Uri = @ApiBaseUrl + N'/signout-callback-oidc';
    IF EXISTS (SELECT 1 FROM OPENJSON(@PostLogout) WHERE [value] = @Uri)
        PRINT 'The `spa` client already has post-logout URI ' + @Uri + ' - leaving it untouched.';
    ELSE
    BEGIN
        SET @PostLogout = JSON_MODIFY(@PostLogout, 'append $', @Uri);
        PRINT 'Added post-logout URI ' + @Uri + ' to the `spa` client.';
    END

    UPDATE auth.OpenIddictApplications
    SET RedirectUris = @Redirects,
        PostLogoutRedirectUris = @PostLogout
    WHERE ClientId = N'spa';
END
GO
