# Authentication

Weft supports five AAD flows, all via MSAL.

| Mode | When to use | Where secrets live |
|---|---|---|
| `ServicePrincipalCertStore` | Windows production runners (Octopus Tentacle, TeamCity agent) | Windows cert store, referenced by thumbprint |
| `ServicePrincipalCertFile` | Cross-platform CI (GitHub Actions, Linux agents) | `.pfx` file at a known path + password from env |
| `ServicePrincipalSecret` | Last resort; avoid in prod | Env var / secret manager |
| `Interactive` | Dev machine, ad-hoc deploys | Browser popup, MSAL cache |
| `DeviceCode` | Headless dev boxes without a browser | Paste code into a browser on another device |

## Setting up a Service Principal

1. In Azure portal → Entra ID → App registrations → New registration.
2. Note the **Application (client) ID** and **Directory (tenant) ID**.
3. Add a certificate (recommended) OR a client secret (less preferred). For cert:
   - Generate a self-signed cert: `openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -keyout key.pem -out cert.pem -subj "/CN=weft-deploy"`.
   - Convert to `.pfx`: `openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem`.
   - Upload `cert.pem` to the app registration (**Certificates & secrets → Upload certificate**).
4. In Power BI / Fabric Admin Portal → Tenant settings, enable:
   - **Allow service principals to use Power BI APIs** (and a security group the SP is in).
   - **Allow XMLA endpoints and Analyze in Excel** (on the workspace's Premium capacity).
5. Add the SP as an **Admin** member of the target workspace.

## Using each mode

### ServicePrincipalCertStore (Windows prod)

Install the `.pfx` into `LocalMachine\My` on the Octopus Tentacle / TeamCity agent:

```powershell
Import-PfxCertificate -FilePath C:\weft\cert.pfx `
  -CertStoreLocation Cert:\LocalMachine\My `
  -Password (ConvertTo-SecureString -String 'your-password' -AsPlainText -Force)
```

Note the thumbprint (`Get-ChildItem Cert:\LocalMachine\My`). Config:

```yaml
profiles:
  prod:
    auth:
      mode: ServicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"
      certStoreLocation: LocalMachine
      certStoreName: My
```

### ServicePrincipalCertFile (Linux/macOS CI)

```yaml
profiles:
  prod:
    auth:
      mode: ServicePrincipalCertFile
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certPath: "${WEFT_CERT_PATH}"
      certPassword: "${WEFT_CERT_PASSWORD}"
```

In the CI runner, drop the `.pfx` on disk (e.g., via Octopus certificate variable) and set `WEFT_CERT_PATH` + `WEFT_CERT_PASSWORD` as sensitive env vars.

### ServicePrincipalSecret

```yaml
auth:
  mode: ServicePrincipalSecret
  tenantId: "${WEFT_TENANT_ID}"
  clientId: "${WEFT_SP_CLIENT_ID}"
  clientSecret: "${WEFT_CLIENT_SECRET}"
```

### Interactive (dev)

```yaml
auth:
  mode: Interactive
  tenantId: "${WEFT_TENANT_ID}"
  clientId: "${WEFT_CLIENT_ID}"
```

First run opens a browser tab; subsequent runs use the MSAL cache (typically `~/.cache/msal` on Unix, `%LOCALAPPDATA%\.IdentityService` on Windows).

### DeviceCode (headless)

```yaml
auth:
  mode: DeviceCode
  tenantId: "${WEFT_TENANT_ID}"
  clientId: "${WEFT_CLIENT_ID}"
```

Prints a code. You paste it into <https://microsoft.com/devicelogin> on another device, sign in, and the CLI resumes.

## Troubleshooting

- **`401 Unauthorized`** — SP isn't an Admin on the workspace, or XMLA is disabled on the tenant.
- **`ServicePrincipalNotEnabled`** — tenant hasn't enabled the service-principal-for-Power-BI-API setting.
- **`CertificateNotFound`** — thumbprint mismatch; `Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -match 'weft' }`.
- **`MSAL cache corrupted`** (Interactive) — delete the cache directory and re-auth.
