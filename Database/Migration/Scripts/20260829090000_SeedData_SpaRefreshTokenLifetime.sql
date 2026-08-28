-- Gives the `spa` OAuth client its own refresh-token idle timeout (8 hours).
--
-- Why this exists as a script and not just a seeder change: seeders are gated behind
-- SeedData:RunSeeders and never run outside Development, and the client-creation seeder is
-- insert-only anyway — so an existing UAT/production database would keep falling back to the
-- server-wide default (7 days). OpenIddict reads this setting straight off the application row
-- (OpenIddictServerHandlers, "tkn_lft:reft") and parses it with TimeSpan.Parse.
--
-- Because refresh tokens are rolling and sliding, the value is an IDLE timeout: the longest a
-- session may go without calling /auth/refresh. Eight hours covers a working day while making an
-- overnight gap force a fresh sign-in, which is the point — a browser that restores its session
-- cookies on relaunch would otherwise hand the user straight back in.
--
-- Admins can retune this afterwards in /admin/clients, or with a direct UPDATE here. Measured on a
-- running instance: the setting is read when each token is issued, so a change lands on the next
-- sign-in with no restart required (verified by moving the value 480 -> 45 -> 480 minutes and
-- watching OpenIddictTokens.ExpirationDate follow each time).
--
-- Idempotent: merges the key into whatever Settings JSON already exists rather than overwriting the
-- document, and leaves an existing value alone so a deliberate admin change is never reverted.

-- Required by JSON_MODIFY. SqlClient (and therefore DbUp) already defaults this ON, but sqlcmd does
-- not — without it a manual run of this file fails with "UPDATE failed because the following SET
-- options have incorrect settings: 'QUOTED_IDENTIFIER'".
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Lifetime nvarchar(50) = N'08:00:00';

IF NOT EXISTS (SELECT 1 FROM auth.OpenIddictApplications WHERE ClientId = N'spa')
BEGIN
    PRINT 'The `spa` OAuth client does not exist — nothing to configure. It will be seeded with this value on a fresh install.';
END
ELSE
BEGIN
    DECLARE @Current nvarchar(max) =
        (SELECT Settings FROM auth.OpenIddictApplications WHERE ClientId = N'spa');

    -- Treat a NULL, blank, or non-JSON Settings column as "no settings yet" rather than failing:
    -- JSON_MODIFY would error on a malformed document and take the whole migration down with it.
    IF @Current IS NULL OR LTRIM(RTRIM(@Current)) = N'' OR ISJSON(@Current) = 0
        SET @Current = N'{}';

    IF JSON_VALUE(@Current, N'$."tkn_lft:reft"') IS NOT NULL
    BEGIN
        PRINT 'The `spa` client already carries a refresh-token lifetime — leaving it untouched.';
    END
    ELSE
    BEGIN
        UPDATE auth.OpenIddictApplications
        SET Settings = JSON_MODIFY(@Current, N'$."tkn_lft:reft"', @Lifetime)
        WHERE ClientId = N'spa';

        PRINT 'Set the `spa` refresh-token lifetime to 08:00:00. Applies to the next token issued.';
    END
END
GO
