# CasSecretTool — Encrypting Configuration Secrets (Operator Manual)

**Audience:** deployment / operations staff on the app servers.
**Purpose:** replace every plaintext password in `appsettings.Production.json` with an encrypted
`ENC:v1:…` value, so no credential sits in plaintext on disk.

This manual is a standalone how-to for the tool. For the surrounding deployment steps (creating
and importing the certificate, granting the app pool access) see
[`multi-server-deployment.md`](./multi-server-deployment.md) **§2.12**.

---

## 1. What the tool does

`CasSecretTool` encrypts a secret value so it can be pasted into `appsettings.Production.json` as
`ENC:v1:<base64>`. The application decrypts it automatically at startup using a certificate in the
Windows certificate store. The tool uses the *same code* the application uses to decrypt, so a
value it produces is guaranteed readable by the app.

It has two actions:

| Action | What it does |
|---|---|
| **encrypt** | Turns a plaintext password into an `ENC:v1:…` value to paste into config. |
| **verify** | Decrypts an `ENC:v1:…` value and shows a **masked** confirmation, to check a value is correct *before* restarting the app. |

The tool **never writes anything to disk** and never logs the secret.

---

## 2. Prerequisites

1. **The secrets certificate is installed** in `LocalMachine\My` on this server, **with its
   private key** (deployment guide §2.12.1–2.12.2). Subject: `CN=CollateralAppraisal-Secrets`.
2. **You know its thumbprint** — the same value that is (or will be) in
   `appsettings.Production.json` under `Secrets:CertificateThumbprint`.
3. The tool is present under the release folder: `C:\Deploy\temp\<version>\tools\`.

> If the tool reports *"No certificates with a private key found"*, the certificate has not been
> imported yet — stop and complete deployment guide §2.12.2 first.

---

## 3. Which certificate to select — IMPORTANT

When the tool lists certificates, **select the one whose thumbprint matches
`Secrets:CertificateThumbprint` in `appsettings.Production.json`** — normally
`CN=CollateralAppraisal-Secrets`.

```
Certificates with a private key:
  [1] CN=CollateralAppraisal-Secrets     (A1B2C3…)   ←  SELECT THIS
  [2] CN=CollateralAppraisal-Signing      (D4E5F6…)   ←  do NOT use (OAuth2 JWT signing)
  [3] CN=CollateralAppraisal-Encryption   (E7F8A9…)   ←  do NOT use (OAuth2 JWT encryption)
```

**Why it matters:** the app decrypts with exactly one certificate — the one named in the config.
If you encrypt a value with a *different* certificate, the app will **fail to start** with a
`Failed to decrypt configuration value '<key>'` error. Encrypting always appears to succeed
(encryption only needs the public key); the mismatch is only caught at startup — which is why
**§6 (verify) is mandatory**.

Do **not** use the OAuth2 signing/encryption certificates: they rotate on a different schedule,
and reusing them would make every secret undecryptable the day those certs are rotated.

---

## 4. Encrypt a secret (interactive — recommended)

On the app server:

```powershell
cd C:\Deploy\temp\<version>\tools
.\CasSecretTool.exe
```

Follow the prompts:

```
CAS Secret Tool
===============
Certificates with a private key:
  [1] CN=CollateralAppraisal-Secrets  (A1B2C3…)  [LocalMachine]
Select cert: 1
(e)ncrypt or (v)erify: e
Value: **********           ←  type/paste the real password; it is NOT shown on screen
ENC:v1:AAELc4zuwOA/ql4O...  ←  copy this ENTIRE line
```

Copy the whole `ENC:v1:…` line into the matching field in `appsettings.Production.json`:

```json
"Mail": {
  "Password": "ENC:v1:AAELc4zuwOA/ql4O..."
}
```

Repeat for every secret in the checklist (§8).

---

## 5. Encrypt a secret (scriptable — optional)

If you prefer flags (e.g. inside a script):

```powershell
# Prompts for the value (not echoed); prints the ENC:v1: result to stdout.
.\CasSecretTool.exe protect --thumbprint A1B2C3D4E5F6...

# Or pipe the value in (the prompt goes to stderr, so stdout is only the result):
"P@ssw0rd" | .\CasSecretTool.exe protect --thumbprint A1B2C3D4E5F6...
```

---

## 6. Verify a value BEFORE restarting the app — mandatory

After pasting a value into config, confirm it decrypts. This runs the **exact** decrypt path the
application uses, so a passing verify guarantees the app can read it.

```powershell
.\CasSecretTool.exe verify --thumbprint A1B2C3D4E5F6... --value "ENC:v1:AAELc4zu..."
```

Expected output:

```
OK — decrypts successfully to: P@s****
```

- The confirmation is **masked** (first 3 chars + `****`) — enough to recognise the password
  without showing it on screen.
- If verify **fails**, you selected the wrong certificate, or the value was truncated when
  pasted. Re-encrypt with the correct certificate. Do **not** restart the app until verify
  passes.

You can also verify interactively: run `.\CasSecretTool.exe`, pick the cert, choose `v`, and paste
the value.

---

## 7. Point the application at the certificate

Set the thumbprint in `appsettings.Production.json` (the deployment template already has the
token):

```json
"Secrets": {
  "CertificateThumbprint": "A1B2C3D4E5F6..."
}
```

If this is left blank, the app falls back to `DataProtection:CertificateThumbprint`.
Strip any spaces or invisible characters copied from `certlm.msc` (see deployment guide §2.5).

---

## 8. Which values must be encrypted

Encrypt every value below that is present and non-empty:

- [ ] `ConnectionStrings:Database` (the whole string — it contains `Password=…`)
- [ ] `ConnectionStrings:Hangfire` (same)
- [ ] `RabbitMQ:Password`
- [ ] `Mail:Password`
- [ ] `Ldap:BindPassword` (only if a service-account bind is used)
- [ ] `SeedData:AdminUser:Password`
- [ ] `FileTransfer:Inbound:Sftp:Password`
- [ ] `FileTransfer:Outbound:Sftp:Password`

> `ConnectionStrings:Redis` has no password by default — encrypt it only if yours does.

At startup, the application logs an **error** naming any of these keys still left in plaintext
(the value is never logged), so a missed one is easy to spot in the logs.

---

## 9. Final check on the server

Confirm no plaintext password remains — this should return **no output**:

```powershell
Select-String -Path appsettings.Production.json -Pattern 'Password' |
    Select-String 'ENC:v1:' -NotMatch
```

Then start the application and confirm it boots. A wrong certificate or a corrupted value fails
fast at startup, naming the offending key (never its value).

Repeat this on **every** app server — the same certificate (same thumbprint) must be installed on
all of them.

---

## 10. Rotating the certificate

There is no dual-key transition, so keep the window short:

1. Install the new secrets certificate on **all** app servers (deployment guide §2.12.1–2.12.2).
2. Re-encrypt **every** `ENC:v1:` value with the new certificate using this tool.
3. Update `Secrets:CertificateThumbprint` to the new thumbprint.
4. Restart the application on each node.

Add the new certificate's expiry to the same monitoring that covers the OAuth2 certificates.

---

## 11. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `No certificates with a private key found` | Secrets cert not imported, or imported without its private key. Redo deployment guide §2.12.2. |
| `Certificate with thumbprint '…' was not found` | Wrong thumbprint, or cert not on this server. Check `certlm.msc` → Personal → Certificates. |
| App start: `Failed to decrypt configuration value '<key>'` | The value was encrypted with a different certificate than `Secrets:CertificateThumbprint`. Re-encrypt with the correct cert and **verify**. |
| `…has no accessible private key` | The app pool identity lacks read access to the private key. Grant it (deployment guide §2.12.2 `Grant-CertReadAccess`). |
| verify shows the wrong password | You encrypted the wrong value — re-encrypt the correct one. |

---

## 12. Security notes

- The tool reads the value **without echoing** it and prints prompts to stderr so a piped result
  cannot leak the secret into a capture.
- No secret value is ever written to disk or logged — by the tool or by the application.
- Losing the certificate means the encrypted values are **unrecoverable**. Keep the original
  plaintext values **and** the certificate PFX (with its password) in the bank's secrets vault.
