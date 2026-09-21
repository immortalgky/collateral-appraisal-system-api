/*
  20260907090000_MarkSystemAccounts.sql

  Purpose : Flag the technical (break-glass) administrator account as auth.AspNetUsers.IsSystem = 1
            so it stops appearing in the Access Report, the user list and the auth audit log.

  Why     : The bank's access matrix is meant to answer "which people hold which access". A
            break-glass admin is not a person, and a seeded-looking one ('admin' /
            'admin@example.com') reads as a default-account finding in an IT audit. The reads are
            raw Dapper SQL with no view to intercept, so a column on the user row is the only place
            a filter can hang off. The account keeps working exactly as before — it is hidden from
            those three reports, not disabled.

  Scope   : 'ADMIN' is the account name in every environment, production included (confirmed with
            the bank). Note this is not implied by the Development seed — outside Development the
            account is not created by the seeder at all (SeedData:RunSeeders is false there), so a
            future environment could name it something else. Adding a name later needs a NEW script
            file: DbUp journals this one once per database by filename and will not re-run it.

  Safety  : Idempotent — re-running changes nothing. Reversible with
            UPDATE auth.AspNetUsers SET IsSystem = 0 WHERE NormalizedUserName = N'...';
            Once flagged, the account can no longer be edited, unlocked or password-reset through
            the admin UI. That maintenance has to happen here, in the database.
*/

SET NOCOUNT ON;

DECLARE @SystemAccounts TABLE (NormalizedUserName nvarchar(256) PRIMARY KEY);

INSERT INTO @SystemAccounts (NormalizedUserName)
VALUES (N'ADMIN');

UPDATE u
SET u.IsSystem = 1
FROM auth.AspNetUsers u
INNER JOIN @SystemAccounts s ON s.NormalizedUserName = u.NormalizedUserName
WHERE u.IsSystem = 0;

PRINT CONCAT('MarkSystemAccounts: flagged ', @@ROWCOUNT, ' account(s).');

-- Name the accounts that were asked for but do not exist, so a wrong name in the list above shows
-- up in the deployment log instead of passing silently as "nothing to do".
DECLARE @Missing nvarchar(max);

SELECT @Missing = STRING_AGG(s.NormalizedUserName, N', ')
FROM @SystemAccounts s
WHERE NOT EXISTS (
    SELECT 1 FROM auth.AspNetUsers u WHERE u.NormalizedUserName = s.NormalizedUserName
);

IF @Missing IS NOT NULL
    PRINT CONCAT('MarkSystemAccounts: WARNING - no such account(s): ', @Missing);
