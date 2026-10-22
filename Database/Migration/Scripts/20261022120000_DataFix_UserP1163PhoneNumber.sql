-- ============================================================
-- Data fix: set the phone number of user P1163.
--
-- Not an EF migration; no audit columns are touched. Idempotent: a second run writes the same value.
-- If the user does not exist on this database, nothing is updated.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

UPDATE [auth].[AspNetUsers]
SET    [PhoneNumber] = N'0 2359 0000 Ext.5032'
WHERE  [UserName] = N'P1163';
GO
