# Weft Plan 2 — Auth + XMLA + CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the pure-logic core from Plan 1 to the outside world: AAD token acquisition (5 providers), TOM `Server` connections to Power BI Premium XMLA endpoints, per-table refresh execution with progress polling, and a `weft` CLI exposing `validate / plan / deploy / refresh / restore-history / inspect` commands. End-state: a single `weft` binary that performs an end-to-end deploy (load → diff → manifest → execute → refresh) against a real Premium workspace.

**Architecture:** Three new projects layered on Plan 1's `Weft.Core`. `Weft.Auth` produces `AccessToken`s via MSAL (cert-store, cert-file, secret, interactive, device). `Weft.Xmla` wraps TOM's `Server` to read target databases, execute TMSL, and run refreshes with progress polling. `Weft.Cli` (System.CommandLine) wires everything via interfaces (`IAuthProvider`, `ITargetReader`, `IXmlaExecutor`, `IRefreshRunner`) declared in `Weft.Core/Abstractions/` so the CLI is unit-testable with mocks.

**Tech Stack:** .NET 10, Microsoft.Identity.Client (MSAL) 4.83.3 (already pinned), Microsoft.AnalysisServices.NetCore.retail.amd64 19.84.1 (TOM), System.CommandLine 2.0.0-beta5, Spectre.Console 0.49.1 (rich TTY output), xUnit, FluentAssertions, NSubstitute 5.x (mocking).

**Reference spec:** `docs/superpowers/specs/2026-04-17-weft-design.md`. Sections this plan implements:
- §4 CLI commands (validate / plan / deploy / refresh / restore-history / inspect)
- §5.2 TargetReader, §5.5 XmlaExecutor, §5.6 AuthProvider
- §6 Data Flow steps 1–17 (full deploy pipeline)
- §6 step 12a post-deploy partition integrity gate
- §6 step 13 per-table refresh-type matrix (`Policy` + `ApplyRefreshPolicy` for incremental; `Full` for static; targeted-partition for dynamic)
- §7A.7 bookmark CLI flags (`--reset-bookmarks`)
- §9 exit codes 2–9
- §9.3 observability (structured JSON logging)

**Out of this plan (deferred):**
- YAML config loading, parameter resolver, hooks — Plan 3.
- TeamCity / Octopus / packaging / docs / samples — Plan 4.
- `restore-history` full date-range materialization is implemented but not wired to a real source-system rebind (covered by Plan 3 parameter overrides).

**Plan 1 carry-over:** Important issue 2 (`RefreshPolicyComparer` non-Basic subclass) and Important 5 (`PartitionIntegrityValidator` asymmetric bookmark check) — neither blocks Plan 2; tracked in Task 32 cleanup.

---

## File structure (locked in by this plan)

```
weft/
├── src/
│   ├── Weft.Core/
│   │   └── Abstractions/                                # NEW: interfaces consumed by CLI
│   │       ├── AccessToken.cs
│   │       ├── IAuthProvider.cs
│   │       ├── ITargetReader.cs
│   │       ├── IXmlaExecutor.cs
│   │       ├── IRefreshRunner.cs
│   │       ├── IPartitionManifestStore.cs
│   │       └── XmlaExecutionResult.cs
│   │
│   ├── Weft.Auth/                                       # NEW project
│   │   ├── Weft.Auth.csproj
│   │   ├── AuthMode.cs
│   │   ├── AuthOptions.cs
│   │   ├── AuthProviderFactory.cs
│   │   ├── ServicePrincipalSecretAuth.cs
│   │   ├── ServicePrincipalCertFileAuth.cs
│   │   ├── ServicePrincipalCertStoreAuth.cs
│   │   ├── InteractiveAuth.cs
│   │   ├── DeviceCodeAuth.cs
│   │   └── CertificateLoader.cs
│   │
│   ├── Weft.Xmla/                                       # NEW project
│   │   ├── Weft.Xmla.csproj
│   │   ├── XmlaConnectionStringBuilder.cs
│   │   ├── TargetReader.cs
│   │   ├── XmlaExecutor.cs
│   │   ├── RefreshTypeSelector.cs
│   │   ├── RefreshCommandBuilder.cs
│   │   ├── RefreshRunner.cs
│   │   ├── FilePartitionManifestStore.cs
│   │   └── ServerConnectionFactory.cs
│   │
│   └── Weft.Cli/                                        # NEW project
│       ├── Weft.Cli.csproj
│       ├── Program.cs
│       ├── ExitCodes.cs
│       ├── Output/
│       │   ├── IConsoleWriter.cs
│       │   ├── HumanConsoleWriter.cs
│       │   └── JsonConsoleWriter.cs
│       ├── Options/
│       │   ├── CommonOptions.cs
│       │   └── ProfileResolver.cs                       # bridge to Plan-3 config; CLI-flag-driven for now
│       └── Commands/
│           ├── ValidateCommand.cs
│           ├── PlanCommand.cs
│           ├── DeployCommand.cs
│           ├── RefreshCommand.cs
│           ├── RestoreHistoryCommand.cs
│           └── InspectCommand.cs
│
└── test/
    ├── Weft.Auth.Tests/
    │   ├── Weft.Auth.Tests.csproj
    │   ├── CertificateLoaderTests.cs
    │   ├── AuthOptionsValidationTests.cs
    │   ├── AuthProviderFactoryTests.cs
    │   └── fixtures/
    │       ├── test-cert.pfx                            # generated 1024-bit, no real CA
    │       └── test-cert.password.txt
    │
    ├── Weft.Xmla.Tests/
    │   ├── Weft.Xmla.Tests.csproj
    │   ├── XmlaConnectionStringBuilderTests.cs
    │   ├── RefreshTypeSelectorTests.cs
    │   ├── RefreshCommandBuilderTests.cs
    │   └── FilePartitionManifestStoreTests.cs
    │
    ├── Weft.Cli.Tests/
    │   ├── Weft.Cli.Tests.csproj
    │   ├── ExitCodesTests.cs
    │   ├── ValidateCommandTests.cs
    │   ├── PlanCommandTests.cs
    │   ├── DeployCommandTests.cs
    │   ├── InspectCommandTests.cs
    │   └── Helpers/
    │       └── CliTestHost.cs                           # in-process command runner with mock interfaces
    │
    └── Weft.Integration.Tests/                          # NEW (gated by env var)
        ├── Weft.Integration.Tests.csproj
        ├── IntegrationTestFact.cs                       # custom [Fact] that skips when env vars missing
        ├── EndToEndDeployTests.cs
        └── RefreshTests.cs
```

---

## Tasks

### Task 1: Add interfaces to `Weft.Core/Abstractions`

**Files:**
- Create: `src/Weft.Core/Abstractions/AccessToken.cs`
- Create: `src/Weft.Core/Abstractions/IAuthProvider.cs`
- Create: `src/Weft.Core/Abstractions/ITargetReader.cs`
- Create: `src/Weft.Core/Abstractions/IXmlaExecutor.cs`
- Create: `src/Weft.Core/Abstractions/IRefreshRunner.cs`
- Create: `src/Weft.Core/Abstractions/IPartitionManifestStore.cs`
- Create: `src/Weft.Core/Abstractions/XmlaExecutionResult.cs`

These interfaces let the CLI depend on contracts, not implementations, and lets unit tests mock the I/O layer.

- [ ] **Step 1: Define `AccessToken`**

`src/Weft.Core/Abstractions/AccessToken.cs`:
```csharp
namespace Weft.Core.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresOnUtc);
```

- [ ] **Step 2: Define `IAuthProvider`**

`src/Weft.Core/Abstractions/IAuthProvider.cs`:
```csharp
namespace Weft.Core.Abstractions;

public interface IAuthProvider
{
    Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Define `ITargetReader` and `XmlaExecutionResult`**

`src/Weft.Core/Abstractions/ITargetReader.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Abstractions;

public interface ITargetReader
{
    Task<Database> ReadAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        CancellationToken cancellationToken = default);
}
```

`src/Weft.Core/Abstractions/XmlaExecutionResult.cs`:
```csharp
namespace Weft.Core.Abstractions;

public sealed record XmlaExecutionResult(
    bool Success,
    IReadOnlyList<string> Messages,
    TimeSpan Duration);
```

- [ ] **Step 4: Define `IXmlaExecutor`**

`src/Weft.Core/Abstractions/IXmlaExecutor.cs`:
```csharp
namespace Weft.Core.Abstractions;

public interface IXmlaExecutor
{
    Task<XmlaExecutionResult> ExecuteAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        string tmslJson,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Define `IRefreshRunner`**

`src/Weft.Core/Abstractions/IRefreshRunner.cs`:
```csharp
using Weft.Core.Diffing;

namespace Weft.Core.Abstractions;

public sealed record RefreshRequest(
    string WorkspaceUrl,
    string DatabaseName,
    AccessToken Token,
    ChangeSet ChangeSet,
    string? EffectiveDateUtc = null);

public interface IRefreshRunner
{
    Task<XmlaExecutionResult> RefreshAsync(
        RefreshRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Define `IPartitionManifestStore`**

`src/Weft.Core/Abstractions/IPartitionManifestStore.cs`:
```csharp
using Weft.Core.Partitions;

namespace Weft.Core.Abstractions;

public interface IPartitionManifestStore
{
    string Write(PartitionManifest manifest, string artifactsDirectory, string label);
    PartitionManifest Read(string path);
}
```

- [ ] **Step 7: Build + commit**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet build
```
Expected: 0 warnings, 0 errors. Tests still 41 passing (no test changes).

```bash
git add src/Weft.Core/Abstractions/
git commit -m "feat(core): add Abstractions for Auth/XMLA/Refresh/ManifestStore"
```

---

### Task 2: Create `Weft.Auth` project

**Files:**
- Create: `src/Weft.Auth/Weft.Auth.csproj`

- [ ] **Step 1: New library**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
mkdir -p src/Weft.Auth
cd src/Weft.Auth
dotnet new classlib -o . --force
rm -f Class1.cs
```

- [ ] **Step 2: Edit csproj**

Replace contents of `src/Weft.Auth/Weft.Auth.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Identity.Client" Version="4.83.3" />
    <ProjectReference Include="..\Weft.Core\Weft.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add to solution + build**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add src/Weft.Auth/Weft.Auth.csproj
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Weft.Auth/ weft.sln
git commit -m "feat(auth): add Weft.Auth class library with MSAL dependency"
```

---

### Task 3: `AuthMode` enum + `AuthOptions`

**Files:**
- Create: `src/Weft.Auth/AuthMode.cs`
- Create: `src/Weft.Auth/AuthOptions.cs`

- [ ] **Step 1: Enum**

`src/Weft.Auth/AuthMode.cs`:
```csharp
namespace Weft.Auth;

public enum AuthMode
{
    ServicePrincipalSecret,
    ServicePrincipalCertFile,
    ServicePrincipalCertStore,
    Interactive,
    DeviceCode
}
```

- [ ] **Step 2: Options**

`src/Weft.Auth/AuthOptions.cs`:
```csharp
using System.Security.Cryptography.X509Certificates;

namespace Weft.Auth;

public sealed record AuthOptions(
    AuthMode Mode,
    string TenantId,
    string ClientId,
    string? ClientSecret = null,
    string? CertPath = null,
    string? CertPassword = null,
    string? CertThumbprint = null,
    StoreLocation CertStoreLocation = StoreLocation.LocalMachine,
    StoreName CertStoreName = StoreName.My,
    string? RedirectUri = null);

public sealed class AuthOptionsValidationException : Exception
{
    public AuthOptionsValidationException(string message) : base(message) {}
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Auth/AuthMode.cs src/Weft.Auth/AuthOptions.cs
git commit -m "feat(auth): AuthMode enum and AuthOptions record"
```

---

### Task 4: Create `Weft.Auth.Tests` project + `AuthOptionsValidationTests`

**Files:**
- Create: `test/Weft.Auth.Tests/Weft.Auth.Tests.csproj`
- Create: `test/Weft.Auth.Tests/AuthOptionsValidationTests.cs`
- Create: `src/Weft.Auth/AuthOptionsValidator.cs`

- [ ] **Step 1: Test project**

```bash
mkdir -p test/Weft.Auth.Tests
cd test/Weft.Auth.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

Edit `test/Weft.Auth.Tests/Weft.Auth.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <Using Include="Xunit" />
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Weft.Auth\Weft.Auth.csproj" />
  </ItemGroup>
</Project>
```

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add test/Weft.Auth.Tests/Weft.Auth.Tests.csproj
```

- [ ] **Step 2: Failing tests**

`test/Weft.Auth.Tests/AuthOptionsValidationTests.cs`:
```csharp
using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthOptionsValidationTests
{
    [Fact]
    public void Secret_mode_requires_ClientSecret()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*ClientSecret*");
    }

    [Fact]
    public void CertFile_mode_requires_CertPath_and_CertPassword()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalCertFile, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*CertPath*");
    }

    [Fact]
    public void CertStore_mode_requires_CertThumbprint()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalCertStore, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*CertThumbprint*");
    }

    [Fact]
    public void Interactive_mode_validates_with_minimal_options()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().NotThrow();
    }

    [Fact]
    public void Tenant_and_client_must_be_non_empty()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*TenantId*");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~AuthOptionsValidationTests
```
Expected: FAIL (`AuthOptionsValidator` not defined).

- [ ] **Step 3: Implement validator**

`src/Weft.Auth/AuthOptionsValidator.cs`:
```csharp
namespace Weft.Auth;

public static class AuthOptionsValidator
{
    public static void Validate(AuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId))
            throw new AuthOptionsValidationException("TenantId is required.");
        if (string.IsNullOrWhiteSpace(options.ClientId))
            throw new AuthOptionsValidationException("ClientId is required.");

        switch (options.Mode)
        {
            case AuthMode.ServicePrincipalSecret:
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    throw new AuthOptionsValidationException(
                        "ClientSecret is required for ServicePrincipalSecret mode.");
                break;

            case AuthMode.ServicePrincipalCertFile:
                if (string.IsNullOrWhiteSpace(options.CertPath))
                    throw new AuthOptionsValidationException(
                        "CertPath is required for ServicePrincipalCertFile mode.");
                if (options.CertPassword is null)
                    throw new AuthOptionsValidationException(
                        "CertPassword is required for ServicePrincipalCertFile mode (use empty string if cert is unprotected).");
                break;

            case AuthMode.ServicePrincipalCertStore:
                if (string.IsNullOrWhiteSpace(options.CertThumbprint))
                    throw new AuthOptionsValidationException(
                        "CertThumbprint is required for ServicePrincipalCertStore mode.");
                break;

            case AuthMode.Interactive:
            case AuthMode.DeviceCode:
                break;
        }
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~AuthOptionsValidationTests
```
Expected: 5 PASS.

```bash
git add src/Weft.Auth/AuthOptionsValidator.cs test/Weft.Auth.Tests/ weft.sln
git commit -m "feat(auth): AuthOptionsValidator + Weft.Auth.Tests project"
```

---

### Task 5: `CertificateLoader` (load .pfx file)

**Files:**
- Create: `src/Weft.Auth/CertificateLoader.cs`
- Create: `test/Weft.Auth.Tests/fixtures/test-cert.pfx` (generated)
- Create: `test/Weft.Auth.Tests/fixtures/test-cert.password.txt`
- Create: `test/Weft.Auth.Tests/CertificateLoaderTests.cs`

- [ ] **Step 1: Generate test certificate**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy/test/Weft.Auth.Tests
mkdir -p fixtures
echo 'WeftTestCertPwd!2026' > fixtures/test-cert.password.txt
openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
  -keyout /tmp/weft-test-key.pem -out /tmp/weft-test-cert.pem \
  -subj "/CN=weft-test/O=Weft/C=US"
openssl pkcs12 -export -out fixtures/test-cert.pfx \
  -inkey /tmp/weft-test-key.pem -in /tmp/weft-test-cert.pem \
  -password file:fixtures/test-cert.password.txt
rm -f /tmp/weft-test-key.pem /tmp/weft-test-cert.pem
ls fixtures/
```
Expected: `fixtures/test-cert.pfx` and `fixtures/test-cert.password.txt`.

Add copy-to-output to `Weft.Auth.Tests.csproj`:
```xml
<ItemGroup>
  <None Update="fixtures\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: Failing test**

`test/Weft.Auth.Tests/CertificateLoaderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class CertificateLoaderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Loads_pfx_file_with_password()
    {
        var pfx = FixturePath("test-cert.pfx");
        var pwd = File.ReadAllText(FixturePath("test-cert.password.txt")).TrimEnd();

        var cert = CertificateLoader.LoadFromFile(pfx, pwd);

        cert.Should().NotBeNull();
        cert.Subject.Should().Contain("CN=weft-test");
        cert.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void Throws_on_missing_pfx()
    {
        var act = () => CertificateLoader.LoadFromFile("/no/such/cert.pfx", "x");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Throws_on_wrong_password()
    {
        var pfx = FixturePath("test-cert.pfx");
        var act = () => CertificateLoader.LoadFromFile(pfx, "wrong-password");
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~CertificateLoaderTests
```
Expected: FAIL.

- [ ] **Step 3: Implement loader**

`src/Weft.Auth/CertificateLoader.cs`:
```csharp
using System.Security.Cryptography.X509Certificates;

namespace Weft.Auth;

public static class CertificateLoader
{
    public static X509Certificate2 LoadFromFile(string pfxPath, string password)
    {
        if (!File.Exists(pfxPath))
            throw new FileNotFoundException($"Certificate file not found: {pfxPath}", pfxPath);

        // .NET 10: prefer X509CertificateLoader over deprecated X509Certificate2 ctor.
        return X509CertificateLoader.LoadPkcs12FromFile(
            pfxPath,
            password,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
    }

    public static X509Certificate2 LoadFromStore(
        string thumbprint,
        StoreLocation location = StoreLocation.LocalMachine,
        StoreName storeName = StoreName.My)
    {
        using var store = new X509Store(storeName, location);
        store.Open(OpenFlags.ReadOnly);
        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);
        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"Certificate with thumbprint '{thumbprint}' not found in {location}/{storeName}.");
        return matches[0];
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~CertificateLoaderTests
```
Expected: 3 PASS.

```bash
git add src/Weft.Auth/CertificateLoader.cs test/Weft.Auth.Tests/CertificateLoaderTests.cs test/Weft.Auth.Tests/fixtures/ test/Weft.Auth.Tests/Weft.Auth.Tests.csproj
git commit -m "feat(auth): CertificateLoader (file + Windows store)"
```

> **Note:** `LoadFromStore` cannot be unit-tested cross-platform. Coverage comes from integration tests on Windows agents.

---

### Task 6: `ServicePrincipalSecretAuth`

**Files:**
- Create: `src/Weft.Auth/ServicePrincipalSecretAuth.cs`

This is the simplest MSAL flow. No unit test for token acquisition (requires real AAD); the class is covered by integration tests in Task 31.

- [ ] **Step 1: Implement**

`src/Weft.Auth/ServicePrincipalSecretAuth.cs`:
```csharp
using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalSecretAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalSecretAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalSecret)
            throw new ArgumentException("AuthMode must be ServicePrincipalSecret.", nameof(options));

        _app = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithClientSecret(options.ClientSecret!)
            .WithTenantId(options.TenantId)
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenForClient(PowerBiScopes)
            .ExecuteAsync(cancellationToken);
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Auth/ServicePrincipalSecretAuth.cs
git commit -m "feat(auth): ServicePrincipalSecretAuth (MSAL client-credentials)"
```

---

### Task 7: `ServicePrincipalCertFileAuth` and `ServicePrincipalCertStoreAuth`

**Files:**
- Create: `src/Weft.Auth/ServicePrincipalCertFileAuth.cs`
- Create: `src/Weft.Auth/ServicePrincipalCertStoreAuth.cs`

- [ ] **Step 1: Cert file auth**

`src/Weft.Auth/ServicePrincipalCertFileAuth.cs`:
```csharp
using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalCertFileAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalCertFileAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalCertFile)
            throw new ArgumentException("AuthMode must be ServicePrincipalCertFile.", nameof(options));

        var cert = CertificateLoader.LoadFromFile(options.CertPath!, options.CertPassword!);
        _app = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithCertificate(cert, sendX5C: true)
            .WithTenantId(options.TenantId)
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenForClient(PowerBiScopes)
            .ExecuteAsync(cancellationToken);
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
```

- [ ] **Step 2: Cert store auth**

`src/Weft.Auth/ServicePrincipalCertStoreAuth.cs`:
```csharp
using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalCertStoreAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalCertStoreAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalCertStore)
            throw new ArgumentException("AuthMode must be ServicePrincipalCertStore.", nameof(options));

        var cert = CertificateLoader.LoadFromStore(
            options.CertThumbprint!,
            options.CertStoreLocation,
            options.CertStoreName);

        _app = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithCertificate(cert, sendX5C: true)
            .WithTenantId(options.TenantId)
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenForClient(PowerBiScopes)
            .ExecuteAsync(cancellationToken);
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Auth/ServicePrincipalCertFileAuth.cs src/Weft.Auth/ServicePrincipalCertStoreAuth.cs
git commit -m "feat(auth): ServicePrincipalCertFileAuth + ServicePrincipalCertStoreAuth"
```

---

### Task 8: `InteractiveAuth` and `DeviceCodeAuth`

**Files:**
- Create: `src/Weft.Auth/InteractiveAuth.cs`
- Create: `src/Weft.Auth/DeviceCodeAuth.cs`

- [ ] **Step 1: InteractiveAuth**

`src/Weft.Auth/InteractiveAuth.cs`:
```csharp
using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class InteractiveAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IPublicClientApplication _app;

    public InteractiveAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.Interactive)
            throw new ArgumentException("AuthMode must be Interactive.", nameof(options));

        _app = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithTenantId(options.TenantId)
            .WithRedirectUri(options.RedirectUri ?? "http://localhost")
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var account = (await _app.GetAccountsAsync()).FirstOrDefault();
        AuthenticationResult result;
        try
        {
            result = await _app.AcquireTokenSilent(PowerBiScopes, account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            result = await _app.AcquireTokenInteractive(PowerBiScopes)
                .ExecuteAsync(cancellationToken);
        }
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
```

- [ ] **Step 2: DeviceCodeAuth**

`src/Weft.Auth/DeviceCodeAuth.cs`:
```csharp
using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class DeviceCodeAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IPublicClientApplication _app;
    private readonly TextWriter _instructionsOut;

    public DeviceCodeAuth(AuthOptions options, TextWriter? instructionsOut = null)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.DeviceCode)
            throw new ArgumentException("AuthMode must be DeviceCode.", nameof(options));

        _app = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithTenantId(options.TenantId)
            .Build();
        _instructionsOut = instructionsOut ?? Console.Out;
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenWithDeviceCode(PowerBiScopes, callback =>
        {
            _instructionsOut.WriteLine(callback.Message);
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Auth/InteractiveAuth.cs src/Weft.Auth/DeviceCodeAuth.cs
git commit -m "feat(auth): InteractiveAuth + DeviceCodeAuth (MSAL public-client)"
```

---

### Task 9: `AuthProviderFactory`

**Files:**
- Create: `src/Weft.Auth/AuthProviderFactory.cs`
- Create: `test/Weft.Auth.Tests/AuthProviderFactoryTests.cs`

- [ ] **Step 1: Failing tests**

`test/Weft.Auth.Tests/AuthProviderFactoryTests.cs`:
```csharp
using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthProviderFactoryTests
{
    [Fact]
    public void Returns_secret_provider_for_secret_mode()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid", ClientSecret: "s");
        AuthProviderFactory.Create(opts).Should().BeOfType<ServicePrincipalSecretAuth>();
    }

    [Fact]
    public void Returns_interactive_provider_for_interactive_mode()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "tid", "cid");
        AuthProviderFactory.Create(opts).Should().BeOfType<InteractiveAuth>();
    }

    [Fact]
    public void Returns_device_code_provider_for_device_mode()
    {
        var opts = new AuthOptions(AuthMode.DeviceCode, "tid", "cid");
        AuthProviderFactory.Create(opts).Should().BeOfType<DeviceCodeAuth>();
    }

    [Fact]
    public void Validates_options_before_constructing_provider()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid"); // missing secret
        var act = () => AuthProviderFactory.Create(opts);
        act.Should().Throw<AuthOptionsValidationException>();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~AuthProviderFactoryTests
```
Expected: FAIL.

- [ ] **Step 2: Implement factory**

`src/Weft.Auth/AuthProviderFactory.cs`:
```csharp
using Weft.Core.Abstractions;

namespace Weft.Auth;

public static class AuthProviderFactory
{
    public static IAuthProvider Create(AuthOptions options) => options.Mode switch
    {
        AuthMode.ServicePrincipalSecret    => new ServicePrincipalSecretAuth(options),
        AuthMode.ServicePrincipalCertFile  => new ServicePrincipalCertFileAuth(options),
        AuthMode.ServicePrincipalCertStore => new ServicePrincipalCertStoreAuth(options),
        AuthMode.Interactive               => new InteractiveAuth(options),
        AuthMode.DeviceCode                => new DeviceCodeAuth(options),
        _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown AuthMode.")
    };
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~AuthProviderFactoryTests
```
Expected: 4 PASS.

```bash
git add src/Weft.Auth/AuthProviderFactory.cs test/Weft.Auth.Tests/AuthProviderFactoryTests.cs
git commit -m "feat(auth): AuthProviderFactory selects provider by AuthMode"
```

---

### Task 10: Create `Weft.Xmla` project

**Files:**
- Create: `src/Weft.Xmla/Weft.Xmla.csproj`

- [ ] **Step 1: New library**

```bash
mkdir -p src/Weft.Xmla
cd src/Weft.Xmla
dotnet new classlib -o . --force
rm -f Class1.cs
```

- [ ] **Step 2: Edit csproj**

`src/Weft.Xmla/Weft.Xmla.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Weft.Core\Weft.Core.csproj" />
    <!-- TOM is referenced transitively via Weft.Core -->
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add to solution + build + commit**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add src/Weft.Xmla/Weft.Xmla.csproj
dotnet build
git add src/Weft.Xmla/ weft.sln
git commit -m "feat(xmla): add Weft.Xmla class library (TOM Server wrappers)"
```

---

### Task 11: `XmlaConnectionStringBuilder`

**Files:**
- Create: `src/Weft.Xmla/XmlaConnectionStringBuilder.cs`
- Create: `test/Weft.Xmla.Tests/Weft.Xmla.Tests.csproj`
- Create: `test/Weft.Xmla.Tests/XmlaConnectionStringBuilderTests.cs`

- [ ] **Step 1: Test project**

```bash
mkdir -p test/Weft.Xmla.Tests
cd test/Weft.Xmla.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

`test/Weft.Xmla.Tests/Weft.Xmla.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <Using Include="Xunit" />
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Weft.Xmla\Weft.Xmla.csproj" />
  </ItemGroup>
</Project>
```

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add test/Weft.Xmla.Tests/Weft.Xmla.Tests.csproj
```

- [ ] **Step 2: Failing test**

`test/Weft.Xmla.Tests/XmlaConnectionStringBuilderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class XmlaConnectionStringBuilderTests
{
    [Fact]
    public void Builds_connection_string_for_powerbi_premium_workspace()
    {
        var conn = new XmlaConnectionStringBuilder()
            .Build("powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev", "SalesModel");

        conn.Should().Contain("Provider=MSOLAP");
        conn.Should().Contain("Data Source=powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev");
        conn.Should().Contain("Initial Catalog=SalesModel");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~XmlaConnectionStringBuilderTests
```
Expected: FAIL.

- [ ] **Step 3: Implement**

`src/Weft.Xmla/XmlaConnectionStringBuilder.cs`:
```csharp
namespace Weft.Xmla;

public sealed class XmlaConnectionStringBuilder
{
    public string Build(string workspaceUrl, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(workspaceUrl))
            throw new ArgumentException("workspaceUrl is required.", nameof(workspaceUrl));
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("databaseName is required.", nameof(databaseName));

        return $"Provider=MSOLAP;Data Source={workspaceUrl};Initial Catalog={databaseName};";
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~XmlaConnectionStringBuilderTests
```
Expected: PASS.

```bash
git add src/Weft.Xmla/XmlaConnectionStringBuilder.cs test/Weft.Xmla.Tests/ weft.sln
git commit -m "feat(xmla): XmlaConnectionStringBuilder for Power BI Premium workspaces"
```

---

### Task 12: `ServerConnectionFactory` (TOM Server with token)

**Files:**
- Create: `src/Weft.Xmla/ServerConnectionFactory.cs`

> **Note:** This wraps TOM `Server` instantiation + token attachment. No unit test — covered by integration tests. The class exists so `TargetReader` and `XmlaExecutor` share a single connection-construction path.

- [ ] **Step 1: Implement**

`src/Weft.Xmla/ServerConnectionFactory.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class ServerConnectionFactory
{
    public Server Connect(string workspaceUrl, string databaseName, AccessToken token)
    {
        var server = new Server();
        server.AccessToken = new Microsoft.AnalysisServices.AccessToken(
            token.Value,
            token.ExpiresOnUtc.UtcDateTime);
        var conn = new XmlaConnectionStringBuilder().Build(workspaceUrl, databaseName);
        server.Connect(conn);
        return server;
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Xmla/ServerConnectionFactory.cs
git commit -m "feat(xmla): ServerConnectionFactory attaches AAD token to TOM Server"
```

---

### Task 13: `TargetReader`

**Files:**
- Create: `src/Weft.Xmla/TargetReader.cs`

> Integration-tested only (real XMLA endpoint). The implementation is a thin wrapper.

- [ ] **Step 1: Implement**

`src/Weft.Xmla/TargetReader.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class TargetReader : ITargetReader
{
    public Task<Database> ReadAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        CancellationToken cancellationToken = default)
    {
        using var server = new ServerConnectionFactory().Connect(workspaceUrl, databaseName, token);
        var sourceDb = server.Databases.FindByName(databaseName)
            ?? throw new InvalidOperationException(
                $"Database '{databaseName}' not found on {workspaceUrl}.");

        // Deep-copy so the returned Database survives `server` disposal.
        var serialized = JsonSerializer.SerializeDatabase(sourceDb, new SerializeOptions
        {
            IgnoreTimestamps = true,
            IgnoreInferredObjects = true,
            IgnoreInferredProperties = true
        });
        var detached = JsonSerializer.DeserializeDatabase(serialized);
        return Task.FromResult(detached);
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Xmla/TargetReader.cs
git commit -m "feat(xmla): TargetReader reads Database via XMLA + AAD token"
```

---

### Task 14: `XmlaExecutor`

**Files:**
- Create: `src/Weft.Xmla/XmlaExecutor.cs`

> Integration-tested only.

- [ ] **Step 1: Implement**

`src/Weft.Xmla/XmlaExecutor.cs`:
```csharp
using System.Diagnostics;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class XmlaExecutor : IXmlaExecutor
{
    public Task<XmlaExecutionResult> ExecuteAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        string tmslJson,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var messages = new List<string>();

        using var server = new ServerConnectionFactory().Connect(workspaceUrl, databaseName, token);
        XmlaResultCollection results;
        try
        {
            results = server.Execute(tmslJson);
        }
        catch (Exception ex)
        {
            messages.Add($"XMLA execution failed: {ex.Message}");
            return Task.FromResult(new XmlaExecutionResult(false, messages, sw.Elapsed));
        }

        var success = true;
        foreach (XmlaResult r in results)
        {
            foreach (XmlaMessage m in r.Messages)
            {
                messages.Add(m.Description);
                if (m is XmlaError) success = false;
            }
        }

        return Task.FromResult(new XmlaExecutionResult(success, messages, sw.Elapsed));
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Xmla/XmlaExecutor.cs
git commit -m "feat(xmla): XmlaExecutor sends TMSL via TOM Server.Execute"
```

---

### Task 15: `RefreshTypeSelector` (per-table refresh-type matrix)

**Files:**
- Create: `src/Weft.Xmla/RefreshTypeSelector.cs`
- Create: `test/Weft.Xmla.Tests/RefreshTypeSelectorTests.cs`

This implements §6 step 13 of the spec.

- [ ] **Step 1: Failing tests**

`test/Weft.Xmla.Tests/RefreshTypeSelectorTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class RefreshTypeSelectorTests
{
    private static Table NewTable(string name) => new() { Name = name };

    private static TablePlan AddPlan(string name, TableClassification c) =>
        new(name, c, NewTable(name));

    private static TableDiff AlterDiff(string name, TableClassification c, bool policyChanged = false) =>
        new(name, c, policyChanged,
            ColumnsAdded: Array.Empty<string>(),
            ColumnsRemoved: Array.Empty<string>(),
            ColumnsModified: Array.Empty<string>(),
            MeasuresAdded: Array.Empty<string>(),
            MeasuresRemoved: Array.Empty<string>(),
            MeasuresModified: Array.Empty<string>(),
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: NewTable(name),
            TargetTable: NewTable(name));

    [Fact]
    public void Static_added_table_uses_Full_refresh_no_policy_application()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AddPlan("T", TableClassification.Static));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Full);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }

    [Fact]
    public void Incremental_added_table_uses_Policy_with_apply_true()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AddPlan("T", TableClassification.IncrementalRefreshPolicy));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeTrue();
    }

    [Fact]
    public void Incremental_altered_with_policy_change_uses_Policy_with_apply_true()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.IncrementalRefreshPolicy, policyChanged: true));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeTrue();
    }

    [Fact]
    public void Incremental_altered_with_only_schema_change_uses_Policy_with_apply_false()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.IncrementalRefreshPolicy, policyChanged: false));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }

    [Fact]
    public void Dynamic_altered_uses_Full_no_policy()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.DynamicallyPartitioned));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Full);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~RefreshTypeSelectorTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Xmla/RefreshTypeSelector.cs`:
```csharp
using Weft.Core.Diffing;

namespace Weft.Xmla;

public sealed record RefreshTypeSpec(
    RefreshTypeSpec.RefreshKind RefreshType,
    bool ApplyRefreshPolicy)
{
    public enum RefreshKind { Full, Policy, DataOnly, Calculate, Automatic }
}

public sealed class RefreshTypeSelector
{
    public RefreshTypeSpec For(TablePlan add) => add.Classification switch
    {
        TableClassification.IncrementalRefreshPolicy =>
            new(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true),
        TableClassification.DynamicallyPartitioned =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        TableClassification.Static =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        _ => throw new ArgumentOutOfRangeException(nameof(add), add.Classification, "Unknown classification.")
    };

    public RefreshTypeSpec For(TableDiff alter) => alter.Classification switch
    {
        TableClassification.IncrementalRefreshPolicy =>
            new(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: alter.RefreshPolicyChanged),
        TableClassification.DynamicallyPartitioned =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        TableClassification.Static =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        _ => throw new ArgumentOutOfRangeException(nameof(alter), alter.Classification, "Unknown classification.")
    };
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RefreshTypeSelectorTests
```
Expected: 5 PASS.

```bash
git add src/Weft.Xmla/RefreshTypeSelector.cs test/Weft.Xmla.Tests/RefreshTypeSelectorTests.cs
git commit -m "feat(xmla): RefreshTypeSelector implements per-table refresh-type matrix (§6 step 13)"
```

---

### Task 16: `RefreshCommandBuilder` (TMSL refresh JSON)

**Files:**
- Create: `src/Weft.Xmla/RefreshCommandBuilder.cs`
- Create: `test/Weft.Xmla.Tests/RefreshCommandBuilderTests.cs`

- [ ] **Step 1: Failing tests**

`test/Weft.Xmla.Tests/RefreshCommandBuilderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class RefreshCommandBuilderTests
{
    [Fact]
    public void Builds_full_refresh_for_one_table()
    {
        var json = new RefreshCommandBuilder().Build(
            databaseName: "TinyStatic",
            entries: new[]
            {
                new RefreshTableEntry("FactSales",
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false))
            },
            effectiveDateUtc: null);

        json.Should().Contain("\"refresh\"");
        json.Should().Contain("\"type\": \"full\"");
        json.Should().Contain("\"FactSales\"");
        json.Should().NotContain("applyRefreshPolicy");
    }

    [Fact]
    public void Builds_policy_refresh_with_apply_true_and_effective_date()
    {
        var json = new RefreshCommandBuilder().Build(
            databaseName: "TinyStatic",
            entries: new[]
            {
                new RefreshTableEntry("FactSales",
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true))
            },
            effectiveDateUtc: "2026-04-17");

        json.Should().Contain("\"type\": \"full\"");        // TMSL maps Policy onto type=full + applyRefreshPolicy=true
        json.Should().Contain("\"applyRefreshPolicy\": true");
        json.Should().Contain("\"effectiveDate\": \"2026-04-17\"");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~RefreshCommandBuilderTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Xmla/RefreshCommandBuilder.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weft.Xmla;

public sealed record RefreshTableEntry(string TableName, RefreshTypeSpec Spec);

public sealed class RefreshCommandBuilder
{
    public string Build(string databaseName, IEnumerable<RefreshTableEntry> entries, string? effectiveDateUtc)
    {
        var operations = new JsonArray();
        foreach (var e in entries)
        {
            var refresh = new JsonObject
            {
                ["type"] = "full",
                ["objects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["database"] = databaseName,
                        ["table"]    = e.TableName
                    }
                }
            };
            if (e.Spec.ApplyRefreshPolicy)
            {
                refresh["applyRefreshPolicy"] = true;
                if (effectiveDateUtc is not null)
                    refresh["effectiveDate"] = effectiveDateUtc;
            }
            operations.Add(new JsonObject { ["refresh"] = refresh });
        }

        var root = new JsonObject
        {
            ["sequence"] = new JsonObject
            {
                ["maxParallelism"] = 1,
                ["operations"] = operations
            }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RefreshCommandBuilderTests
```
Expected: 2 PASS.

```bash
git add src/Weft.Xmla/RefreshCommandBuilder.cs test/Weft.Xmla.Tests/RefreshCommandBuilderTests.cs
git commit -m "feat(xmla): RefreshCommandBuilder emits TMSL refresh sequences"
```

---

### Task 17: `RefreshRunner`

**Files:**
- Create: `src/Weft.Xmla/RefreshRunner.cs`

Wires `RefreshTypeSelector` + `RefreshCommandBuilder` + `XmlaExecutor`. Integration-tested only.

- [ ] **Step 1: Implement**

`src/Weft.Xmla/RefreshRunner.cs`:
```csharp
using Weft.Core.Abstractions;
using Weft.Core.Diffing;

namespace Weft.Xmla;

public sealed class RefreshRunner : IRefreshRunner
{
    private readonly IXmlaExecutor _executor;
    private readonly RefreshTypeSelector _selector = new();
    private readonly RefreshCommandBuilder _builder = new();

    public RefreshRunner(IXmlaExecutor executor)
    {
        _executor = executor;
    }

    public async Task<XmlaExecutionResult> RefreshAsync(
        RefreshRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<RefreshTableEntry>();
        foreach (var add in request.ChangeSet.TablesToAdd)
            entries.Add(new RefreshTableEntry(add.Name, _selector.For(add)));
        foreach (var alt in request.ChangeSet.TablesToAlter)
            entries.Add(new RefreshTableEntry(alt.Name, _selector.For(alt)));

        if (entries.Count == 0)
        {
            progress?.Report("No tables to refresh; nothing changed.");
            return new XmlaExecutionResult(true, new[] { "No refresh required." }, TimeSpan.Zero);
        }

        progress?.Report($"Refreshing {entries.Count} table(s): {string.Join(", ", entries.Select(e => e.TableName))}");
        var tmsl = _builder.Build(request.DatabaseName, entries, request.EffectiveDateUtc);
        var result = await _executor.ExecuteAsync(
            request.WorkspaceUrl, request.DatabaseName, request.Token, tmsl, cancellationToken);

        foreach (var msg in result.Messages)
            progress?.Report(msg);
        return result;
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Xmla/RefreshRunner.cs
git commit -m "feat(xmla): RefreshRunner derives refresh entries and submits via XmlaExecutor"
```

---

### Task 18: `FilePartitionManifestStore`

**Files:**
- Create: `src/Weft.Xmla/FilePartitionManifestStore.cs`
- Create: `test/Weft.Xmla.Tests/FilePartitionManifestStoreTests.cs`

- [ ] **Step 1: Failing test**

`test/Weft.Xmla.Tests/FilePartitionManifestStoreTests.cs`:
```csharp
using FluentAssertions;
using Weft.Core.Partitions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class FilePartitionManifestStoreTests
{
    [Fact]
    public void Writes_manifest_to_artifacts_dir_and_returns_path()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var manifest = new PartitionManifest(
                CapturedAtUtc: DateTimeOffset.UtcNow,
                TargetDatabase: "TinyStatic",
                Tables: new Dictionary<string, IReadOnlyList<PartitionRecord>>
                {
                    ["FactSales"] = new[] { new PartitionRecord("FactSales", "wm-001", null, null) }
                });

            var store = new FilePartitionManifestStore();
            var path = store.Write(manifest, dir, "pre-partitions");

            File.Exists(path).Should().BeTrue();
            Path.GetFileName(path).Should().Contain("pre-partitions");
            Path.GetFileName(path).Should().EndWith(".json");

            var reread = store.Read(path);
            reread.TargetDatabase.Should().Be("TinyStatic");
            reread.Tables["FactSales"][0].RefreshBookmark.Should().Be("wm-001");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~FilePartitionManifestStoreTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Xmla/FilePartitionManifestStore.cs`:
```csharp
using System.Text.Json;
using Weft.Core.Abstractions;
using Weft.Core.Partitions;

namespace Weft.Xmla;

public sealed class FilePartitionManifestStore : IPartitionManifestStore
{
    private readonly PartitionManifestWriter _writer = new();

    public string Write(PartitionManifest manifest, string artifactsDirectory, string label)
    {
        Directory.CreateDirectory(artifactsDirectory);
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safeLabel = string.Concat(label.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var fileName = $"{ts}-{manifest.TargetDatabase}-{safeLabel}.json";
        var path = Path.Combine(artifactsDirectory, fileName);
        _writer.Write(manifest, path);
        return path;
    }

    public PartitionManifest Read(string path)
        => JsonSerializer.Deserialize<PartitionManifest>(
            File.ReadAllText(path),
            PartitionManifestWriter.JsonOptions)!;
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~FilePartitionManifestStoreTests
```
Expected: PASS.

```bash
git add src/Weft.Xmla/FilePartitionManifestStore.cs test/Weft.Xmla.Tests/FilePartitionManifestStoreTests.cs
git commit -m "feat(xmla): FilePartitionManifestStore writes pre/post deploy manifests"
```

---

### Task 19: Create `Weft.Cli` project + `ExitCodes`

**Files:**
- Create: `src/Weft.Cli/Weft.Cli.csproj`
- Create: `src/Weft.Cli/ExitCodes.cs`
- Create: `src/Weft.Cli/Program.cs`
- Create: `test/Weft.Cli.Tests/Weft.Cli.Tests.csproj`
- Create: `test/Weft.Cli.Tests/ExitCodesTests.cs`

- [ ] **Step 1: New console project**

```bash
mkdir -p src/Weft.Cli
cd src/Weft.Cli
dotnet new console -o . --force
rm -f Program.cs   # we replace it below
```

`src/Weft.Cli/Weft.Cli.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AssemblyName>weft</AssemblyName>
    <RootNamespace>Weft.Cli</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta5.25306.1" />
    <PackageReference Include="Spectre.Console" Version="0.49.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Weft.Core\Weft.Core.csproj" />
    <ProjectReference Include="..\Weft.Auth\Weft.Auth.csproj" />
    <ProjectReference Include="..\Weft.Xmla\Weft.Xmla.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: ExitCodes**

`src/Weft.Cli/ExitCodes.cs`:
```csharp
namespace Weft.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int Generic = 1;
    public const int ConfigError = 2;
    public const int AuthError = 3;
    public const int SourceLoadError = 4;
    public const int TargetReadError = 5;
    public const int DiffValidationError = 6;
    public const int TmslExecutionError = 7;
    public const int RefreshError = 8;
    public const int PartitionIntegrityError = 9;
}
```

- [ ] **Step 3: Minimal Program.cs**

`src/Weft.Cli/Program.cs`:
```csharp
using System.CommandLine;
using Weft.Cli.Commands;

namespace Weft.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = BuildRoot();
        return root.InvokeAsync(args);
    }

    public static RootCommand BuildRoot()
    {
        var root = new RootCommand("Weft — diff-based Power BI semantic-model deploys.");
        // Commands wired in subsequent tasks.
        return root;
    }
}
```

- [ ] **Step 4: Test project + ExitCodes test**

```bash
mkdir -p test/Weft.Cli.Tests
cd test/Weft.Cli.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

`test/Weft.Cli.Tests/Weft.Cli.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <Using Include="Xunit" />
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Weft.Cli\Weft.Cli.csproj" />
    <ProjectReference Include="..\..\test\Weft.Core.Tests\Weft.Core.Tests.csproj" />
  </ItemGroup>
</Project>
```

`test/Weft.Cli.Tests/ExitCodesTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli;

namespace Weft.Cli.Tests;

public class ExitCodesTests
{
    [Fact]
    public void Codes_match_spec()
    {
        ExitCodes.Success.Should().Be(0);
        ExitCodes.ConfigError.Should().Be(2);
        ExitCodes.AuthError.Should().Be(3);
        ExitCodes.SourceLoadError.Should().Be(4);
        ExitCodes.TargetReadError.Should().Be(5);
        ExitCodes.DiffValidationError.Should().Be(6);
        ExitCodes.TmslExecutionError.Should().Be(7);
        ExitCodes.RefreshError.Should().Be(8);
        ExitCodes.PartitionIntegrityError.Should().Be(9);
    }
}
```

- [ ] **Step 5: Build + sln + test + commit**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add src/Weft.Cli/Weft.Cli.csproj test/Weft.Cli.Tests/Weft.Cli.Tests.csproj
dotnet build
dotnet test --filter FullyQualifiedName~ExitCodesTests
```
Expected: PASS.

```bash
git add src/Weft.Cli/ test/Weft.Cli.Tests/ weft.sln
git commit -m "feat(cli): scaffold Weft.Cli (System.CommandLine + Spectre.Console) + ExitCodes"
```

---

### Task 20: `IConsoleWriter` + Human/Json formatters

**Files:**
- Create: `src/Weft.Cli/Output/IConsoleWriter.cs`
- Create: `src/Weft.Cli/Output/HumanConsoleWriter.cs`
- Create: `src/Weft.Cli/Output/JsonConsoleWriter.cs`

- [ ] **Step 1: Interface**

`src/Weft.Cli/Output/IConsoleWriter.cs`:
```csharp
namespace Weft.Cli.Output;

public interface IConsoleWriter
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Plan(string headline, IEnumerable<string> bullets);
}
```

- [ ] **Step 2: Human writer**

`src/Weft.Cli/Output/HumanConsoleWriter.cs`:
```csharp
using Spectre.Console;

namespace Weft.Cli.Output;

public sealed class HumanConsoleWriter : IConsoleWriter
{
    public void Info(string message)  => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    public void Warn(string message)  => AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {Markup.Escape(message)}");
    public void Error(string message) => AnsiConsole.MarkupLine($"[red]ERROR:[/] {Markup.Escape(message)}");

    public void Plan(string headline, IEnumerable<string> bullets)
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(headline)}[/]");
        foreach (var b in bullets) AnsiConsole.MarkupLine($"  {Markup.Escape(b)}");
    }
}
```

- [ ] **Step 3: JSON writer**

`src/Weft.Cli/Output/JsonConsoleWriter.cs`:
```csharp
using System.Text.Json;

namespace Weft.Cli.Output;

public sealed class JsonConsoleWriter : IConsoleWriter
{
    public void Info(string message)  => Emit("info", message);
    public void Warn(string message)  => Emit("warn", message);
    public void Error(string message) => Emit("error", message);

    public void Plan(string headline, IEnumerable<string> bullets)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("o"),
            level = "plan",
            headline,
            items = bullets.ToArray()
        }));
    }

    private static void Emit(string level, string message) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("o"),
            level,
            message
        }));
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build
git add src/Weft.Cli/Output/
git commit -m "feat(cli): IConsoleWriter with Human (Spectre) and JSON formatters"
```

---

### Task 21: `CommonOptions` + `ProfileResolver` (CLI-flag-only for now)

**Files:**
- Create: `src/Weft.Cli/Options/CommonOptions.cs`
- Create: `src/Weft.Cli/Options/ProfileResolver.cs`

> Plan 3 will replace ProfileResolver with YAML-config loading. For Plan 2, profiles come from CLI flags (workspace, database, auth-mode, etc.).

- [ ] **Step 1: CommonOptions**

`src/Weft.Cli/Options/CommonOptions.cs`:
```csharp
using System.CommandLine;
using System.Security.Cryptography.X509Certificates;
using Weft.Auth;

namespace Weft.Cli.Options;

public static class CommonOptions
{
    public static Option<string> SourceOption() =>
        new("--source", "-s") { Description = "Path to source .bim or TE folder.", Required = true };

    public static Option<string> WorkspaceOption() =>
        new("--workspace", "-w") { Description = "XMLA workspace URL (powerbi://...)", Required = true };

    public static Option<string> DatabaseOption() =>
        new("--database", "-d") { Description = "Target dataset/database name.", Required = true };

    public static Option<string> ArtifactsOption() =>
        new("--artifacts") { Description = "Directory for plan/manifest/receipt JSON.", DefaultValueFactory = _ => "./artifacts" };

    public static Option<string> LogFormatOption() =>
        new("--log-format") { Description = "human | json", DefaultValueFactory = _ => "human" };

    public static Option<bool> AllowDropsOption() =>
        new("--allow-drops") { Description = "Permit dropping tables that exist on target but not source." };

    public static Option<bool> NoRefreshOption() =>
        new("--no-refresh") { Description = "Skip refresh after deploy." };

    public static Option<bool> ResetBookmarksOption() =>
        new("--reset-bookmarks") { Description = "Clear RefreshBookmark annotations on refreshed tables before refresh." };

    public static Option<string?> EffectiveDateOption() =>
        new("--effective-date") { Description = "ISO date used as RefreshPolicy effectiveDate (UTC)." };

    // Auth options
    public static Option<AuthMode> AuthModeOption() =>
        new("--auth") { Description = "Auth mode.", DefaultValueFactory = _ => AuthMode.Interactive };
    public static Option<string?> TenantOption() =>
        new("--tenant") { Description = "AAD tenant id (or env: WEFT_TENANT_ID)." };
    public static Option<string?> ClientOption() =>
        new("--client") { Description = "AAD client id (or env: WEFT_CLIENT_ID)." };
    public static Option<string?> ClientSecretOption() =>
        new("--client-secret") { Description = "(secret mode) Client secret (or env: WEFT_CLIENT_SECRET)." };
    public static Option<string?> CertPathOption() =>
        new("--cert-path") { Description = "(cert-file mode) Path to .pfx (or env: WEFT_CERT_PATH)." };
    public static Option<string?> CertPasswordOption() =>
        new("--cert-password") { Description = "(cert-file mode) .pfx password (or env: WEFT_CERT_PASSWORD)." };
    public static Option<string?> CertThumbprintOption() =>
        new("--cert-thumbprint") { Description = "(cert-store mode) Cert thumbprint (or env: WEFT_CERT_THUMBPRINT)." };
}
```

- [ ] **Step 2: ProfileResolver**

`src/Weft.Cli/Options/ProfileResolver.cs`:
```csharp
using Weft.Auth;

namespace Weft.Cli.Options;

public sealed record ResolvedProfile(
    string WorkspaceUrl,
    string DatabaseName,
    string SourcePath,
    string ArtifactsDirectory,
    AuthOptions Auth,
    bool AllowDrops,
    bool NoRefresh,
    bool ResetBookmarks,
    string? EffectiveDate);

public static class ProfileResolver
{
    public static AuthOptions BuildAuthOptions(
        AuthMode mode,
        string? tenant, string? client,
        string? clientSecret,
        string? certPath, string? certPassword,
        string? certThumbprint)
    {
        var t = tenant ?? Environment.GetEnvironmentVariable("WEFT_TENANT_ID")
            ?? throw new InvalidOperationException("--tenant is required (or env WEFT_TENANT_ID).");
        var c = client ?? Environment.GetEnvironmentVariable("WEFT_CLIENT_ID")
            ?? throw new InvalidOperationException("--client is required (or env WEFT_CLIENT_ID).");

        return new AuthOptions(
            Mode: mode,
            TenantId: t,
            ClientId: c,
            ClientSecret: clientSecret ?? Environment.GetEnvironmentVariable("WEFT_CLIENT_SECRET"),
            CertPath: certPath ?? Environment.GetEnvironmentVariable("WEFT_CERT_PATH"),
            CertPassword: certPassword ?? Environment.GetEnvironmentVariable("WEFT_CERT_PASSWORD"),
            CertThumbprint: certThumbprint ?? Environment.GetEnvironmentVariable("WEFT_CERT_THUMBPRINT"));
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Cli/Options/
git commit -m "feat(cli): CommonOptions and ProfileResolver (CLI-flag-driven; Plan 3 adds YAML)"
```

---

### Task 22: `ValidateCommand`

**Files:**
- Create: `src/Weft.Cli/Commands/ValidateCommand.cs`
- Create: `test/Weft.Cli.Tests/ValidateCommandTests.cs`

`weft validate --source <path>` — parses the source model, reports any structural issues, exits 0 on success.

- [ ] **Step 1: Failing test**

`test/Weft.Cli.Tests/ValidateCommandTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class ValidateCommandTests
{
    [Fact]
    public async Task Returns_zero_on_valid_bim()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var exit = await ValidateCommand.RunAsync(fixture);
        exit.Should().Be(0);
    }

    [Fact]
    public async Task Returns_SourceLoadError_on_missing_file()
    {
        var exit = await ValidateCommand.RunAsync("/no/such/path.bim");
        exit.Should().Be(ExitCodes.SourceLoadError);
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~ValidateCommandTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Cli/Commands/ValidateCommand.cs`:
```csharp
using System.CommandLine;
using Weft.Cli.Options;
using Weft.Core.Loading;

namespace Weft.Cli.Commands;

public static class ValidateCommand
{
    public static Command Build()
    {
        var src = CommonOptions.SourceOption();
        var cmd = new Command("validate", "Parse and validate a source model.");
        cmd.Options.Add(src);
        cmd.SetAction(async (parse, ct) =>
        {
            var source = parse.GetValue(src)!;
            return await RunAsync(source);
        });
        return cmd;
    }

    public static Task<int> RunAsync(string sourcePath)
    {
        try
        {
            var loader = ModelLoaderFactory.For(sourcePath);
            var db = loader.Load(sourcePath);
            Console.Out.WriteLine($"OK: '{db.Name}' loaded with {db.Model.Tables.Count} table(s).");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Source not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Source load failed: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
    }
}
```

- [ ] **Step 3: Wire into Program.cs**

Replace `src/Weft.Cli/Program.cs`:
```csharp
using System.CommandLine;
using Weft.Cli.Commands;

namespace Weft.Cli;

public static class Program
{
    public static Task<int> Main(string[] args) => BuildRoot().InvokeAsync(args);

    public static RootCommand BuildRoot()
    {
        var root = new RootCommand("Weft — diff-based Power BI semantic-model deploys.");
        root.Subcommands.Add(ValidateCommand.Build());
        return root;
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ValidateCommandTests
```
Expected: 2 PASS.

```bash
git add src/Weft.Cli/Commands/ValidateCommand.cs src/Weft.Cli/Program.cs test/Weft.Cli.Tests/ValidateCommandTests.cs
git commit -m "feat(cli): validate command (loads .bim/folder, exit 0 on success)"
```

---

### Task 23: `PlanCommand` (in-memory plan against a target snapshot file)

**Files:**
- Create: `src/Weft.Cli/Commands/PlanCommand.cs`
- Create: `test/Weft.Cli.Tests/PlanCommandTests.cs`

`weft plan` is a dry-run that writes a TMSL plan artifact and prints a human-readable summary. For Plan 2, the target can be supplied as a `.bim` snapshot file (so the command is fully unit-testable). For real XMLA targets, callers can capture target via `weft inspect --snapshot` first.

- [ ] **Step 1: Failing test**

`test/Weft.Cli.Tests/PlanCommandTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class PlanCommandTests
{
    [Fact]
    public async Task Writes_plan_artifact_and_returns_zero()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");
        var artifactsDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var exit = await PlanCommand.RunAsync(
                source: fixture,
                targetSnapshot: fixture,        // self-compare → empty plan
                artifactsDirectory: artifactsDir);

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifactsDir, "*-plan.tmsl").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifactsDir, recursive: true); }
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~PlanCommandTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Cli/Commands/PlanCommand.cs`:
```csharp
using System.CommandLine;
using Microsoft.AnalysisServices.Tabular;
using Weft.Cli.Options;
using Weft.Core;
using Weft.Core.Loading;

namespace Weft.Cli.Commands;

public static class PlanCommand
{
    public static Command Build()
    {
        var src = CommonOptions.SourceOption();
        var tgt = new Option<string>("--target-snapshot")
            { Description = "Path to a .bim snapshot of the target (offline plan).", Required = true };
        var artifacts = CommonOptions.ArtifactsOption();

        var cmd = new Command("plan", "Compute and print a deploy plan; write TMSL to artifacts.");
        cmd.Options.Add(src); cmd.Options.Add(tgt); cmd.Options.Add(artifacts);
        cmd.SetAction(async (parse, ct) =>
            await RunAsync(parse.GetValue(src)!, parse.GetValue(tgt)!, parse.GetValue(artifacts)!));
        return cmd;
    }

    public static Task<int> RunAsync(string source, string targetSnapshot, string artifactsDirectory)
    {
        try
        {
            var srcDb = ModelLoaderFactory.For(source).Load(source);
            var tgtDb = ModelLoaderFactory.For(targetSnapshot).Load(targetSnapshot);
            var result = WeftCore.Plan(srcDb, tgtDb);

            Directory.CreateDirectory(artifactsDirectory);
            var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            var planPath = Path.Combine(artifactsDirectory, $"{ts}-{tgtDb.Name}-plan.tmsl");
            File.WriteAllText(planPath, result.TmslJson);

            Console.Out.WriteLine($"Plan written to {planPath}");
            Console.Out.WriteLine($"  Add:       {result.ChangeSet.TablesToAdd.Count}");
            Console.Out.WriteLine($"  Drop:      {result.ChangeSet.TablesToDrop.Count}");
            Console.Out.WriteLine($"  Alter:     {result.ChangeSet.TablesToAlter.Count}");
            Console.Out.WriteLine($"  Unchanged: {result.ChangeSet.TablesUnchanged.Count}");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Source/target not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Plan failed: {ex.Message}");
            return Task.FromResult(ExitCodes.DiffValidationError);
        }
    }
}
```

- [ ] **Step 3: Wire into Program.cs**

Add to `BuildRoot`:
```csharp
root.Subcommands.Add(PlanCommand.Build());
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~PlanCommandTests
```
Expected: PASS.

```bash
git add src/Weft.Cli/Commands/PlanCommand.cs src/Weft.Cli/Program.cs test/Weft.Cli.Tests/PlanCommandTests.cs
git commit -m "feat(cli): plan command (offline plan against snapshot, writes TMSL artifact)"
```

---

### Task 24: `InspectCommand` — partition snapshot

**Files:**
- Create: `src/Weft.Cli/Commands/InspectCommand.cs`
- Create: `test/Weft.Cli.Tests/InspectCommandTests.cs`

`weft inspect partitions --target-snapshot <bim>` — reads partitions from a snapshot file and prints them. For real XMLA targets, the live read happens via Weft.Xmla in deploy, but inspect supports both modes.

- [ ] **Step 1: Failing test**

`test/Weft.Cli.Tests/InspectCommandTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class InspectCommandTests
{
    [Fact]
    public async Task Lists_partitions_from_a_bim_snapshot()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var sw = new StringWriter();
        Console.SetOut(sw);
        var exit = await InspectCommand.RunFromSnapshotAsync(fixture, tableFilter: null);
        var output = sw.ToString();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("FactSales");
        output.Should().Contain("DimDate");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~InspectCommandTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Cli/Commands/InspectCommand.cs`:
```csharp
using System.CommandLine;
using Weft.Cli.Options;
using Weft.Core.Loading;
using Weft.Core.Partitions;

namespace Weft.Cli.Commands;

public static class InspectCommand
{
    public static Command Build()
    {
        var snap = new Option<string>("--target-snapshot")
            { Description = "Read partitions from a .bim snapshot file.", Required = true };
        var table = new Option<string?>("--table") { Description = "Filter to one table." };

        var partitions = new Command("partitions", "List partitions and bookmarks.");
        partitions.Options.Add(snap); partitions.Options.Add(table);
        partitions.SetAction(async (parse, ct) =>
            await RunFromSnapshotAsync(parse.GetValue(snap)!, parse.GetValue(table)));

        var inspect = new Command("inspect", "Inspect target state.");
        inspect.Subcommands.Add(partitions);
        return inspect;
    }

    public static Task<int> RunFromSnapshotAsync(string snapshotPath, string? tableFilter)
    {
        try
        {
            var db = ModelLoaderFactory.For(snapshotPath).Load(snapshotPath);
            var manifest = new PartitionManifestReader().Read(db);
            foreach (var (tableName, parts) in manifest.Tables)
            {
                if (tableFilter is not null && !string.Equals(tableName, tableFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                Console.Out.WriteLine($"Table: {tableName}");
                foreach (var p in parts)
                    Console.Out.WriteLine($"  - {p.Name}    bookmark={p.RefreshBookmark ?? "<none>"}");
            }
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Snapshot not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
    }
}
```

- [ ] **Step 3: Wire + run + commit**

Add to `BuildRoot`:
```csharp
root.Subcommands.Add(InspectCommand.Build());
```

```bash
dotnet test --filter FullyQualifiedName~InspectCommandTests
```
Expected: PASS.

```bash
git add src/Weft.Cli/Commands/InspectCommand.cs src/Weft.Cli/Program.cs test/Weft.Cli.Tests/InspectCommandTests.cs
git commit -m "feat(cli): inspect partitions command (snapshot-driven; live mode in deploy)"
```

---

### Task 25: `DeployCommand` core orchestration (with mocks)

**Files:**
- Create: `src/Weft.Cli/Commands/DeployCommand.cs`
- Create: `test/Weft.Cli.Tests/Helpers/CliTestHost.cs`
- Create: `test/Weft.Cli.Tests/DeployCommandTests.cs`

The deploy command is the orchestration heart. We make it fully unit-testable by injecting `IAuthProvider`, `ITargetReader`, `IXmlaExecutor`, `IRefreshRunner`, `IPartitionManifestStore` via a static-method overload that the CLI binding wraps.

- [ ] **Step 1: Test helper**

`test/Weft.Cli.Tests/Helpers/CliTestHost.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;
using NSubstitute;
using Weft.Core.Abstractions;
using Weft.Core.Partitions;

namespace Weft.Cli.Tests.Helpers;

public static class CliTestHost
{
    public static IAuthProvider StubAuth(string token = "fake-token") =>
        Substitute.For<IAuthProvider>()
            .Returns(_ => Task.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1))));

    public static ITargetReader StubTarget(Database db)
    {
        var r = Substitute.For<ITargetReader>();
        r.ReadAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromResult(db));
        return r;
    }

    public static IXmlaExecutor StubExecutor(bool success = true) =>
        Substitute.For<IXmlaExecutor>()
            .ExecuteAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(new XmlaExecutionResult(success, Array.Empty<string>(), TimeSpan.Zero)))
            .When(s => s).Provider as IXmlaExecutor ?? Substitute.For<IXmlaExecutor>();

    // Simpler: use a factory method
    public static IXmlaExecutor MakeExecutor(bool success = true)
    {
        var ex = Substitute.For<IXmlaExecutor>();
        ex.ExecuteAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(new XmlaExecutionResult(success, Array.Empty<string>(), TimeSpan.Zero)));
        return ex;
    }

    public static IRefreshRunner MakeRefreshRunner(bool success = true)
    {
        var r = Substitute.For<IRefreshRunner>();
        r.RefreshAsync(default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult(new XmlaExecutionResult(success, Array.Empty<string>(), TimeSpan.Zero)));
        return r;
    }
}
```

(Use `MakeExecutor`/`MakeRefreshRunner`; the chained `Returns(_ =>...)` extension on `IAuthProvider` is awkward — replace with):
```csharp
public static IAuthProvider MakeAuth(string token = "fake-token")
{
    var a = Substitute.For<IAuthProvider>();
    a.GetTokenAsync(default).ReturnsForAnyArgs(
        Task.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1))));
    return a;
}
```
Replace `StubAuth` with `MakeAuth` in the helper. (Drop the awkward `StubExecutor` form too; keep just `MakeExecutor` and `MakeRefreshRunner`.)

- [ ] **Step 2: Failing test (full happy path)**

`test/Weft.Cli.Tests/DeployCommandTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli.Commands;
using Weft.Cli.Tests.Helpers;
using Weft.Core.Loading;
using Weft.Xmla;

namespace Weft.Cli.Tests;

public class DeployCommandTests
{
    private static string TinyStaticPath() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

    [Fact]
    public async Task Happy_path_returns_zero_and_writes_pre_post_manifests_and_receipt()
    {
        var src = TinyStaticPath();
        var tgtDb = new BimFileLoader().Load(src); // identical model → empty changeset
        var artifacts = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var exit = await DeployCommand.RunAsync(
                source: src,
                workspaceUrl: "powerbi://x",
                databaseName: "TinyStatic",
                artifactsDirectory: artifacts,
                allowDrops: false,
                noRefresh: false,
                resetBookmarks: false,
                effectiveDate: null,
                auth: CliTestHost.MakeAuth(),
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifacts, "*-pre-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-post-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-receipt.json").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~DeployCommandTests
```
Expected: FAIL.

- [ ] **Step 3: Implement DeployCommand orchestration**

`src/Weft.Cli/Commands/DeployCommand.cs`:
```csharp
using System.CommandLine;
using System.Text.Json;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Core;
using Weft.Core.Abstractions;
using Weft.Core.Loading;
using Weft.Core.Partitions;
using Weft.Core.Tmsl;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class DeployCommand
{
    public static Command Build()
    {
        var src       = CommonOptions.SourceOption();
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var artifacts = CommonOptions.ArtifactsOption();
        var allowDrops = CommonOptions.AllowDropsOption();
        var noRefresh  = CommonOptions.NoRefreshOption();
        var resetBookmarks = CommonOptions.ResetBookmarksOption();
        var effectiveDate  = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();

        var cmd = new Command("deploy", "Deploy a model: load → diff → execute → refresh.");
        foreach (var o in new Option[] { src, workspace, database, artifacts, allowDrops, noRefresh,
                                         resetBookmarks, effectiveDate, authMode, tenant, client,
                                         clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);

        cmd.SetAction(async (parse, ct) =>
        {
            var auth = ProfileResolver.BuildAuthOptions(
                parse.GetValue(authMode), parse.GetValue(tenant), parse.GetValue(client),
                parse.GetValue(clientSecret), parse.GetValue(certPath), parse.GetValue(certPwd),
                parse.GetValue(certThumb));
            var provider = AuthProviderFactory.Create(auth);

            return await RunAsync(
                source: parse.GetValue(src)!,
                workspaceUrl: parse.GetValue(workspace)!,
                databaseName: parse.GetValue(database)!,
                artifactsDirectory: parse.GetValue(artifacts)!,
                allowDrops: parse.GetValue(allowDrops),
                noRefresh: parse.GetValue(noRefresh),
                resetBookmarks: parse.GetValue(resetBookmarks),
                effectiveDate: parse.GetValue(effectiveDate),
                auth: provider,
                targetReader: new TargetReader(),
                executor: new XmlaExecutor(),
                refreshRunner: new RefreshRunner(new XmlaExecutor()),
                manifestStore: new FilePartitionManifestStore());
        });
        return cmd;
    }

    public static async Task<int> RunAsync(
        string source,
        string workspaceUrl,
        string databaseName,
        string artifactsDirectory,
        bool allowDrops,
        bool noRefresh,
        bool resetBookmarks,
        string? effectiveDate,
        IAuthProvider auth,
        ITargetReader targetReader,
        IXmlaExecutor executor,
        IRefreshRunner refreshRunner,
        IPartitionManifestStore manifestStore,
        CancellationToken cancellationToken = default)
    {
        // 1. Auth
        AccessToken token;
        try { token = await auth.GetTokenAsync(cancellationToken); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Auth failed: {ex.Message}");
            return ExitCodes.AuthError;
        }

        // 2. Load source
        Microsoft.AnalysisServices.Tabular.Database srcDb;
        try { srcDb = ModelLoaderFactory.For(source).Load(source); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Source load failed: {ex.Message}");
            return ExitCodes.SourceLoadError;
        }

        // 3. Read target + write pre-manifest
        Microsoft.AnalysisServices.Tabular.Database tgtDb;
        try { tgtDb = await targetReader.ReadAsync(workspaceUrl, databaseName, token, cancellationToken); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Target read failed: {ex.Message}");
            return ExitCodes.TargetReadError;
        }
        var preManifest = new PartitionManifestReader().Read(tgtDb);
        var prePath = manifestStore.Write(preManifest, artifactsDirectory, "pre-partitions");
        Console.Out.WriteLine($"Pre-deploy manifest: {prePath}");

        // 4. Plan
        PlanResult plan;
        try { plan = WeftCore.Plan(srcDb, tgtDb); }
        catch (PartitionIntegrityException ex)
        {
            Console.Error.WriteLine($"Partition integrity violation: {ex.Message}");
            return ExitCodes.PartitionIntegrityError;
        }

        // 5. Pre-flight: drops
        if (plan.ChangeSet.TablesToDrop.Count > 0 && !allowDrops)
        {
            Console.Error.WriteLine(
                $"Refusing to drop tables without --allow-drops: {string.Join(", ", plan.ChangeSet.TablesToDrop)}");
            return ExitCodes.DiffValidationError;
        }

        if (plan.ChangeSet.IsEmpty)
        {
            Console.Out.WriteLine("Nothing to deploy.");
            // still write post-manifest + receipt so the artifacts exist
        }

        // 6. Write plan TMSL
        Directory.CreateDirectory(artifactsDirectory);
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var planPath = Path.Combine(artifactsDirectory, $"{ts}-{databaseName}-plan.tmsl");
        File.WriteAllText(planPath, plan.TmslJson);

        // 7. Execute (skip if empty)
        if (!plan.ChangeSet.IsEmpty)
        {
            var exec = await executor.ExecuteAsync(workspaceUrl, databaseName, token, plan.TmslJson, cancellationToken);
            foreach (var m in exec.Messages) Console.Out.WriteLine(m);
            if (!exec.Success)
            {
                Console.Error.WriteLine("TMSL execution failed.");
                return ExitCodes.TmslExecutionError;
            }
        }

        // 8. Post-deploy manifest + integrity gate
        var postDb = await targetReader.ReadAsync(workspaceUrl, databaseName, token, cancellationToken);
        var postManifest = new PartitionManifestReader().Read(postDb);
        var postPath = manifestStore.Write(postManifest, artifactsDirectory, "post-partitions");
        Console.Out.WriteLine($"Post-deploy manifest: {postPath}");

        var droppedTables = new HashSet<string>(plan.ChangeSet.TablesToDrop, StringComparer.Ordinal);
        foreach (var (table, prePartitions) in preManifest.Tables)
        {
            if (droppedTables.Contains(table)) continue;
            if (!postManifest.Tables.TryGetValue(table, out var postPartitions))
            {
                Console.Error.WriteLine($"Partition integrity violation: table '{table}' missing post-deploy.");
                return ExitCodes.PartitionIntegrityError;
            }
            var postNames = postPartitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var missing = prePartitions.Where(p => !postNames.Contains(p.Name)).Select(p => p.Name).ToList();
            if (missing.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Partition integrity violation on '{table}': missing post-deploy: {string.Join(", ", missing)}");
                return ExitCodes.PartitionIntegrityError;
            }
        }

        // 9. Refresh
        if (!noRefresh && !plan.ChangeSet.IsEmpty)
        {
            var req = new RefreshRequest(workspaceUrl, databaseName, token, plan.ChangeSet, effectiveDate);
            var rrx = await refreshRunner.RefreshAsync(req,
                progress: new Progress<string>(line => Console.Out.WriteLine(line)),
                cancellationToken: cancellationToken);
            if (!rrx.Success)
            {
                Console.Error.WriteLine("Refresh failed (deploy succeeded). Investigate.");
                return ExitCodes.RefreshError;
            }
        }

        // 10. Receipt
        var receipt = new
        {
            ts, databaseName, workspaceUrl,
            add = plan.ChangeSet.TablesToAdd.Select(t => t.Name).ToArray(),
            drop = plan.ChangeSet.TablesToDrop.ToArray(),
            alter = plan.ChangeSet.TablesToAlter.Select(t => t.Name).ToArray(),
            unchanged = plan.ChangeSet.TablesUnchanged.ToArray(),
            preManifest = prePath,
            postManifest = postPath,
            planTmsl = planPath,
            refreshSkipped = noRefresh
        };
        var receiptPath = Path.Combine(artifactsDirectory, $"{ts}-{databaseName}-receipt.json");
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        Console.Out.WriteLine($"Receipt: {receiptPath}");

        return ExitCodes.Success;
    }
}
```

- [ ] **Step 4: Wire into Program.cs**

Add to `BuildRoot`:
```csharp
root.Subcommands.Add(DeployCommand.Build());
```

- [ ] **Step 5: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~DeployCommandTests
```
Expected: 1 PASS.

```bash
git add src/Weft.Cli/Commands/DeployCommand.cs src/Weft.Cli/Program.cs test/Weft.Cli.Tests/Helpers/ test/Weft.Cli.Tests/DeployCommandTests.cs
git commit -m "feat(cli): deploy command (full pipeline with pre/post manifests + integrity gate)"
```

---

### Task 26: DeployCommand failure-path tests

**Files:**
- Modify: `test/Weft.Cli.Tests/DeployCommandTests.cs`

- [ ] **Step 1: Add failure-path tests**

Append to `DeployCommandTests` class:

```csharp
[Fact]
public async Task Returns_AuthError_when_token_acquisition_throws()
{
    var src = TinyStaticPath();
    var tgtDb = new BimFileLoader().Load(src);
    var artifacts = Directory.CreateTempSubdirectory().FullName;
    try
    {
        var auth = NSubstitute.Substitute.For<Weft.Core.Abstractions.IAuthProvider>();
        auth.GetTokenAsync(default).ReturnsForAnyArgs<Task<Weft.Core.Abstractions.AccessToken>>(
            _ => throw new InvalidOperationException("aad down"));

        var exit = await DeployCommand.RunAsync(
            source: src, workspaceUrl: "powerbi://x", databaseName: "TinyStatic",
            artifactsDirectory: artifacts, allowDrops: false, noRefresh: false,
            resetBookmarks: false, effectiveDate: null,
            auth: auth,
            targetReader: CliTestHost.StubTarget(tgtDb),
            executor: CliTestHost.MakeExecutor(),
            refreshRunner: CliTestHost.MakeRefreshRunner(),
            manifestStore: new Weft.Xmla.FilePartitionManifestStore());

        exit.Should().Be(ExitCodes.AuthError);
    }
    finally { Directory.Delete(artifacts, recursive: true); }
}

[Fact]
public async Task Returns_DiffValidationError_when_drop_attempted_without_allow_drops()
{
    var src = TinyStaticPath();
    var tgtDb = new BimFileLoader().Load(src);
    tgtDb.Model.Tables.Add(new Microsoft.AnalysisServices.Tabular.Table { Name = "Orphan" });
    var artifacts = Directory.CreateTempSubdirectory().FullName;
    try
    {
        var exit = await DeployCommand.RunAsync(
            source: src, workspaceUrl: "powerbi://x", databaseName: "TinyStatic",
            artifactsDirectory: artifacts, allowDrops: false, noRefresh: false,
            resetBookmarks: false, effectiveDate: null,
            auth: CliTestHost.MakeAuth(),
            targetReader: CliTestHost.StubTarget(tgtDb),
            executor: CliTestHost.MakeExecutor(),
            refreshRunner: CliTestHost.MakeRefreshRunner(),
            manifestStore: new Weft.Xmla.FilePartitionManifestStore());

        exit.Should().Be(ExitCodes.DiffValidationError);
    }
    finally { Directory.Delete(artifacts, recursive: true); }
}

[Fact]
public async Task Returns_TmslExecutionError_when_executor_reports_failure()
{
    var src = TinyStaticPath();
    var tgtDb = new BimFileLoader().Load(src);
    tgtDb.Model.Tables["FactSales"].Columns.Add(
        new Microsoft.AnalysisServices.Tabular.DataColumn
        {
            Name = "ToDelete", DataType = Microsoft.AnalysisServices.Tabular.DataType.String, SourceColumn = "x"
        });
    var artifacts = Directory.CreateTempSubdirectory().FullName;
    try
    {
        var exit = await DeployCommand.RunAsync(
            source: src, workspaceUrl: "powerbi://x", databaseName: "TinyStatic",
            artifactsDirectory: artifacts, allowDrops: false, noRefresh: false,
            resetBookmarks: false, effectiveDate: null,
            auth: CliTestHost.MakeAuth(),
            targetReader: CliTestHost.StubTarget(tgtDb),
            executor: CliTestHost.MakeExecutor(success: false),
            refreshRunner: CliTestHost.MakeRefreshRunner(),
            manifestStore: new Weft.Xmla.FilePartitionManifestStore());

        exit.Should().Be(ExitCodes.TmslExecutionError);
    }
    finally { Directory.Delete(artifacts, recursive: true); }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~DeployCommandTests
```
Expected: 4 PASS (1 happy + 3 failure paths).

- [ ] **Step 2: Commit**

```bash
git add test/Weft.Cli.Tests/DeployCommandTests.cs
git commit -m "test(cli): deploy failure-path coverage (auth, drops, exec failure)"
```

---

### Task 27: `RefreshCommand`

**Files:**
- Create: `src/Weft.Cli/Commands/RefreshCommand.cs`

`weft refresh --workspace ... --database ... --tables A,B` — refreshes specific tables. Used out-of-band from a deploy.

- [ ] **Step 1: Implement (no unit test; integration-tested in T31)**

`src/Weft.Cli/Commands/RefreshCommand.cs`:
```csharp
using System.CommandLine;
using Microsoft.AnalysisServices.Tabular;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Core.Diffing;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class RefreshCommand
{
    public static Command Build()
    {
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var tables    = new Option<string>("--tables")
            { Description = "Comma-separated table names to refresh.", Required = true };
        var effectiveDate = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();

        var cmd = new Command("refresh", "Refresh selected tables.");
        foreach (var o in new Option[] { workspace, database, tables, effectiveDate,
                                         authMode, tenant, client, clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);

        cmd.SetAction(async (parse, ct) =>
        {
            var auth = ProfileResolver.BuildAuthOptions(
                parse.GetValue(authMode), parse.GetValue(tenant), parse.GetValue(client),
                parse.GetValue(clientSecret), parse.GetValue(certPath), parse.GetValue(certPwd),
                parse.GetValue(certThumb));
            var provider = AuthProviderFactory.Create(auth);
            var token = await provider.GetTokenAsync(ct);

            var names = parse.GetValue(tables)!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var entries = names.Select(n => new RefreshTableEntry(
                n, new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false))).ToList();
            var tmsl = new RefreshCommandBuilder().Build(parse.GetValue(database)!, entries, parse.GetValue(effectiveDate));
            var result = await new XmlaExecutor().ExecuteAsync(
                parse.GetValue(workspace)!, parse.GetValue(database)!, token, tmsl, ct);

            foreach (var m in result.Messages) Console.Out.WriteLine(m);
            return result.Success ? ExitCodes.Success : ExitCodes.RefreshError;
        });
        return cmd;
    }
}
```

- [ ] **Step 2: Wire + commit**

Add to `BuildRoot`:
```csharp
root.Subcommands.Add(RefreshCommand.Build());
```

```bash
dotnet build
git add src/Weft.Cli/Commands/RefreshCommand.cs src/Weft.Cli/Program.cs
git commit -m "feat(cli): refresh command (Full type on listed tables)"
```

---

### Task 28: `RestoreHistoryCommand`

**Files:**
- Create: `src/Weft.Cli/Commands/RestoreHistoryCommand.cs`

Issues a `Policy` refresh with `applyRefreshPolicy: true` and an effective date for one table. Date-range computation via `RestorePartitionSet` is informational only — the actual partition materialization is the service's job.

- [ ] **Step 1: Implement**

`src/Weft.Cli/Commands/RestoreHistoryCommand.cs`:
```csharp
using System.CommandLine;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class RestoreHistoryCommand
{
    public static Command Build()
    {
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var table     = new Option<string>("--table") { Description = "Table to restore.", Required = true };
        var fromOpt   = new Option<string?>("--from")  { Description = "ISO date (inclusive)." };
        var toOpt     = new Option<string?>("--to")    { Description = "ISO date (inclusive)." };
        var effectiveDate = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();

        var cmd = new Command("restore-history", "Re-materialize historical partitions per the table's RefreshPolicy.");
        foreach (var o in new Option[] { workspace, database, table, fromOpt, toOpt, effectiveDate,
                                         authMode, tenant, client, clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);

        cmd.SetAction(async (parse, ct) =>
        {
            var auth = ProfileResolver.BuildAuthOptions(
                parse.GetValue(authMode), parse.GetValue(tenant), parse.GetValue(client),
                parse.GetValue(clientSecret), parse.GetValue(certPath), parse.GetValue(certPwd),
                parse.GetValue(certThumb));
            var provider = AuthProviderFactory.Create(auth);
            var token = await provider.GetTokenAsync(ct);

            var tableName = parse.GetValue(table)!;
            var entries = new[]
            {
                new RefreshTableEntry(tableName,
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true))
            };
            var tmsl = new RefreshCommandBuilder().Build(
                parse.GetValue(database)!,
                entries,
                parse.GetValue(effectiveDate) ?? parse.GetValue(toOpt));

            Console.Out.WriteLine(
                $"Restoring history for '{tableName}' from {parse.GetValue(fromOpt) ?? "<policy start>"} to {parse.GetValue(toOpt) ?? "<today>"}.");
            Console.Out.WriteLine("WARNING: this can only recover data the source system still has.");

            var result = await new XmlaExecutor().ExecuteAsync(
                parse.GetValue(workspace)!, parse.GetValue(database)!, token, tmsl, ct);
            foreach (var m in result.Messages) Console.Out.WriteLine(m);
            return result.Success ? ExitCodes.Success : ExitCodes.RefreshError;
        });
        return cmd;
    }
}
```

- [ ] **Step 2: Wire + commit**

Add to `BuildRoot`:
```csharp
root.Subcommands.Add(RestoreHistoryCommand.Build());
```

```bash
dotnet build
git add src/Weft.Cli/Commands/RestoreHistoryCommand.cs src/Weft.Cli/Program.cs
git commit -m "feat(cli): restore-history command (Policy refresh + effective-date)"
```

---

### Task 29: CLI smoke test (build the binary and shell out)

**Files:**
- Create: `test/Weft.Cli.Tests/CliSmokeTests.cs`

- [ ] **Step 1: Failing test**

`test/Weft.Cli.Tests/CliSmokeTests.cs`:
```csharp
using System.Diagnostics;
using FluentAssertions;

namespace Weft.Cli.Tests;

public class CliSmokeTests
{
    private static string CliPath()
    {
        // Built CLI binary from `dotnet build`
        var rid = "net10.0";
        return Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Weft.Cli", "bin", "Debug", rid, "weft.dll");
    }

    private static (int Exit, string Stdout, string Stderr) Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { CliPath() },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void Help_works()
    {
        var (exit, stdout, _) = Run("--help");
        exit.Should().Be(0);
        stdout.Should().Contain("validate");
        stdout.Should().Contain("plan");
        stdout.Should().Contain("deploy");
        stdout.Should().Contain("refresh");
    }

    [Fact]
    public void Validate_on_fixture_succeeds()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");
        var (exit, _, _) = Run("validate", "--source", fixture);
        exit.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run + commit**

```bash
dotnet build
dotnet test --filter FullyQualifiedName~CliSmokeTests
```
Expected: 2 PASS.

```bash
git add test/Weft.Cli.Tests/CliSmokeTests.cs
git commit -m "test(cli): smoke tests via dotnet exec on built CLI binary"
```

---

### Task 30: `Weft.Integration.Tests` project + IntegrationTestFact

**Files:**
- Create: `test/Weft.Integration.Tests/Weft.Integration.Tests.csproj`
- Create: `test/Weft.Integration.Tests/IntegrationTestFact.cs`

- [ ] **Step 1: New project**

```bash
mkdir -p test/Weft.Integration.Tests
cd test/Weft.Integration.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

`test/Weft.Integration.Tests/Weft.Integration.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <Using Include="Xunit" />
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Weft.Auth\Weft.Auth.csproj" />
    <ProjectReference Include="..\..\src\Weft.Xmla\Weft.Xmla.csproj" />
    <ProjectReference Include="..\..\src\Weft.Cli\Weft.Cli.csproj" />
  </ItemGroup>
</Project>
```

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add test/Weft.Integration.Tests/Weft.Integration.Tests.csproj
```

- [ ] **Step 2: Custom fact attribute that skips when env vars missing**

`test/Weft.Integration.Tests/IntegrationTestFact.cs`:
```csharp
using Xunit;

namespace Weft.Integration.Tests;

public sealed class IntegrationTestFactAttribute : FactAttribute
{
    public IntegrationTestFactAttribute()
    {
        var required = new[]
        {
            "WEFT_INT_WORKSPACE",
            "WEFT_INT_DATABASE",
            "WEFT_INT_TENANT_ID",
            "WEFT_INT_CLIENT_ID",
            "WEFT_INT_CLIENT_SECRET"
        };
        var missing = required.Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))).ToList();
        if (missing.Count > 0)
            Skip = $"Integration tests skipped — missing env: {string.Join(", ", missing)}";
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add test/Weft.Integration.Tests/ weft.sln
git commit -m "test(integration): scaffold Weft.Integration.Tests with env-gated [IntegrationTestFact]"
```

---

### Task 31: `EndToEndDeployTests` (integration)

**Files:**
- Create: `test/Weft.Integration.Tests/EndToEndDeployTests.cs`

This test only runs when integration env vars are present. CI sets them; local dev usually doesn't.

- [ ] **Step 1: Test**

`test/Weft.Integration.Tests/EndToEndDeployTests.cs`:
```csharp
using FluentAssertions;
using Weft.Auth;
using Weft.Cli;
using Weft.Cli.Commands;
using Weft.Xmla;

namespace Weft.Integration.Tests;

public class EndToEndDeployTests
{
    [IntegrationTestFact]
    public async Task Deploys_tiny_static_against_test_workspace()
    {
        var workspace = Environment.GetEnvironmentVariable("WEFT_INT_WORKSPACE")!;
        var database  = Environment.GetEnvironmentVariable("WEFT_INT_DATABASE")!;
        var tenant    = Environment.GetEnvironmentVariable("WEFT_INT_TENANT_ID")!;
        var clientId  = Environment.GetEnvironmentVariable("WEFT_INT_CLIENT_ID")!;
        var secret    = Environment.GetEnvironmentVariable("WEFT_INT_CLIENT_SECRET")!;

        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var auth = AuthProviderFactory.Create(new AuthOptions(
                AuthMode.ServicePrincipalSecret, tenant, clientId, ClientSecret: secret));

            var exit = await DeployCommand.RunAsync(
                source: fixture,
                workspaceUrl: workspace,
                databaseName: database,
                artifactsDirectory: artifacts,
                allowDrops: true,    // CI workspace; safe
                noRefresh: true,     // skip refresh to keep test fast
                resetBookmarks: false,
                effectiveDate: null,
                auth: auth,
                targetReader: new TargetReader(),
                executor: new XmlaExecutor(),
                refreshRunner: new RefreshRunner(new XmlaExecutor()),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifacts, "*-pre-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-post-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-receipt.json").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }
}
```

- [ ] **Step 2: Build + commit (test will SKIP locally without env vars)**

```bash
dotnet build
dotnet test --filter FullyQualifiedName~EndToEndDeployTests
```
Expected: SKIP locally; PASS in CI when env vars are set.

```bash
git add test/Weft.Integration.Tests/EndToEndDeployTests.cs
git commit -m "test(integration): end-to-end deploy against env-configured workspace"
```

---

### Task 32: Plan-1 carry-overs (optional cleanup)

**Files:**
- Modify: `src/Weft.Core/RefreshPolicy/RefreshPolicyComparer.cs`
- Modify: `src/Weft.Core/Tmsl/PartitionIntegrityValidator.cs`
- Modify: `test/Weft.Core.Tests/RefreshPolicy/RefreshPolicyComparerTests.cs`
- Modify: `test/Weft.Core.Tests/Tmsl/PartitionIntegrityValidatorTests.cs`

Address two latent issues from the Plan-1 final review:

**Issue A:** `RefreshPolicyComparer` falls back to `a.GetType() == b.GetType()` for non-Basic policy subclasses, silently reporting "equal" even when fields differ.

- [ ] **Step 1: Strengthen the fallback (RefreshPolicyComparer)**

Replace the final `return a.GetType() == b.GetType();` with:
```csharp
throw new NotSupportedException(
    $"Refresh policy comparison not implemented for type {a.GetType().FullName}. " +
    $"File an issue if Microsoft has shipped a new RefreshPolicy subclass.");
```

Add a test:
```csharp
private sealed class FakePolicy : Microsoft.AnalysisServices.Tabular.RefreshPolicy { }

[Fact]
public void Throws_for_unknown_policy_subclass()
{
    var act = () => new RefreshPolicyComparer().AreEqual(new FakePolicy(), new FakePolicy());
    act.Should().Throw<NotSupportedException>();
}
```

(`Microsoft.AnalysisServices.Tabular.RefreshPolicy` is abstract — if `FakePolicy` won't compile, the test can be skipped; document why with a comment.)

**Issue B:** `PartitionIntegrityValidator` only flags "target had bookmark, emitted didn't". An emitted bookmark on a partition that target had none is silently allowed. Plan 2 fixes this:

- [ ] **Step 2: Symmetric bookmark check**

In `PartitionIntegrityValidator.cs`, change the bookmark loop:
```csharp
foreach (var emitted in emittedPartitionNodes)
{
    var name = emitted?["name"]?.GetValue<string>();
    if (name is null) continue;
    if (!target.Model.Tables[tableName].Partitions.ContainsName(name)) continue;

    var targetPartition = target.Model.Tables[tableName].Partitions[name];
    var targetBookmark = targetPartition.Annotations
        .Find(PartitionAnnotationNames.RefreshBookmark)?.Value;

    var emittedBookmark = ((emitted!["annotations"] as JsonArray) ?? new JsonArray())
        .OfType<JsonObject>()
        .FirstOrDefault(a => a["name"]?.GetValue<string>() == PartitionAnnotationNames.RefreshBookmark)
        ?["value"]?.GetValue<string>();

    var targetEmpty = string.IsNullOrEmpty(targetBookmark);
    var emittedEmpty = string.IsNullOrEmpty(emittedBookmark);

    if (targetEmpty && emittedEmpty) continue;

    if (!string.Equals(targetBookmark, emittedBookmark, StringComparison.Ordinal))
    {
        throw new PartitionIntegrityException(
            $"Bookmark integrity violation on '{tableName}'/'{name}': " +
            $"target RefreshBookmark '{targetBookmark ?? "<none>"}' did not match emitted '{emittedBookmark ?? "<none>"}'.");
    }
}
```

Add a test that covers the new direction:
```csharp
[Fact]
public void Throws_when_emitted_bookmark_appears_on_partition_without_one_on_target()
{
    var src = FixtureLoader.LoadBim("models/tiny-static.bim");
    var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
    // target has NO bookmark on FactSales partition.

    var malicious = """
    {
      "sequence": {
        "maxParallelism": 1,
        "operations": [
          {
            "createOrReplace": {
              "object": { "database": "TinyStatic", "table": "FactSales" },
              "table": {
                "name": "FactSales",
                "columns": [],
                "partitions": [
                  {
                    "name": "FactSales", "mode": "import",
                    "source": { "type": "m", "expression": "x" },
                    "annotations": [ { "name": "RefreshBookmark", "value": "injected" } ]
                  }
                ]
              }
            }
          }
        ]
      }
    }
    """;

    var cs = new ChangeSet(
        TablesToAdd: Array.Empty<TablePlan>(),
        TablesToDrop: Array.Empty<string>(),
        TablesToAlter: Array.Empty<TableDiff>(),
        TablesUnchanged: Array.Empty<string>(),
        MeasuresChanged: Array.Empty<string>(),
        RelationshipsChanged: Array.Empty<string>(),
        RolesChanged: Array.Empty<string>(),
        PerspectivesChanged: Array.Empty<string>(),
        CulturesChanged: Array.Empty<string>(),
        ExpressionsChanged: Array.Empty<string>(),
        DataSourcesChanged: Array.Empty<string>());

    var act = () => new PartitionIntegrityValidator().Validate(malicious, tgt, cs);
    act.Should().Throw<PartitionIntegrityException>().WithMessage("*injected*");
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test
```
Expected: all tests still pass + new tests pass.

```bash
git add src/Weft.Core/RefreshPolicy/RefreshPolicyComparer.cs src/Weft.Core/Tmsl/PartitionIntegrityValidator.cs test/Weft.Core.Tests/RefreshPolicy/RefreshPolicyComparerTests.cs test/Weft.Core.Tests/Tmsl/PartitionIntegrityValidatorTests.cs
git commit -m "fix(core): plan-1 carry-overs (policy-comparer subclass guard, symmetric bookmark check)"
```

---

### Task 33: Final clean build + tag

- [ ] **Step 1: Full pipeline**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet clean
dotnet build -warnaserror
dotnet test
```
Expected:
- 0 build warnings, 0 errors.
- Test count ≥ 60 (Plan-1 base 41 + ~20 from Plan 2).
- Integration tests skipped locally (no env vars).

- [ ] **Step 2: Tag**

```bash
git tag -a plan-2-auth-xmla-cli-complete -m "Weft Plan 2: Auth + XMLA + CLI complete"
git log --oneline | head -40
```

---

## Spec coverage check (run after Task 33)

| Spec section | Plan-2 task(s) |
|---|---|
| §4 CLI commands | Tasks 22–28 |
| §5.2 TargetReader | Task 13 |
| §5.5 XmlaExecutor | Task 14 |
| §5.6 AuthProvider (5 modes + factory) | Tasks 6, 7, 8, 9 |
| §6 Data Flow steps 1–17 (deploy pipeline) | Task 25 |
| §6 step 12a partition integrity gate | Task 25 (post-manifest diff) |
| §6 step 13 per-table refresh-type matrix | Task 15 |
| §7A.7 `--reset-bookmarks` CLI flag | Task 21 (option) + Task 25 (wired into executor — note: Plan 2 acceptance is the flag exists; clearing logic is implemented in Plan 3 alongside config knobs) |
| §9 exit codes | Tasks 19, 22, 23, 25–28 |
| §9.3 observability (JSON logs) | Task 20 |
| §10 unit-test priority 4 (refresh-type matrix) | Task 15 |

Items NOT in this plan and tracked elsewhere:
- §7 Parameter resolution / overrides — Plan 3.
- §7A.4 config knobs (`bookmarkMode`, `applyOnFirstDeploy`, `dynamicPartitionStrategy`) — Plan 3.
- §6 step 6 history-loss pre-flight gate — Plan 3 (depends on config + retention calc wiring).
- Hooks (§5.9) — Plan 3.
- Octopus / TeamCity / docs / samples — Plan 4.

---

## Done criteria for Plan 2

- [ ] All 33 tasks committed.
- [ ] `dotnet test` passes (60+ tests), 0 warnings, 0 errors.
- [ ] `weft --help` lists all six commands: validate, plan, deploy, refresh, restore-history, inspect.
- [ ] Integration tests skip cleanly when env vars absent; pass when set.
- [ ] Tag `plan-2-auth-xmla-cli-complete` exists.
- [ ] Plan-1 carry-overs (Important issues 2 and 5) addressed in Task 32.

When all items above are checked, Plan 2 is complete and Plan 3 (Config + Parameters + Hooks) can begin.
