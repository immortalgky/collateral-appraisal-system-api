# Multi-Server Deployment & Certificate Management

This runbook covers everything required to run the Collateral Appraisal System API
behind a load balancer on **two (or more) Windows IIS servers** without JWT decrypt
failures, plus how local developers handle certificates on **macOS / Linux**.

It is the operational complement to the in-code wiring done in:

- `Modules/Auth/Auth/AuthModule.cs` — OpenIddict signing/encryption cert loading
- `Modules/Auth/Auth/Infrastructure/AuthDbContext.cs` — `IDataProtectionKeyContext`
- `Shared/Shared/Security/DataProtectionExtensions.cs` — shared keyring registration
- `Shared/Shared/Security/CertificateProvider.cs` — store/file cert loading
- `Bootstrapper/Api/Program.cs` — `UseForwardedHeaders`, `AddSharedDataProtection`

---

## 1. Why this matters

The system uses two layers of cryptographic material that **must be identical on every
load-balanced node**:

| Layer | What it protects | Where it lives |
|---|---|---|
| ASP.NET Core **Data Protection keys** | Antiforgery cookies, OpenIddict reference refresh tokens, any `IDataProtector` payloads | `auth.DataProtectionKeys` table (shared via SQL Server) |
| OpenIddict **signing certificate** | JWT signature on access/identity tokens | Windows cert store (prod) / auto-generated (dev) |
| OpenIddict **encryption certificate** | Symmetric key wrap inside encrypted JWT access tokens | Windows cert store (prod) / auto-generated (dev) |

If any of these differ across nodes, you get one of:

- *"The antiforgery token could not be decrypted"* (cookies)
- *"The specified token is invalid"* (refresh tokens issued on Node A, redeemed on Node B)
- *"IDX10503: Signature validation failed"* (signing cert mismatch)
- *"IDX10609: Decryption failed"* (encryption cert mismatch — **the exact symptom from the previous deployment**)

Data Protection is shared automatically via SQL once both nodes point at the same
database. The certificates require manual provisioning.

---

## 2. Production: Windows IIS, two servers behind a load balancer

### 2.1 Prerequisites

- Two Windows Server hosts running IIS, both joined to the same network as the SQL Server.
- An L7 load balancer (ARR, F5, Azure App Gateway, etc.) terminating TLS in front.
- A service account (or `IIS AppPool\<YourAppPoolName>` identity) that the app pool runs as.
- Administrator access to **both** servers to import certificates and grant private-key ACLs.

### 2.2 Generate the two certificates (do this **once**, on a build/admin workstation)

> Run as Administrator in PowerShell. Pick strong PFX passwords and store them in your
> secrets vault — they are needed every time the cert is imported.

```powershell
# --- Signing certificate (RSA-2048, DigitalSignature) ---
$signCert = New-SelfSignedCertificate `
  -Subject       "CN=CollateralAppraisal-Signing" `
  -KeyAlgorithm  RSA `
  -KeyLength     2048 `
  -HashAlgorithm SHA256 `
  -KeyUsage      DigitalSignature `
  -NotAfter      (Get-Date).AddYears(10) `
  -CertStoreLocation Cert:\CurrentUser\My

$signPwd = ConvertTo-SecureString -String "<SIGNING_PFX_PASSWORD>" -AsPlainText -Force
Export-PfxCertificate -Cert $signCert -FilePath .\cas-signing.pfx -Password $signPwd

Write-Host "Signing thumbprint: $($signCert.Thumbprint)"

# --- Encryption certificate (RSA-2048, KeyEncipherment + DataEncipherment) ---
$encCert = New-SelfSignedCertificate `
  -Subject       "CN=CollateralAppraisal-Encryption" `
  -KeyAlgorithm  RSA `
  -KeyLength     2048 `
  -HashAlgorithm SHA256 `
  -KeyUsage      KeyEncipherment, DataEncipherment `
  -NotAfter      (Get-Date).AddYears(10) `
  -CertStoreLocation Cert:\CurrentUser\My

$encPwd = ConvertTo-SecureString -String "<ENCRYPTION_PFX_PASSWORD>" -AsPlainText -Force
Export-PfxCertificate -Cert $encCert -FilePath .\cas-encryption.pfx -Password $encPwd

Write-Host "Encryption thumbprint: $($encCert.Thumbprint)"

# Remove from the build workstation's CurrentUser store — keep PFX + thumbprints only.
Remove-Item "Cert:\CurrentUser\My\$($signCert.Thumbprint)"
Remove-Item "Cert:\CurrentUser\My\$($encCert.Thumbprint)"
```

**Why two separate certs?** OpenIddict signs JWTs with one cert and encrypts them with
another. They have different key-usage flags. Don't reuse the same cert for both.

**Why self-signed?** These certs are never presented to a browser — they sign/encrypt
JWTs consumed by your own resource server (`OpenIddictValidationAspNetCoreDefaults`).
A self-signed cert is fine. Do **not** use your TLS server cert here.

Record the two thumbprints — they go into appsettings on each server.

### 2.3 Import on **every** IIS server

On **each** server, run as Administrator:

```powershell
$signPwd = ConvertTo-SecureString -String "<SIGNING_PFX_PASSWORD>" -AsPlainText -Force
Import-PfxCertificate `
  -FilePath .\cas-signing.pfx `
  -CertStoreLocation Cert:\LocalMachine\My `
  -Password $signPwd

$encPwd = ConvertTo-SecureString -String "<ENCRYPTION_PFX_PASSWORD>" -AsPlainText -Force
Import-PfxCertificate `
  -FilePath .\cas-encryption.pfx `
  -CertStoreLocation Cert:\LocalMachine\My `
  -Password $encPwd
```

Verify both are present:

```powershell
Get-ChildItem Cert:\LocalMachine\My | Where-Object Subject -like "*CollateralAppraisal*"
```

### 2.4 Grant the IIS app pool read access to the private keys

This is the step that's easy to forget — without it, the app crashes at startup with
`CryptographicException: Keyset does not exist`.

**GUI route**:
1. Run `certlm.msc` (Certificate Manager — Local Machine).
2. Navigate to **Personal → Certificates**.
3. Right-click the signing cert → **All Tasks → Manage Private Keys…**.
4. Click **Add…**, set Location to the local machine, and enter the app pool identity:
   `IIS AppPool\<YourAppPoolName>` (replace with your actual app pool name).
5. Grant **Read** permission. Click OK.
6. Repeat for the encryption cert.

**Scripted route** (PowerShell, runs on the server):

```powershell
function Grant-CertReadAccess {
    param([string]$Thumbprint, [string]$AppPoolIdentity)

    $cert = Get-ChildItem "Cert:\LocalMachine\My\$Thumbprint"
    $keyName = $cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName 2>$null
    if (-not $keyName) {
        # Modern CNG keys
        $keyName = ([System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)).Key.UniqueName
        $keyPath = "$env:ProgramData\Microsoft\Crypto\Keys\$keyName"
    } else {
        $keyPath = "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys\$keyName"
    }

    $acl = Get-Acl $keyPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($AppPoolIdentity, "Read", "Allow")
    $acl.AddAccessRule($rule)
    Set-Acl -Path $keyPath -AclObject $acl

    Write-Host "Granted Read to $AppPoolIdentity on $keyPath"
}

Grant-CertReadAccess -Thumbprint "<signing-thumbprint>"    -AppPoolIdentity "IIS AppPool\CAS"
Grant-CertReadAccess -Thumbprint "<encryption-thumbprint>" -AppPoolIdentity "IIS AppPool\CAS"
```

### 2.5 Get the certificate thumbprints

A **thumbprint** is the SHA-1 hash of the cert's binary DER encoding — a 40-character
hex string that uniquely identifies one cert in the Windows store. It is how `appsettings`
tells `CertificateProvider.LoadFromStore` which cert to pick out of `LocalMachine\My`.
The same PFX imported on two machines produces the same thumbprint on both, which is
exactly why both LB nodes can reference the same value.

Pick whichever method is convenient:

**A. Captured at generation time (easiest).** The script in §2.2 already prints it:
```powershell
Write-Host "Signing thumbprint: $($signCert.Thumbprint)"
```
Copy that string straight into appsettings.

**B. List the store on a server where the PFX is already imported:**
```powershell
Get-ChildItem Cert:\LocalMachine\My |
    Where-Object Subject -like "*CollateralAppraisal*" |
    Select-Object Thumbprint, Subject, NotAfter
```

**C. GUI (`certlm.msc`).** Personal → Certificates → double-click → Details tab →
scroll to *Thumbprint*.

**D. From a PFX file before importing:**
```powershell
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 `
    ("C:\path\to\cas-signing.pfx", "<password>")
$cert.Thumbprint
```

**Gotcha — invisible characters.** Copying out of `certlm.msc` can prefix a hidden
left-to-right mark before the first digit; the string *looks* identical but
`LoadFromStore` will fail with "certificate not found". Paste into a plain-text editor
to inspect, or strip non-hex characters:
```powershell
($cert.Thumbprint -replace '[^A-F0-9]','')
```
Spaces and case do not matter — .NET normalizes them — but invisible characters do.

### 2.6 Configure the application

`appsettings.Production.json.template` already carries the right shape — substitute your
release-pipeline variables for the tokens:

```jsonc
{
  "OAuth2": {
    "SigningCertificate": {
      "Source": "store",
      "StoreName": "My",
      "StoreLocation": "LocalMachine",
      "Thumbprint": "#{OAUTH2_SIGNING_CERT_THUMBPRINT}#"
    },
    "EncryptionCertificate": {
      "Source": "store",
      "StoreName": "My",
      "StoreLocation": "LocalMachine",
      "Thumbprint": "#{OAUTH2_ENCRYPTION_CERT_THUMBPRINT}#"
    }
  },
  "AppBaseUrl": "#{APP_BASE_URL}#"
}
```

**Critical**:

- `AppBaseUrl` must be the **public load-balancer URL** (e.g. `https://cas.example.com`),
  not the per-server hostname. OpenIddict stamps this into the JWT `iss` claim; mismatched
  issuers between nodes will fail validation on the other node.
- The same two thumbprints must be deployed to **both** servers.
- Strip any whitespace from thumbprints copied from `certlm.msc` (Windows likes to prefix
  an invisible LRM character).

### 2.7 Configure IIS for forwarded headers (ARR / external LB)

If you use ARR, enable `Reverse Proxy` and ensure the `X-Forwarded-Proto` and
`X-Forwarded-For` headers are preserved. The code at `Bootstrapper/Api/Program.cs`
already calls `UseForwardedHeaders()` first in the pipeline.

For tighter security, pin known proxy IPs (the LB private IP) by setting
`Microsoft__AspNetCore__HttpOverrides__ForwardedHeadersOptions__KnownProxies__0`
or by extending the `Configure<ForwardedHeadersOptions>` block in `Program.cs`.

### 2.8 Database (one-time)

The `AddDataProtectionKeys` migration adds `auth.DataProtectionKeys` and runs
automatically via `MigrationService` on the first node that boots. The other node will
find the table already populated and join the same keyring transparently.

If you prefer to pre-apply the migration manually:

```bash
dotnet ef database update \
  --project Modules/Auth/Auth \
  --startup-project Bootstrapper/Api \
  --context AuthDbContext
```

### 2.9 Smoke test the load-balanced deployment

> This is the test that would have caught the previous JWT decrypt failure.

1. Disable Server B at the LB. Hit `/connect/token` (or your login flow) → get an access
   token and a refresh token.
2. Re-enable Server B, disable Server A.
3. Using the **same** refresh token, call `POST /connect/token` with
   `grant_type=refresh_token`. **Must succeed**. (Pre-fix: failed with IDX10609.)
4. Using the **same** access token, hit any protected endpoint via Server B. **Must succeed**.
5. `SELECT TOP 5 Id, FriendlyName, Xml FROM auth.DataProtectionKeys` — at least one row,
   `Xml` contains the encrypted keyring.
6. `GET https://<lb>/.well-known/openid-configuration` — the `issuer` and all endpoint
   URLs must be the public LB hostname over HTTPS, **never** the internal hostname.
7. Antiforgery: `GET` a Razor page on Server A (sets the `__RequestVerificationToken`
   cookie), then `POST` to the same form via Server B with the cookie + `X-CSRF-TOKEN`
   header → must pass validation.

If any test fails, see §6 Troubleshooting.

### 2.10 Certificate rotation

The certs in §2.2 are valid for **10 years**. You will almost never touch them. When
the time comes (or if a key is ever compromised), the simple procedure below is
appropriate for an internal on-prem system. A zero-downtime alternative is documented
for completeness but is not normally needed.

#### Calendar reminder

When you generate the certs, immediately set a calendar reminder for **9 years out**
("CAS OAuth2 certs expire in ~12 months — schedule rotation"). The runbook lives in
git but the reminder does not — write it down somewhere people will see it.

#### Standard rotation (maintenance-window swap)

User impact: one forced re-login for every active session. Downtime: a few minutes
during the app-pool restart. Pick a Sunday or off-hours window.

1. Generate two new PFXs per §2.2 — name them `cas-signing-v2.pfx` and
   `cas-encryption-v2.pfx` so they're easy to distinguish.
2. Import to `LocalMachine\My` on both servers and grant app-pool ACLs (§2.3–§2.4).
3. Update `OAuth2:SigningCertificate.Thumbprint` and
   `OAuth2:EncryptionCertificate.Thumbprint` in `appsettings.Production.json` on both
   servers to the new thumbprints.
4. Recycle the app pool on Server A, verify it comes up healthy, then Server B.
5. Hit `/.well-known/openid-configuration` and confirm the `jwks_uri` document lists the
   new keys.
6. After 24 hours of stable operation, remove the old certs:
   ```powershell
   Remove-Item Cert:\LocalMachine\My\<old-signing-thumbprint>
   Remove-Item Cert:\LocalMachine\My\<old-encryption-thumbprint>
   ```

That's it. Refresh tokens issued before the swap will fail at next refresh — users
re-authenticate normally. Acceptable for an internal system.

#### Optional: zero-downtime rotation

Only needed if forced re-login is unacceptable (24/7 public SaaS, etc.). Requires a
small code change because the current `CertificateProvider` reads a single thumbprint.

1. Extend `CertificateProvider` (and the `OAuth2:*Certificate` config schema) to accept
   an array of thumbprints.
2. Make `AuthModule.cs` call `options.AddSigningCertificate(...)` once per cert (and
   the same for encryption). OpenIddict signs with the first cert and validates against
   any of them.
3. Generate + import the new certs (§2.2–§2.4).
4. Deploy with `[old, new]` thumbprints listed — OpenIddict will start signing with
   `old` and accept both.
5. Wait `max(refresh_token_lifetime)` = 7 days for old refresh tokens to drain.
6. Redeploy with `[new]` only as the configured cert, removing the old one.
7. `Remove-Item` the old cert from both stores.

If you ever need this, do the code change first and merge it before generating the new
certs — the rotation itself is then config-only.

### 2.11 SignalR + WebSockets (multi-server fan-out)

The app hosts two SignalR hubs: `/notificationHub` (Notification module) and
`/workflowHub` (Workflow module). Both rely on `IHubContext<T>` to push messages
to connected clients. When the app runs on two IIS boxes, two things have to be
true for push messages to reach every connected user:

1. A message published on Server A must reach clients connected to Server B.
   → solved by the **Redis backplane** wired in
   `Modules/Notification/Notification/NotificationModule.cs`, toggled via
   `SignalR:UseRedisBackplane` (set to `true` in `appsettings.Production.json`).
2. A SignalR client's connection must not break when the F5 routes a fresh HTTP
   request to a different backend than the one currently holding the connection.
   → solved by forcing **WebSockets-only** transport. A WebSocket is a single
   TCP socket pinned to one backend for the connection's lifetime; no LB sticky
   session is required, which keeps the §5 contract (`LB sticky sessions OFF`)
   intact.

#### 2.11.1 Per-server: install the Windows WebSocket Protocol feature

WebSocket support is a separate Windows feature. .NET 9 + ANCM will refuse the
upgrade without it. On **each** IIS server, as Administrator:

```powershell
Install-WindowsFeature -Name Web-WebSockets

# Verify
Get-WindowsFeature -Name Web-WebSockets
# InstallState : Installed

# Pick up the new feature
Restart-WebAppPool -Name "CAS"   # use the actual app pool name
```

The published `web.config` does **not** need a `<webSocket>` element — once the
feature is installed, IIS allows upgrades automatically. Do **not** add
`<webSocket enabled="false" />` anywhere; that disables it.

Confirm the published `web.config` uses `hostingModel="inprocess"` (default for
the .NET 9 SDK publish target). In-process passes the WS upgrade straight to
Kestrel without an extra hop and is the right choice for SignalR.

#### 2.11.2 App pool settings (confirm, don't randomly change)

| Setting | Value | Why |
|---|---|---|
| `.NET CLR Version` | `No Managed Code` | Standard for ANCM-hosted apps. |
| `Idle Time-out (minutes)` | `0` | Guarantees the worker is never killed mid-stream. |
| `Regular Time Interval (minutes)` | `0` | Recycle only on deploys; never on a clock. A scheduled recycle would drop every active WS at once across both boxes. |
| `Shutdown Time Limit (seconds)` | `90` | Lets in-flight WS connections close gracefully on pool recycle. |

#### 2.11.3 F5 BIG-IP: do nothing up front, file a ticket only with evidence

The F5 is owned by the bank's network team. **Do not request F5 changes as a
precondition of the SignalR rollout.** Modern BIG-IP (v13+) with a stock `http`
profile passes WebSocket upgrades through transparently: the F5 sees the
`Upgrade: websocket` header on the initial `GET`, load-balances to one pool
member, sees the `101 Switching Protocols`, and from that point treats the
connection as a raw TCP relay. SignalR's 15-second pings keep the F5's default
300s TCP idle timer reset indefinitely. Persistence stays `none` (which the §5
contract already requires).

In the common case, the IIS feature install plus the SPA client change (§2.11.4)
is everything we need — **zero F5 tickets**.

The three things that *can* break (and require a network ticket if and only if
verification §2.11.5 reveals them):

1. **WAF / ASM blocking the upgrade.** Banks frequently run F5 ASM in front of
   the HTTP profile. ASM's default HTTP-compliance policy can drop `Upgrade`
   requests on URLs it doesn't know. Symptom: WS handshake returns 400 / 403 or
   the connection is reset right after the `GET`. Ticket ask: disable ASM for
   `/notificationHub` and `/workflowHub`, or enable the **WebSocket Security**
   policy with those two URLs whitelisted.
2. **HTTP profile stripping `Upgrade` / `Connection` headers.** Symptom: client
   sees `200 OK` with no `Upgrade` response header (then errors out because the
   client is set to `skipNegotiation: true`). Ticket ask: attach a `websocket`
   profile, or move the HTTP profile to `transparent` for those URLs.
3. **TCP idle timeout shorter than ~30 seconds.** Rare but seen on hardened
   banking VIPs. Symptom: connection drops on a steady cadence ≈ idle timeout.
   Ticket ask: raise TCP idle to ≥ 60s (recommended 300s, ideal 3600s).

When filing the ticket, attach the failing browser DevTools screenshot and/or
`wscat` transcript so the network team has concrete evidence, not just
"WebSockets don't work."

If the network team needs paste-and-adapt TMSH, this is the *maximum* ask (only
the lines matching the actual failure mode are needed):

```tmsh
# Only if §2.11.5 V2 reveals WAF/upgrade-stripping issues:
create ltm profile websocket cas-ws-prof {
    masking unmask
    compression preserved
}
modify ltm virtual <cas-https-vip-name> {
    profiles add { cas-ws-prof { } }
}

# Only if §2.11.5 V3 reveals idle-timeout drops:
create ltm profile tcp cas-ws-tcp {
    defaults-from tcp
    idle-timeout 3600
}
modify ltm virtual <cas-https-vip-name> {
    profiles add { cas-ws-tcp { } }
}

# Persistence stays as it is (already 'none' per §5):
# modify ltm virtual <cas-https-vip-name> persist none
```

A couple of F5 behaviours we should *confirm* (no ticket needed — observable
from the app side):

- `access_token` query-string parameter is preserved (SignalR sends the JWT
  there because browsers can't add an `Authorization` header on a WS handshake).
  A 401 in V2 below is the smoking gun for this.
- `X-Forwarded-Proto` keeps being forwarded as `https`, so discovery URLs and
  any client-side redirects come back as `wss://` not `ws://` (already verified
  for the rest of the app per §2.7).

#### 2.11.4 Companion change in the frontend SPA repo

The SignalR JS client must be configured to skip negotiation and use WebSockets
directly. This change lives in the SPA repo
(`~/Developer/collateral-appraisal-system-app/`), not in this backend repo, but
it **must ship in the same release** as the IIS feature install — otherwise the
client will keep trying long-polling, which we never sticky-routed for.

At both `HubConnectionBuilder` call sites (one per hub):

```ts
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl(`${apiBaseUrl}/notificationHub`, {
    skipNegotiation: true,
    transport: HttpTransportType.WebSockets,
    withCredentials: true,
    accessTokenFactory: () => getAccessToken(),
  })
  .withAutomaticReconnect()
  .configureLogging(LogLevel.Information)
  .build();
```

#### 2.11.5 Verification (staging that mirrors prod)

Run in order. Stop at the first failure and address it before continuing.

- **V1 — IIS-only smoke, per server, F5 bypassed.** From a workstation that can
  reach each IIS server directly:
  ```bash
  npx wscat -c "wss://<iis-server-A-direct>/notificationHub?access_token=<jwt>"
  # Expect: Connected. Sending {"protocol":"json","version":1}<0x1e> returns {"type":6}<0x1e>.
  ```
  Repeat against Server B. If one fails, §2.11.1 is incomplete on that box.

- **V2 — Through F5 (the real client path).** Same `wscat` command against
  `wss://<f5-vip-fqdn>/notificationHub`. In a browser, DevTools Network tab must
  show the hub request as `101 Switching Protocols` with `Connection: Upgrade`
  and `Upgrade: websocket` headers in both request and response. A `200 OK`
  followed by polling means either the SPA still has negotiation enabled, or
  the F5 is stripping the upgrade (§2.11.3 case 1 or 2).

- **V3 — Idle survival.** Open the SPA, connect to `/notificationHub`, leave
  the tab idle for **65 minutes**. WS frame view should show pings every 15s
  the whole time, connection stays `Connected`. A drop at a fixed interval is
  §2.11.3 case 3.

- **V4 — Server-A drain test.** Set Server A's F5 pool member to `Disabled
  (gracefully)`. Active WS connections to A close; the SPA's
  `withAutomaticReconnect()` reconnects to Server B within ~5 seconds. New page
  loads while A is drained connect cleanly to B. Re-enable A, drain B, repeat.

- **V5 — End-to-end with the backplane.** Open SPA tab #1 (browser session A) —
  F5 routes to Server A (confirm via Server A's logs containing the new
  connection ID). Open SPA tab #2 (different browser, *same* user) — F5 routes
  to Server B. Trigger any flow that calls
  `IHubContext<NotificationHub>.Clients.User(userId).SendAsync(...)`. **Both**
  tabs must receive the notification. If only one does, the backplane is not
  publishing — check `cas-prod:*` keys via `redis-cli MONITOR`.

---

## 3. Development: macOS / Linux

You generally **do not** need to do anything. In `Development`, `AuthModule.cs` calls
`AddDevelopmentSigningCertificate()` and `AddDevelopmentEncryptionCertificate()`, which
auto-generate self-signed certs and persist them to the user keystore. They survive
restarts and require zero setup.

The sections below cover two situations where you might want to do more.

### 3.1 Recommended: just run the app

```bash
docker compose up -d
dotnet run --project Bootstrapper/Api
```

OpenIddict creates and reuses dev certs automatically. Skip the rest of this section.

### 3.2 Optional: test the production cert path locally

Useful when validating the `Source: file` branch of `CertificateProvider` before a release.

#### Generate two PFXs with OpenSSL (macOS / Linux)

```bash
# Signing cert
openssl req -x509 -nodes \
  -newkey rsa:2048 -sha256 \
  -days 365 \
  -keyout cas-signing.key \
  -out    cas-signing.crt \
  -subj "/CN=CollateralAppraisal-Signing-Dev" \
  -addext "keyUsage = critical, digitalSignature"

openssl pkcs12 -export \
  -inkey cas-signing.key \
  -in    cas-signing.crt \
  -out   cas-signing.pfx \
  -passout pass:devpassword \
  -name "CollateralAppraisal-Signing-Dev"

# Encryption cert
openssl req -x509 -nodes \
  -newkey rsa:2048 -sha256 \
  -days 365 \
  -keyout cas-encryption.key \
  -out    cas-encryption.crt \
  -subj "/CN=CollateralAppraisal-Encryption-Dev" \
  -addext "keyUsage = critical, keyEncipherment, dataEncipherment"

openssl pkcs12 -export \
  -inkey cas-encryption.key \
  -in    cas-encryption.crt \
  -out   cas-encryption.pfx \
  -passout pass:devpassword \
  -name "CollateralAppraisal-Encryption-Dev"

# Clean up loose .key / .crt files once the .pfx files exist
rm cas-signing.key cas-signing.crt cas-encryption.key cas-encryption.crt
```

#### Wire the file-source into a local `appsettings.Local.json` (gitignored)

> Do **not** commit PFX files or this config to git.

```jsonc
{
  "OAuth2": {
    "SigningCertificate": {
      "Source": "file",
      "Path": "/Users/<you>/secrets/cas-signing.pfx",
      "Password": "devpassword"
    },
    "EncryptionCertificate": {
      "Source": "file",
      "Path": "/Users/<you>/secrets/cas-encryption.pfx",
      "Password": "devpassword"
    }
  }
}
```

For passwords, prefer `dotnet user-secrets`:

```bash
dotnet user-secrets --project Bootstrapper/Api init
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:SigningCertificate:Source"   "file"
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:SigningCertificate:Path"     "$HOME/secrets/cas-signing.pfx"
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:SigningCertificate:Password" "devpassword"
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:EncryptionCertificate:Source"   "file"
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:EncryptionCertificate:Path"     "$HOME/secrets/cas-encryption.pfx"
dotnet user-secrets --project Bootstrapper/Api set "OAuth2:EncryptionCertificate:Password" "devpassword"
```

#### Run the prod code path locally

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project Bootstrapper/Api
```

You should see startup logs from `CertificateProvider`:
`Loading certificate from file: /Users/.../cas-signing.pfx`

Token responses will now be encrypted JWTs (vs. the unencrypted dev path).

### 3.3 Multi-developer parity (optional)

If your team wants every dev machine to load the same dev certs (so refresh tokens
survive `git pull` rebuilds across teammates), commit two **password-protected** PFXs
to a private location and document the password in your password manager — never the
repo. Most teams do not need this; the default auto-generated dev certs are fine.

---

## 4. Linux production hosting (optional, not used today)

If you ever migrate off Windows IIS:

1. Generate PFXs with OpenSSL as in §3.2 (but with `-days 1095` and proper passwords).
2. Deploy the two PFXs to `/etc/cas/certs/` on each app server, mode `0400`,
   owner = the app's runtime user.
3. Use `Source: file` in `appsettings.Production.json`. Read the password from an env
   var: `OAuth2__SigningCertificate__Password`.
4. Behind Nginx/HAProxy/etc., set the forwarded-header network whitelist:
   ```csharp
   options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
   ```

The rest (`AddSharedDataProtection<AuthDbContext>()`, `UseForwardedHeaders`) is identical.

---

## 5. Pre-deployment checklist

Tick every box before flipping traffic to the new build:

**Code**
- [ ] `Bootstrapper/Api/Program.cs` calls `AddSharedDataProtection<AuthDbContext>()`.
- [ ] `Bootstrapper/Api/Program.cs` calls `app.UseForwardedHeaders()` as the **first** middleware.
- [ ] `Modules/Auth/Auth/AuthModule.cs` hard-fails when `OAuth2:SigningCertificate` /
      `EncryptionCertificate` config is missing in non-Development.
- [ ] `Modules/Auth/Auth/Infrastructure/AuthDbContext.cs` implements `IDataProtectionKeyContext`.
- [ ] EF migration `AddDataProtectionKeys` is present and applied.

**Infrastructure (per server)**
- [ ] Signing PFX imported to `LocalMachine\My`.
- [ ] Encryption PFX imported to `LocalMachine\My`.
- [ ] Both private keys grant **Read** to the app pool identity.
- [ ] Both servers can reach the SQL Server hosting `auth.DataProtectionKeys`.
- [ ] LB forwards `X-Forwarded-Proto` and `X-Forwarded-For` headers.
- [ ] LB sticky sessions are **OFF** (proves the keyring fix actually works).

**Configuration**
- [ ] `appsettings.Production.json` has identical `OAuth2:SigningCertificate.Thumbprint`
      on both servers.
- [ ] `appsettings.Production.json` has identical `OAuth2:EncryptionCertificate.Thumbprint`
      on both servers.
- [ ] `AppBaseUrl` points to the public LB URL, identical on both servers.
- [ ] Both servers use the same `Database` connection string.

**SignalR (per §2.11)**
- [ ] `Get-WindowsFeature Web-WebSockets` returns `Installed` on **both** IIS servers.
- [ ] `appsettings.Production.json` has `SignalR:UseRedisBackplane = true`.
- [ ] SPA bundle ships `skipNegotiation: true, transport: WebSockets` on both
      `/notificationHub` and `/workflowHub` `HubConnectionBuilder` call sites.
- [ ] No F5 ticket filed up front — F5 stays untouched unless §2.11.5 V2/V3 fails
      with concrete evidence.

**Verification**
- [ ] `/.well-known/openid-configuration` advertises the public LB URL.
- [ ] At least one row in `auth.DataProtectionKeys`.
- [ ] Token issued on Node A redeems successfully on Node B (LB-toggle test, §2.9).
- [ ] SignalR staging V2 (WS handshake through F5 as-is) passes.
- [ ] SignalR staging V3 (1-hour idle survival through F5 as-is) passes.
- [ ] SignalR staging V5 (cross-IIS fan-out via Redis backplane) passes.

---

## 6. Troubleshooting

### `CryptographicException: Keyset does not exist` at startup
Missing private-key ACL. Re-run §2.4 for the cert whose thumbprint appears in the stack
trace.

### `IDX10503: Signature validation failed`
The two servers have different **signing** certs. Re-import the signing PFX on the
deviating server and restart. Verify both have the same `Thumbprint` in
`Cert:\LocalMachine\My`.

### `IDX10609: Decryption failed`
The two servers have different **encryption** certs. Same fix as above for the
encryption PFX.

### `The antiforgery token could not be decrypted` after a deploy
Either:
- `auth.DataProtectionKeys` table is empty (migration didn't run) — apply manually
  (§2.8), then **restart both servers** so the keyring is regenerated and persisted.
- Both servers point at different databases.
- `SetApplicationName("CollateralAppraisalSystem")` differs between nodes — check
  `Shared/Shared/Security/DataProtectionExtensions.cs` is identical in both deploy
  artifacts.

### Discovery doc shows internal hostnames or `http://`
`UseForwardedHeaders` isn't seeing the proxy. Check IIS/ARR is forwarding
`X-Forwarded-Proto` and `X-Forwarded-For` and that the middleware runs **before**
`UseHttpsRedirection`.

### App refuses to start with "Production requires OAuth2:SigningCertificate..."
Working as intended — fill in `OAuth2:SigningCertificate` and
`OAuth2:EncryptionCertificate` in `appsettings.<env>.json` and redeploy.

### Refresh token issued before deploy now rejected after deploy
Expected if you replaced the encryption cert without keeping the old one. Either keep
both certs during rotation (§2.10) or accept a one-time forced re-login.

---

## 7. Reference

- ASP.NET Core Data Protection — <https://learn.microsoft.com/aspnet/core/security/data-protection/>
- OpenIddict signing & encryption credentials — <https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html>
- `IDataProtectionKeyContext` — <https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers#entity-framework-core>
- `ForwardedHeadersOptions` — <https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer>
