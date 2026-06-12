# LDAP / Active Directory — Connection Test & Configuration Guide

How to test the bank AD (LHB.NET) connection, discover what attributes AD exposes, and
configure the app's LDAP login. Run the PowerShell from a **Windows app server** (it has
line-of-sight to AD and is domain-joined). No RSAT and no `ldapsearch` are required.

## Known environment values

From the bank's AD path `LDAP://LHB.NET/DC=LHB,DC=NET`:

| Setting | Value |
|---|---|
| Domain (`Server`) | `LHB.NET` |
| NetBIOS `Domain` | `LHB` |
| `BaseDn` | `DC=LHB,DC=NET` |
| Service account | `CN=CASU2USER,OU=ServiceUser,DC=LHB,DC=NET` (sAMAccountName `CASU2USER`) |
| `SearchFilter` | `(sAMAccountName={0})` |

## Authentication mode: Windows Integrated (chosen)

The app binds to AD as the **IIS app-pool identity** (`LHB\CASU2USER`) — no password in
`appsettings`. Negotiate/Kerberos seals the channel, so plain port **389** is used (no LDAPS
certificate to trust). Set the app-pool password once in IIS (encrypted in
`applicationHost.config`). In dev, `dotnet run` uses the developer's own domain login, so it
works with zero password config.

> A service-account fallback (`UseIntegratedAuth:false`, explicit `BindDn`/`BindPassword` over
> LDAPS:636) is still supported in code via simple bind (`AuthType.Basic`).

---

## Step 1 — Confirm values + dump every attribute (no service account, no RSAT)

Run as your logged-on domain user. The last loop is the **"what info can we take from AD"** answer.

```powershell
$root = [ADSI]"LDAP://RootDSE"
$root.defaultNamingContext        # expect: DC=LHB,DC=NET
$root.dnsHostName                 # a real DC hostname
$env:USERDNSDOMAIN                # expect: LHB.NET

$ds = New-Object System.DirectoryServices.DirectorySearcher([ADSI]"LDAP://DC=LHB,DC=NET")
$ds.Filter = "(sAMAccountName=YOUR_TEST_USER)"
$u = $ds.FindOne()
$u.Properties.GetEnumerator() | % { "{0} = {1}" -f $_.Name, ($_.Value -join ';') }
```

## Step 2 — Confirm integrated bind works (mirrors what the app does)

```powershell
Add-Type -AssemblyName System.DirectoryServices.Protocols
$c = New-Object System.DirectoryServices.Protocols.LdapConnection((New-Object System.DirectoryServices.Protocols.LdapDirectoryIdentifier("LHB.NET",389)))
$c.SessionOptions.ProtocolVersion = 3
$c.AuthType = [System.DirectoryServices.Protocols.AuthType]::Negotiate
$c.Bind(); "SEARCH BIND OK (integrated, no password)"     # uses current Windows identity

# Validate a user's password the same way the app will (sAMAccountName + domain, NOT a DN):
$pw = Read-Host "test user's password" -AsSecureString
$c2 = New-Object System.DirectoryServices.Protocols.LdapConnection((New-Object System.DirectoryServices.Protocols.LdapDirectoryIdentifier("LHB.NET",389)))
$c2.AuthType = [System.DirectoryServices.Protocols.AuthType]::Negotiate
$c2.Bind((New-Object System.Net.NetworkCredential("YOUR_TEST_USER",[Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw)),"LHB"))); "USER BIND OK"
```

If AD rejects the 389 Negotiate bind with "strong auth required", the code can enable
`SessionOptions.Signing`/`Sealing` for the integrated path.

---

## Why binding with a DN failed under the old behavior (reference)

`LdapConnection` defaults to `AuthType.Negotiate`, which accepts a bare username /
`DOMAIN\user` / UPN — **not** a distinguished name. The original code bound with DNs
(`BindDn` and the user's directory DN), so the bind failed with
`"The supplied credential is invalid."` even with the correct password. Reproduce:

```powershell
# DN + Negotiate -> FAILS ("The supplied credential is invalid.")
# DN + Basic (simple bind, over LDAPS:636) -> OK
# bare username + Negotiate -> OK   (this is why an early bare-username test misled us)
```

The fix: integrated mode uses Negotiate with username+domain; service-account mode uses
`AuthType.Basic` so a DN can bind.

---

## Configuration (`appsettings`)

Committed `appsettings.json` keeps `Enabled:false`. Set `Enabled:true` in
`appsettings.Development.json` or the server environment to turn LDAP on.

```jsonc
"Ldap": {
  "Enabled": true,
  "Server": "LHB.NET",
  "Port": 389,
  "UseSsl": false,
  "UseIntegratedAuth": true,
  "Domain": "LHB",
  "BaseDn": "DC=LHB,DC=NET",
  "BindDn": "",
  "BindPassword": "",
  "SearchFilter": "(sAMAccountName={0})",
  "FallbackToLocalAuth": true,        // keep true until LDAP logins work, then optionally false
  "ConnectionTimeoutSeconds": 5,
  "Attributes": {
    "Username": "sAMAccountName",
    "Email": "mail",
    "FirstName": "givenName",
    "LastName": "sn",
    "Department": "department",
    "Position": "title"
  }
}
```

## IIS / AD setup

- IIS Manager → Application Pools → *(the app's pool)* → Advanced Settings →
  **Identity = Custom account** → `LHB\CASU2USER` + password (set "Load User Profile = True"
  if needed). Apply on **both** app servers (N=2).
- `CASU2USER` needs only **read** on the directory (default for authenticated domain users) — no admin.

## Verify the login path end-to-end

1. Set `Enabled:true` (dev settings) and `dotnet run --project Bootstrapper/Api`.
2. Open the login page; sign in as a test AD user with their **AD/Windows password**.
3. Expect a row in `auth.AspNetUsers` with `AuthSource = 'LDAP'` and
   `FirstName/LastName/Email/Department/Position` filled from AD
   (`FindOrCreateLdapUserAsync`, `Login.cshtml.cs`).
4. Seq (`localhost:5341`) shows `"User {Username} logged in via LDAP"` /
   `"Auto-provisioned LDAP user"`. On failure the `LdapException` message pinpoints the cause.

## Attributes: available vs. mapped

Fill from Step 1 output. The app currently maps 6; extend `LdapAttributeMapping` +
`LdapUserInfo` + `ReadUserAttributesAsync` to map more (e.g. `displayName`, `employeeID`,
`telephoneNumber`, `manager`, `memberOf`).

| AD attribute | Mapped today | App field |
|---|---|---|
| `sAMAccountName` | yes | UserName |
| `mail` | yes | Email |
| `givenName` | yes | FirstName |
| `sn` | yes | LastName |
| `department` | yes | Department |
| `title` | yes | Position |
| _(others from Step 1)_ | no | — |
