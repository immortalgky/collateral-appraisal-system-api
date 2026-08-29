-- Gives the `spa` OAuth client its own token lifetimes.
--
--   access    00:15:00   same as the server-wide default — written here so the admin screen shows
--   identity  00:15:00   the numbers in force instead of a blank field
--   refresh   08:00:00   an IDLE timeout: the longest a session may go without calling
--                        /auth/refresh. Covers a working day while making an overnight gap force a
--                        fresh sign-in, which is the point — a browser that restores its session
--                        cookies on relaunch would otherwise hand the user straight back in.
--
-- Why a script and not just a seeder change: seeders are gated behind SeedData:RunSeeders and never
-- run outside Development, and the client-creation seeder is insert-only anyway, so an existing
-- UAT/production database would keep inheriting the server-wide defaults. OpenIddict reads these
-- settings straight off the application row (OpenIddictServerHandlers) and parses them with
-- TimeSpan.Parse.
--
-- Note the trade-off in writing access/identity at all: once a value is on the row, this client stops
-- following the server-wide default, so a later change in AuthModule will not reach it. That is the
-- point of per-client configuration, but it does mean two places to look.
--
-- Admins retune these in /admin/clients, or with a direct UPDATE here. Measured on a running
-- instance: the settings are read as each token is issued, so a change lands on the next sign-in
-- with no restart required (verified by moving refresh 480 -> 45 -> 480 minutes and watching
-- OpenIddictTokens.ExpirationDate follow each time).
--
-- Idempotent: merges each key into whatever Settings JSON already exists rather than overwriting the
-- document, and leaves an existing value alone so a deliberate admin change is never reverted.

-- Required by JSON_MODIFY. SqlClient (and therefore DbUp) already defaults this ON, but sqlcmd does
-- not — without it a manual run of this file fails with "UPDATE failed because the following SET
-- options have incorrect settings: 'QUOTED_IDENTIFIER'".
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM auth.OpenIddictApplications WHERE ClientId = N'spa')
BEGIN
    PRINT 'The `spa` OAuth client does not exist — nothing to configure. A fresh install seeds these values with the client.';
END
ELSE
BEGIN
    DECLARE @Wanted TABLE (SettingKey nvarchar(50) PRIMARY KEY, Lifetime nvarchar(50), Label nvarchar(50));
    INSERT INTO @Wanted (SettingKey, Lifetime, Label) VALUES
        (N'tkn_lft:act',  N'00:15:00', N'access'),
        (N'tkn_lft:idt',  N'00:15:00', N'identity'),
        (N'tkn_lft:reft', N'08:00:00', N'refresh');

    DECLARE @SettingKey nvarchar(50), @Lifetime nvarchar(50), @Label nvarchar(50);
    DECLARE @Current nvarchar(max), @Path nvarchar(100);

    DECLARE settings_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT SettingKey, Lifetime, Label FROM @Wanted ORDER BY SettingKey;
    OPEN settings_cursor;
    FETCH NEXT FROM settings_cursor INTO @SettingKey, @Lifetime, @Label;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Re-read inside the loop: each iteration writes the column, so a value cached before the
        -- first UPDATE would silently drop every key written after it.
        SET @Current = (SELECT Settings FROM auth.OpenIddictApplications WHERE ClientId = N'spa');

        -- Treat a NULL, blank, or non-JSON Settings column as "no settings yet" rather than failing:
        -- JSON_MODIFY errors on a malformed document and would take the whole migration down with it.
        IF @Current IS NULL OR LTRIM(RTRIM(@Current)) = N'' OR ISJSON(@Current) = 0
            SET @Current = N'{}';

        SET @Path = N'$."' + @SettingKey + N'"';

        IF JSON_VALUE(@Current, @Path) IS NOT NULL
        BEGIN
            PRINT 'The `spa` client already carries a ' + @Label + ' token lifetime — leaving it untouched.';
        END
        ELSE
        BEGIN
            UPDATE auth.OpenIddictApplications
            SET Settings = JSON_MODIFY(@Current, @Path, @Lifetime)
            WHERE ClientId = N'spa';

            PRINT 'Set the `spa` ' + @Label + ' token lifetime to ' + @Lifetime + '. Applies to the next token issued.';
        END

        FETCH NEXT FROM settings_cursor INTO @SettingKey, @Lifetime, @Label;
    END

    CLOSE settings_cursor;
    DEALLOCATE settings_cursor;
END
GO
