# Maintenance scripts

Manual scripts for a DBA to run by hand. **Nothing here runs automatically.**

`DatabaseMigrator` executes two sets only — one-time journaled scripts under
`Database/Migration/Scripts/**` (matched by `.Migration.Scripts.`) and repeatable checksum-tracked
scripts under `Views` / `StoredProcedures` / `Functions` (matched by `IsRepeatableScript`).
`.Scripts.Maintenance.` matches neither filter. The files are embedded in the assembly and copied to
the output directory, but no code path ever reads them.

If a change needs to reach every environment automatically, it belongs in
`Database/Migration/Scripts/` with a `yyyyMMddHHmmss_` prefix — not here.

## Bank provisioning — run in this order

These three are a set, sourced from the CAS Security Matrix (28/07/2026). Order matters: users
reference roles, and the RBAC sync assumes both exist.

| # | Script | Character | Re-runnable |
|---|---|---|---|
| 1 | `CreateBankRoles.sql` | Additive. Creates the 3 roles our seed lacks — Inquiry, Report, IT Security — and grants each the matrix's permissions. Never edits existing roles or menus. | Yes — creates by `NormalizedName`, grants by `NOT EXISTS` |
| 2 | `CreateBankUsers.sql` | Additive, insert-only. Bulk-creates bank staff as LDAP users (`PasswordHash = NULL`, `AuthSource = 'LDAP'`, `MustChangePassword = 0`) with role, committee and team membership. | Yes — every insert guarded by `NOT EXISTS` |
| 3 | `MASTER_RBAC_Deploy.sql` | **DESTRUCTIVE. Run once per database.** PART A is a full sync: it *revokes* any `auth.RolePermissions` grant not in its intended list, across 12 managed roles. Any permission granted by hand through `/admin/roles` is deleted. | Technically yes, but re-running re-destroys hand-grants |

Read PART A of `MASTER_RBAC_Deploy.sql` before running it on an environment where anyone has been
granting permissions through the admin UI.

## Role-permission repair

| Script | Character |
|---|---|
| `RestoreAllRolePermissions.sql` | Insert-only reconcile — repopulates `auth.RolePermissions` for all roles from the same mapping as `AuthDataSeed.cs`. Adds what is missing, removes nothing. Overlaps heavily with `MASTER_RBAC_Deploy.sql` PART A; prefer this one unless you specifically want the destructive sync. |

Both files encode the same role→permission truth as `Modules/Auth/Auth/Infrastructure/Seed/AuthDataSeed.cs`
and the `RP` map in `docs/access-matrix.xlsx`. **When you change any role's grants, update all of them
in lockstep** — the seeder is create-only, so a fresh seed and a restore must not diverge.

## One-off data fixes

| Script | Character |
|---|---|
| `PatchUserDepartments.sql` | Corrects `auth.AspNetUsers.Department` for 1,085 bank users (Thai → English). Matches by `NormalizedUserName`, updates only rows that actually differ. Inserts chunked at 900 rows (SQL Server caps `VALUES` at 1,000). Source: `.claude/docs/fix user department.xlsx`, which is gitignored — the row-level provenance is not reviewable in git. |
| `UpdateAppraisalSearchUrl.sql` | Repoints the "Appraisal Search" menu item `/appraisals/search` → `/appraisals/list`. Superseded by the auto-applied migration script that covers the same row; kept because it is idempotent and harmless. |

## Housekeeping / diagnostics

`BackfillCompletedTaskCommitteeRemark.sql`, `BackfillPendingTaskOpenedAt.sql`,
`BackfillPendingTaskSlaDurationHours.sql`, `BackfillPricingFinalValueLandArea.sql`,
`CleanupLoadTestAppraisals.sql`, `CleanupOrphanedProperties.sql`, `ListFragmentedIndexes.sql`,
`ListTableSizes.sql`, `PdpaRedactCustomerData.sql`, `RebuildOrReorganizeIndexes.sql`,
`RestrictPmaPropertyPermissionToIntAppraisalStaff.sql`, `WebhookSubscriptions_LOS_PMA.sql`.

`PdpaRedactCustomerData.sql` is destructive by design — it redacts customer PII in place.

## Conventions for new scripts

- Lead with a header comment: what it does, where the data came from, whether it is idempotent, and
  any prerequisite script.
- Make it re-runnable. Guard inserts with `NOT EXISTS`; guard updates by pinning the old value in the
  `WHERE` so a second run is a no-op.
- Any DML against `auth.MenuItems` needs `SET QUOTED_IDENTIFIER ON` — the table has a filtered index.
- State the blast radius in the header if the script deletes or revokes anything.
