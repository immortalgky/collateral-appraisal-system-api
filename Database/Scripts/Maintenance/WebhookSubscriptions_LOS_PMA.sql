/*==============================================================================
  WebhookSubscriptions_LOS_PMA.sql
  ------------------------------------------------------------------------------
  Purpose : Seed the dedicated webhook subscription for the LOS PMA push
            (AppraisalPmaUpdatedIntegrationEvent -> WebhookDispatchConsumer ->
            APPRAISAL_PMA_UPDATED), WITHOUT disturbing the existing LOS
            catch-all subscription that already serves the other 9 outbound
            events (APPRAISAL_CREATED, APPRAISAL_STATUS_CHANGED, etc.).

  Background: subscriptions are now keyed by (SystemCode, EventType), where
  EventType = NULL means "catch-all" (matches any event for that SystemCode -
  see IWebhookSubscriptionRepository.GetBySubscriptionAsync: an exact
  (SystemCode, EventType) match wins, otherwise it falls back to the
  catch-all row). "LOS" already has a catch-all (EventType IS NULL, HMAC)
  row. The PMA push needs its OWN row because it uses a different auth model
  (bespoke token-fetch, not HMAC) and a different HTTP method (PUT, not
  POST) - reusing the catch-all row would either break the other 9 events or
  silently send them through the wrong auth/URL.

  Run this MANUALLY (SSMS / sqlcmd) against the target environment. It is NOT
  part of DbUp/EF migrations. Re-runnable: guarded by IF NOT EXISTS, so
  running it twice is a no-op the second time.

  BEFORE running in a real environment:
    - Replace the CallbackUrl / TokenEndpoint placeholders with the real LOS
      host once confirmed (see plan: LOS update endpoint URL/method was
      still being confirmed from the LOS update spec sheet at implementation
      time - HttpMethod='PUT' is the current best guess).
    - Replace the ClientSecret placeholder with the real LOS-provided secret
      (do NOT commit a real secret into source control / this script).
==============================================================================*/

SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- (a) Existing LOS catch-all row (SystemCode='LOS', EventType IS NULL) is left
--     completely untouched by this script. Nothing to do here - shown only for
--     documentation / manual verification:
--
--     SELECT * FROM integration.WebhookSubscriptions
--     WHERE SystemCode = 'LOS' AND EventType IS NULL;
-------------------------------------------------------------------------------

-------------------------------------------------------------------------------
-- (b) Insert the dedicated PMA-push subscription, if it doesn't already exist.
-------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM integration.WebhookSubscriptions
    WHERE SystemCode = 'LOS' AND EventType = 'APPRAISAL_PMA_UPDATED'
)
BEGIN
    INSERT INTO integration.WebhookSubscriptions
    (
        Id,
        SystemCode,
        EventType,
        CallbackUrl,
        SecretKey,
        IsActive,
        LastDeliveryAt,
        AuthType,
        TokenEndpoint,
        ClientId,
        ClientSecret,
        HttpMethod,
        CreatedAt,
        CreatedBy,
        CreatedWorkstation,
        UpdatedAt,
        UpdatedBy,
        UpdatedWorkstation
    )
    VALUES
    (
        NEWID(),
        'LOS',
        'APPRAISAL_PMA_UPDATED',
        'https://<LOS-HOST>:8420/api/appraisal/pma',   -- TODO(LOS): confirm the real update URL + method from the update spec sheet
        NULL,                                          -- SecretKey unused for AuthType='TokenBearer' (nullable column)
        1,                                              -- IsActive
        NULL,                                           -- LastDeliveryAt
        'TokenBearer',
        'https://<LOS-HOST>:8420/api/auth/token',       -- POST + JSON { client_id, client_secret } -> { access_token, token_type, expires_in }
        'CAS',                                          -- ClientId
        '<LOS-PROVIDED-CLIENT-SECRET>',                 -- TODO: replace with the real secret; do not commit real secrets
        'PUT',                                           -- LOS update call method (best guess pending spec confirmation)
        GETDATE(),
        'SYSTEM',
        NULL,
        NULL,
        NULL,
        NULL
    );
END;

-------------------------------------------------------------------------------
-- Example (commented out): splitting a catch-all subscription into explicit
-- per-event rows. Useful if a SystemCode's catch-all needs to be phased out
-- in favour of one row per event (e.g. once every event type it serves has
-- its own auth/callback requirements). Not run by this script.
-------------------------------------------------------------------------------
/*
-- 1) Add an explicit row for one specific event, copying the catch-all's
--    current auth/callback so behavior is unchanged for that event...
INSERT INTO integration.WebhookSubscriptions
(Id, SystemCode, EventType, CallbackUrl, SecretKey, IsActive, LastDeliveryAt,
 AuthType, TokenEndpoint, ClientId, ClientSecret, HttpMethod,
 CreatedAt, CreatedBy, CreatedWorkstation, UpdatedAt, UpdatedBy, UpdatedWorkstation)
SELECT
    NEWID(), SystemCode, 'APPRAISAL_STATUS_CHANGED', CallbackUrl, SecretKey, IsActive, NULL,
    AuthType, TokenEndpoint, ClientId, ClientSecret, HttpMethod,
    GETDATE(), 'SYSTEM', NULL, NULL, NULL, NULL
FROM integration.WebhookSubscriptions
WHERE SystemCode = 'LOS' AND EventType IS NULL;

-- 2) ...repeat per event type until every event the catch-all served has its
--    own row, then optionally deactivate (not delete) the catch-all:
-- UPDATE integration.WebhookSubscriptions
-- SET IsActive = 0, UpdatedAt = GETDATE(), UpdatedBy = 'SYSTEM'
-- WHERE SystemCode = 'LOS' AND EventType IS NULL;
*/
