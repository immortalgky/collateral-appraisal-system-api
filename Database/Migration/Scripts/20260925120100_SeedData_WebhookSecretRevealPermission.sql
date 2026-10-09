-- ============================================================
-- WEBHOOK_SECRET_REVEAL permission (Admin only)
-- Schema: auth
--
-- Webhook secrets (integration.WebhookSubscriptions.SecretKey / ClientSecret) are now
-- stored encrypted and never returned by the list/detail API. Holders of this code
-- can decrypt one on the admin screen via POST /webhook-subscriptions/{id}/secret/reveal;
-- every reveal is written to integration.WebhookSecretRevealLogs before the value is
-- returned. The endpoint also requires WEBHOOK_SUBSCRIPTIONS_MANAGE.
--
-- Mirrors the entry in AuthDataSeed.SeedPermissionsAsync. SeedAdminRoleAsync is
-- create-only, so an existing Admin role only gets the new code from this script.
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

-- Stated explicitly like every auth seed script: production runs this through sqlcmd, whose
-- QUOTED_IDENTIFIER default is OFF (see 20260925120000_UpdateSeed_UngateMenuGroups.sql).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'WEBHOOK_SECRET_REVEAL', N'Reveal Webhook Secrets',
       N'Decrypt and view a webhook subscription''s stored secret (every reveal is audited)',
       N'Integration', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'WEBHOOK_SECRET_REVEAL');

INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode = N'WEBHOOK_SECRET_REVEAL'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);
