# Deployment scripts

Small PowerShell deploy for the Collateral Appraisal System onto Windows
Server / IIS. No external CI/CD infra — build a versioned bundle on your build
box, copy it to a **temp** folder on each app server, then swap **temp → live**.

The end-to-end runbook for bank IT is
[`docs/deployment/production-deployment-guide.html`](../docs/deployment/production-deployment-guide.html).

```
deploy/
  deploy.config.ps1           # per-server settings (paths, pool name, health URL) — EDIT THIS
  Publish.ps1                 # build box (macOS or Windows, pwsh): produce CAS-<ver>.zip
  New-DbDeploymentScripts.ps1 # build box: generate the db/ SQL bundle (called by Publish.ps1)
  Invoke-SqlDeploy.ps1        # DBA: run the db/ SQL bundle with sqlcmd, in order, abort on error
                              #      (00_CreateDatabase.sql in that bundle creates the database)
  Invoke-DbMigrate.ps1        # legacy fallback: run Database.exe instead of the SQL bundle
  Deploy-App.ps1              # server: safe in-place backend swap (run on each server)
  Deploy-Web.ps1              # server: frontend static-file swap (run on each server)
```

## Prerequisites

- **Build box:** PowerShell 7 (`pwsh`), .NET 9 SDK, `dotnet-ef`, Node + **pnpm**.
  On macOS: `brew install --cask powershell`, `dotnet tool install --global dotnet-ef`,
  `npm i -g pnpm`. The frontend repo enforces pnpm (`preinstall: only-allow pnpm`).
- **Frontend `.env.<mode>`:** the SPA bakes `VITE_API_URL` in at build time.
  Create `.env.production` in the frontend repo with the target environment's
  public API/app URLs. `Publish.ps1` fails fast if it is missing.
- **App servers:** Windows Server + IIS with the **ASP.NET Core 9 Hosting
  Bundle** (ANCM) and the **URL Rewrite** module (needed by the SPA site). Run
  the `Deploy-*.ps1` scripts from an elevated PowerShell.
- **DBA workstation / SQL host:** `sqlcmd` or SSMS. Nothing else.

## Release flow

**0. First deployment to a new environment only — configure the servers.** Skip this on a
redeploy; `appsettings.Production.json` is preserved across deployments (see *What gets
preserved* below), so it only has to be right once. See *First-time setup on a new server* for
the IIS/certificate side.

On each app server, create `appsettings.Production.json` from
`Bootstrapper/Api/appsettings.Production.json.template` and substitute **every** `#{TOKEN}#`.
`ConnectionStrings:Database` must be right or the app cannot start.

> **The application no longer seeds data.** `SeedData:RunSeeders` gates every data seeder and
> **fails closed** — unset means off, and the production template sets it to `false` explicitly.
> Seeding is a fresh-install convenience for Development only. On a live database the
> whole-table-guarded seeders were already no-ops, while the per-key ones silently re-inserted rows
> an admin had deleted, undoing their work on every app-pool recycle. Reference data that code
> depends on now ships as a one-off script in `Database/Migration/Scripts/`, applied once per
> database by the `db/` bundle; everything else belongs to the admin UI and is never rewritten.

### Standing up a brand-new environment

Because seeding is off, a **new, empty** database will have no permissions, menus, roles, workflow
groups or admin account, and — unlike before — **this no longer self-heals on restart**. For a new
environment, one of the following is required:

1. Restore or script the reference data from an existing environment (preferred — the C# blueprints
   have drifted from the real databases in places, so the live data is the more truthful source), or
2. Set `SeedData:RunSeeders: true` for the **first boot only**, confirm sign-in works, then set it
   back to `false` before the environment is handed over.

If you take option 2, these two values must be correct *at that first boot*, or the deployment looks
healthy while nobody can sign in:

| Setting | If it is wrong/missing at the seeding boot |
|---|---|
| `SeedData:AdminUser` | no admin account is created — nobody can sign in |
| `Cors:AllowedOrigins` | the `spa` OAuth client is skipped and logged as an error — nobody can sign in |

This does not apply to an already-live environment, where those rows exist already.

**1. Build (build box):**
```bash
pwsh deploy/Publish.ps1 -Version 20260723-101500 -FrontendMode production
# -> dist-artifacts/CAS-20260723-101500.zip  (api/ web/ tools/ db/ database/)
```
Building on macOS is fine. The .NET outputs are published *framework-dependent*
(the server's ASP.NET Core 9 Hosting Bundle supplies the runtime) but **RID-qualified
to `win-x64`**, so the bundle always contains the Windows apphosts — `api\Api.exe`,
`database\Database.exe` and `tools\CasSecretTool.exe` — whatever OS did the build.
That matters: a plain publish on macOS emits a Mach-O binary and **no `.exe` at all**,
which would leave the operator with no `CasSecretTool.exe` to run. Override with
`-RuntimeIdentifier ''` to publish portable instead.

**2. Copy** `CAS-<ver>.zip` to `C:\Deploy\temp\` on each app server and expand it
so you have `C:\Deploy\temp\<ver>\{api,web,tools,db,database}`. Hand `db\` to the DBA.
The `tools\` folder holds `CasSecretTool` for encrypting config secrets — see the operator
manual `docs/deployment/cas-secret-tool-manual.md` (background in
`docs/deployment/multi-server-deployment.md` §2.12).

**3. Deploy the database — ONCE per release**, before any new app instance starts.
The `db/` folder is plain SQL; `Database.exe` is not needed on the server.

> This step is **mandatory, not a convenience**. The application does not apply migrations — on
> startup it only checks for pending ones and refuses to boot if the schema is behind the build
> (`Shared/Shared/Data/Extensions/MigrationExtension.cs`). Deploying the app first will simply
> fail its health check. Data seeding is a separate thing that still happens at app start —
> see step 4.

**On a new environment, review `00_CreateDatabase.sql` first.** The bundle creates the database
itself — this used to happen implicitly (`Database.Migrate()` creates a missing database; the app
no longer migrates). It runs against `[master]`, uses the **instance default data/log paths**, and
then applies production settings: fixed-MB autogrowth, `RECOVERY FULL`,
`READ_COMMITTED_SNAPSHOT ON`, `AUTO_SHRINK OFF`, statistics options, `PAGE_VERIFY CHECKSUM` and
Query Store. Its `DECLARE` block at the top is the only part to edit:

- **Collation** — nothing in the codebase pins one, so whatever is set here is what production
  gets. Thai text is `nvarchar`, so this governs sorting and comparison rather than storage — but
  a production database that differs from UAT will sort and compare differently with nothing to
  catch it. **Match UAT** unless the bank mandates otherwise.
- **File sizes / growth** — sized so autogrowth never fires in normal operation. Adjust to the
  environment.
- **`@DbName`** — the database to create. If you change it, pass the same name to
  `Invoke-SqlDeploy.ps1 -Database`; that script verifies the database exists afterwards and fails
  with a pointed message if the two disagree.

Two consequences worth planning for: `RECOVERY FULL` means the transaction log grows until a log
backup truncates it, so **a transaction-log backup schedule must exist before go-live** or the log
volume will fill; and RCSI (enabled per this repo's own
[`docs/SQL_Server_Locking_&_Isolation_Reference.md`](../docs/SQL_Server_Locking_&_Isolation_Reference.md))
keeps row versions in tempdb, so size and monitor tempdb accordingly. Explicit lock hints in the
application are unaffected.

The deploy login needs `db_owner` on the database, plus permission to create it. Schemas
(`appraisal`, `request`, `workflow`, `auth`, …) are created by the EF scripts.

```powershell
.\deploy\Invoke-SqlDeploy.ps1 -ServerInstance SQLHOST -Database CollateralAppraisal `
    -ScriptPath C:\Deploy\temp\20260723-101500\db -TrustedConnection
```
or, in SSMS/sqlcmd, run the files in this order — every one is idempotent:

| File | Runs against | Contents |
|---|---|---|
| `00_CreateDatabase.sql` | **`master`** | creates the database (default file paths) + production settings |
| `00_Prepare.sql` | target db | `dbo.DatabaseMigrationHistory` (the journal table) |
| `01_EF_01..11_*.sql` | target db | EF Core idempotent schema scripts, **in numeric order** |
| `02_Repeatable_ViewsAndProcs.sql` | target db | 60 views/procs, `CREATE OR ALTER`, dependency-ordered |
| `03_OneTime_DataScripts.sql` | target db | 40 seed/data scripts, each skipped if already journaled |
| `99_Verify.sql` | target db | read-only verification — run last, read the output |

`Invoke-SqlDeploy.ps1` handles the `master` connection for the first file automatically, then
verifies the database exists before running anything inside it.

**Running the files by hand in SSMS?** All of them are plain T-SQL — no SQLCMD Mode, no `:setvar`,
nothing to enable. Just connect to the right database and press Execute. Two points only:

- `00_CreateDatabase.sql` must be run against **`master`**, and its `DECLARE @DbName` in the EDIT
  block at the top must match the database you then use for the rest.
- `99_Verify.sql` is read-only; run it last and read the output.

**4. Deploy the app on each server** (elevated PowerShell, **one server at a time**):
```powershell
.\deploy\Deploy-App.ps1 -Version 20260723-101500
.\deploy\Deploy-Web.ps1 -Version 20260723-101500
```
On startup each node verifies the schema is current and refuses to boot if it is behind the build.
**No data is written at startup** — see the seeding note above. The Workflow module additionally
logs `CRITICAL` if any required `workflow.ActivityProcessConfigurations` row is missing, which is
the signal that a `Database/Migration/Scripts/` script shipped with a feature was not applied;
it logs rather than throws, so it will not stop a node from serving.

Confirm `/health/ready` before moving to the next server — an F5 courtesy so one node keeps
serving throughout the rollout. (This used to be a hard requirement because the insert-only
seeders would race on check-then-insert if both nodes started together. With seeding disabled
that race is gone, but rolling one server at a time is still the right way to deploy.)

**5. Post-deployment steps that are NOT automated.** These are manual on purpose —
they need per-environment values the repo cannot hold:

| Step | Why it is manual |
|---|---|
| `Database/Scripts/Maintenance/WebhookSubscriptions_LOS_PMA.sql` | Registers the LOS PMA push subscription. Contains `<LOS-HOST>` and `<LOS-PROVIDED-CLIENT-SECRET>` placeholders that **must** be replaced with the real LOS values before running. Not in the `db/` bundle. |
| Committee members | Committees and thresholds exist, but members resolve by username, so a production database gets **no members**. Add them via the admin UI. |
| `los` / `cls` OAuth clients | Not created by the app (seeding is off). Create them via `/admin/clients`, or set `Authentication:Clients:<id>:ClientSecret` and use a first-boot seeding run on a new environment only. |

Everything else — schema, views, procs, reference data, menus, permissions, roles and
the workflow assignment groups — is applied by the `db/` bundle.

The application writes no *reference data* at startup. The one exception is `JobSchedules`:
`UseModuleRecurringJobs` inserts a row for any recurring job that does not have one yet
(`Shared/Shared/Scheduling/RecurringJobScheduleExtensions.cs`), which is deliberate and safe — an
existing row always wins over the code default, so a cron edited through `/admin/job-schedules`
is re-applied on every boot and never overwritten.

## How the SQL bundle is generated

`New-DbDeploymentScripts.ps1` reproduces exactly what `Database.exe migrate` does,
as files:

- **EF schema** — `dotnet ef migrations script --idempotent` per DbContext, in the
  same dependency order as `Database/Migration/EfCoreMigrationService.cs`.
- **Repeatable objects** — every `Database/Scripts/{Views,StoredProcedures}/**.sql`.
  The runtime tool resolves view-on-view dependencies by retrying on SQL error 208;
  offline, the generator topologically sorts them instead (comments are stripped
  before the scan — header comments naming sibling views otherwise create false
  cycles). Each script is followed by a journal upsert carrying the **same SHA-256
  checksum** the tool computes, so a later `Database.exe` run treats them as unchanged.
- **One-time scripts** — every `Database/Migration/Scripts/*.sql`, each wrapped in a
  journal check that uses `SET NOEXEC ON` to skip an already-applied block (these
  scripts contain their own `GO` batches, so a plain `IF … BEGIN … END` guard would
  not work).

Regenerate just the SQL sections (skipping the slow EF step) with:
```bash
pwsh deploy/New-DbDeploymentScripts.ps1 -OutDir ./out/db -SkipEf
```

## What gets preserved (never overwritten on the server)

`Deploy-App.ps1` uses `robocopy /MIR` (so stale DLLs are cleaned) but excludes,
via `/XF` and `/XD`, the files configured in `deploy.config.ps1`:

- `appsettings.Production.json` — generated on the server from the `.template`. Every `#{TOKEN}#`
  must be substituted. In particular `Cors:AllowedOrigins` drives the SPA's OAuth redirect URIs
  (the `spa` client is skipped if it is empty, and nobody can sign in), and the `los` / `cls`
  client secrets are skipped — with an error logged — if left as placeholders.
- `web.config` — server-owned; also carries the raised `maxAllowedContentLength`.
- `logs/`, `DataProtection-Keys/` — guarded. (Data Protection keys are actually
  stored in the DB here, so this is just belt-and-braces.)

## First-time setup on a new server (one-off, not scripted)

See §6 of the production deployment guide. In short: Hosting Bundle + URL Rewrite
+ WebSockets, a domain service account, two app pools (`CAS-Api`, `CAS-Web`), two
websites, the OAuth2 certificates with private-key ACLs, and the 50 MB upload limit.

## Rollback

Every `Deploy-App` / `Deploy-Web` run mirrors the previous live folder to
`C:\Deploy\backups\<timestamp>\{api,web}` first:
```powershell
.\deploy\Deploy-App.ps1 -ArtifactApiPath C:\Deploy\backups\<timestamp>\api -SkipBackup
```
Database migrations are **not** auto-rolled-back — schema changes are
backward-compatible across a single release so the previous app build still runs.
Reversing a migration means restoring the pre-deployment backup.
