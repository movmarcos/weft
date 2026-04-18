# Weft Studio v0.1.1 — Open from Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a **File → Connect to workspace…** entry point that signs in to AAD, lists datasets from a Power BI / Fabric workspace via XMLA, opens the selected one as a read-only snapshot, and disables Save (Save As .bim still works).

**Architecture:** `ConnectionManager` (App layer, stateless) orchestrates 3 async calls — `SignInAsync`, `ListDatasetsAsync`, `FetchModelAsync` — each mockable via existing interfaces (`IAuthProvider`, `ITargetReader`). The UI layer adds a `ConnectDialog` (modal, progressive state machine) that drives `ConnectionManager` and hands the resulting `ModelSession` to `ShellViewModel.Explorer`. Snapshot semantics enforced by a new `ModelSession.ReadOnly` flag that disables `SaveCommand` and shows an orange banner in the shell.

**Tech Stack:** .NET 10, Avalonia 11.2 + ReactiveUI, Avalonia.Controls.DataGrid (new), MSAL (via existing Weft.Auth), ADOMD (via existing Weft.Xmla), xUnit + FluentAssertions + NSubstitute + Avalonia.Headless.XUnit.

**Spec reference:** `docs/superpowers/specs/2026-04-18-weft-studio-v0.1.1-open-from-workspace-design.md`

---

## File structure at end of v0.1.1 (new and modified files only)

```
src/Weft.Auth/
├── AuthOptionsValidator.cs            MODIFIED — TenantId optional for Interactive/DeviceCode
├── InteractiveAuth.cs                 MODIFIED — /common authority when TenantId empty
└── DeviceCodeAuth.cs                  MODIFIED — /common authority when TenantId empty

test/Weft.Auth.Tests/
└── AuthOptionsValidatorTests.cs       MODIFIED — new tests for /common behavior

studio/src/WeftStudio.App/
├── ModelSession.cs                    MODIFIED — adds ReadOnly property + workspace ctor
├── Connections/                       NEW folder
│   ├── ConnectionManager.cs
│   ├── WorkspaceReference.cs
│   ├── WorkspaceUrlException.cs
│   └── DatasetInfo.cs
├── AppSettings/                       NEW folder
│   └── ClientIdProvider.cs
└── Settings/
    └── Settings.cs                    MODIFIED — adds RecentWorkspaces + ClientIdOverride

studio/src/WeftStudio.Ui/
├── WeftStudio.Ui.csproj               MODIFIED — adds Avalonia.Controls.DataGrid
├── Connect/                           NEW folder
│   ├── ConnectDialog.axaml(.cs)
│   ├── ConnectDialogViewModel.cs
│   ├── ConnectDialogState.cs
│   └── DatasetRow.cs
└── Shell/
    ├── ShellViewModel.cs              MODIFIED — OpenWorkspaceCommand, IsReadOnly, WorkspaceLabel
    ├── ShellWindow.axaml              MODIFIED — File menu, read-only banner
    └── ShellWindow.axaml.cs           MODIFIED — OnConnectToWorkspace handler

studio/test/WeftStudio.App.Tests/
├── WorkspaceReferenceTests.cs         NEW
├── ClientIdProviderTests.cs           NEW
├── ModelSessionReadOnlyTests.cs       NEW
├── ConnectionManagerTests.cs          NEW
└── SettingsRecentWorkspacesTests.cs   NEW

studio/test/WeftStudio.Ui.Tests/
├── ConnectDialogViewModelTests.cs     NEW
├── DatasetFilterTests.cs              NEW
└── ShellViewModelReadOnlyTests.cs     NEW
```

Total new files: 17. Modified files: 8.

---

## Phase 1 — App-layer foundation (no UI yet)

### Task 1: Add ReadOnly flag to ModelSession

**Files:**
- Modify: `studio/src/WeftStudio.App/ModelSession.cs`
- Create: `studio/test/WeftStudio.App.Tests/ModelSessionReadOnlyTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// studio/test/WeftStudio.App.Tests/ModelSessionReadOnlyTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;

namespace WeftStudio.App.Tests;

public class ModelSessionReadOnlyTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void OpenBim_sessions_are_not_ReadOnly()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Workspace_ctor_produces_ReadOnly_session()
    {
        var source = ModelSession.OpenBim(FixturePath);
        var ws = new ModelSession(source.Database, sourcePath: null, readOnly: true);
        ws.ReadOnly.Should().BeTrue();
        ws.SourcePath.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ModelSessionReadOnlyTests`
Expected: compilation FAIL — `ReadOnly` property and 3-arg constructor don't exist.

- [ ] **Step 3: Modify ModelSession**

Replace the constructor and add `ReadOnly`:

```csharp
// studio/src/WeftStudio.App/ModelSession.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Loading;

namespace WeftStudio.App;

public sealed class ModelSession
{
    public Database Database { get; }
    public string? SourcePath { get; }
    public bool ReadOnly { get; }
    public bool IsDirty => ChangeTracker.HasUncommittedCommands;
    public ChangeTracker ChangeTracker { get; }

    internal ModelSession(Database db, string? sourcePath, bool readOnly = false)
    {
        Database = db;
        SourcePath = sourcePath;
        ReadOnly = readOnly;
        ChangeTracker = new ChangeTracker();
    }

    public static ModelSession OpenBim(string path)
    {
        var loader = new BimFileLoader();
        var database = loader.Load(path);
        return new ModelSession(database, path, readOnly: false);
    }
}
```

Note: constructor is still `internal` (Task 11 in v0.1.0 added `InternalsVisibleTo`). The 3-arg form replaces the old 2-arg form; both callers in the codebase pass through the factory.

- [ ] **Step 4: Run all tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: all existing tests still pass (31 pre-existing + 2 new = 33 passing). 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/ModelSession.cs studio/test/WeftStudio.App.Tests/ModelSessionReadOnlyTests.cs
git commit -m "feat(app): add ReadOnly flag to ModelSession for workspace-loaded sessions"
```

---

### Task 2: WorkspaceReference parsing

**Files:**
- Create: `studio/src/WeftStudio.App/Connections/WorkspaceReference.cs`
- Create: `studio/src/WeftStudio.App/Connections/WorkspaceUrlException.cs`
- Create: `studio/test/WeftStudio.App.Tests/WorkspaceReferenceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// studio/test/WeftStudio.App.Tests/WorkspaceReferenceTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Connections;

namespace WeftStudio.App.Tests;

public class WorkspaceReferenceTests
{
    [Fact]
    public void Parse_accepts_powerbi_fabric_url_and_extracts_workspace_name()
    {
        var r = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/DEV - Finance");
        r.Server.Should().Be("powerbi://api.powerbi.com/v1.0/myorg/DEV - Finance");
        r.WorkspaceName.Should().Be("DEV - Finance");
    }

    [Fact]
    public void Parse_accepts_asazure_url_and_leaves_workspace_name_empty()
    {
        var r = WorkspaceReference.Parse("asazure://westeurope.asazure.windows.net/my-aas");
        r.Server.Should().Be("asazure://westeurope.asazure.windows.net/my-aas");
        r.WorkspaceName.Should().Be("");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://api.powerbi.com/v1.0/myorg/foo")]
    [InlineData("not-a-url")]
    public void Parse_rejects_malformed_input(string url)
    {
        Action act = () => WorkspaceReference.Parse(url);
        act.Should().Throw<WorkspaceUrlException>();
    }

    [Fact]
    public void Parse_trims_trailing_whitespace()
    {
        var r = WorkspaceReference.Parse("  powerbi://api.powerbi.com/v1.0/myorg/X  ");
        r.Server.Should().Be("powerbi://api.powerbi.com/v1.0/myorg/X");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter WorkspaceReferenceTests`
Expected: compilation FAIL — `WorkspaceReference` and `WorkspaceUrlException` missing.

- [ ] **Step 3: Implement**

```csharp
// studio/src/WeftStudio.App/Connections/WorkspaceUrlException.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

public sealed class WorkspaceUrlException : Exception
{
    public WorkspaceUrlException(string message) : base(message) { }
}
```

```csharp
// studio/src/WeftStudio.App/Connections/WorkspaceReference.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

public sealed record WorkspaceReference(string Server, string WorkspaceName)
{
    private static readonly string[] ValidSchemes = { "powerbi://", "asazure://" };

    public static WorkspaceReference Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new WorkspaceUrlException("XMLA endpoint URL cannot be empty.");

        var trimmed = url.Trim();

        if (!ValidSchemes.Any(s => trimmed.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            throw new WorkspaceUrlException(
                "Must start with powerbi:// or asazure://");

        // For Power BI Fabric: .../myorg/<workspace-name>
        var workspaceName = "";
        if (trimmed.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
        {
            var marker = "/myorg/";
            var idx = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                workspaceName = trimmed[(idx + marker.Length)..].Trim('/');
        }

        return new WorkspaceReference(trimmed, workspaceName);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln --filter WorkspaceReferenceTests`
Expected: 6 passed (1 powerbi + 1 asazure + 4 malformed + 1 trim = actually 7: 1+1+4 theory rows+1).

Run full suite: `dotnet test studio/weft-studio.sln` — expect 40 passed, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/Connections studio/test/WeftStudio.App.Tests/WorkspaceReferenceTests.cs
git commit -m "feat(app/connections): WorkspaceReference parses XMLA endpoint URL"
```

---

### Task 3: DatasetInfo record

**Files:**
- Create: `studio/src/WeftStudio.App/Connections/DatasetInfo.cs`

No tests of its own — exercised by `ConnectionManagerTests` in Task 7.

- [ ] **Step 1: Write the record**

```csharp
// studio/src/WeftStudio.App/Connections/DatasetInfo.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

/// <summary>
/// One row in the dataset picker grid.
/// Fields may be null/0 when XMLA doesn't surface them — the grid shows "-" in that case.
/// </summary>
public sealed record DatasetInfo(
    string Name,
    long? SizeBytes,
    DateTime? LastUpdatedUtc,
    string? RefreshPolicy,
    string? Owner);
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build studio/weft-studio.sln
```
Expected: 0 warnings, 0 errors.

```bash
git add studio/src/WeftStudio.App/Connections/DatasetInfo.cs
git commit -m "feat(app/connections): DatasetInfo record for dataset-picker grid rows"
```

---

### Task 4: ClientIdProvider with precedence resolver

**Files:**
- Create: `studio/src/WeftStudio.App/AppSettings/ClientIdProvider.cs`
- Create: `studio/test/WeftStudio.App.Tests/ClientIdProviderTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// studio/test/WeftStudio.App.Tests/ClientIdProviderTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.AppSettings;

namespace WeftStudio.App.Tests;

public class ClientIdProviderTests
{
    [Fact]
    public void Commandline_arg_takes_highest_precedence()
    {
        var id = ClientIdProvider.Resolve(
            commandLineArg: "cli-guid",
            envVar: "env-guid",
            userOverride: "settings-guid",
            baked: "baked-guid");
        id.Should().Be("cli-guid");
    }

    [Fact]
    public void Env_var_second_when_no_commandline()
    {
        var id = ClientIdProvider.Resolve(
            commandLineArg: null,
            envVar: "env-guid",
            userOverride: "settings-guid",
            baked: "baked-guid");
        id.Should().Be("env-guid");
    }

    [Fact]
    public void Settings_override_third_when_no_commandline_or_env()
    {
        var id = ClientIdProvider.Resolve(null, null, "settings-guid", "baked-guid");
        id.Should().Be("settings-guid");
    }

    [Fact]
    public void Baked_default_fallback_when_nothing_else_set()
    {
        var id = ClientIdProvider.Resolve(null, null, null, "baked-guid");
        id.Should().Be("baked-guid");
    }

    [Fact]
    public void Empty_strings_are_treated_as_null()
    {
        var id = ClientIdProvider.Resolve("", "", "", "baked-guid");
        id.Should().Be("baked-guid");
    }

    [Fact]
    public void All_empty_returns_empty_string_not_null()
    {
        var id = ClientIdProvider.Resolve(null, null, null, "");
        id.Should().Be("");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ClientIdProviderTests`
Expected: compilation FAIL — type missing.

- [ ] **Step 3: Implement**

```csharp
// studio/src/WeftStudio.App/AppSettings/ClientIdProvider.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.AppSettings;

public static class ClientIdProvider
{
    public const string EnvVarName = "WEFT_STUDIO_CLIENTID";

    public static string Resolve(
        string? commandLineArg,
        string? envVar,
        string? userOverride,
        string baked)
    {
        if (!string.IsNullOrWhiteSpace(commandLineArg)) return commandLineArg;
        if (!string.IsNullOrWhiteSpace(envVar))         return envVar;
        if (!string.IsNullOrWhiteSpace(userOverride))   return userOverride;
        return baked;
    }

    public static string ResolveFromEnvironment(string? commandLineArg, string? userOverride, string baked) =>
        Resolve(
            commandLineArg,
            Environment.GetEnvironmentVariable(EnvVarName),
            userOverride,
            baked);
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln --filter ClientIdProviderTests`
Expected: 6 passed. Full suite 46 passed, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/AppSettings studio/test/WeftStudio.App.Tests/ClientIdProviderTests.cs
git commit -m "feat(app/settings): ClientIdProvider with arg/env/override/baked precedence"
```

---

### Task 5: Extend Settings for RecentWorkspaces + ClientIdOverride

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Settings/Settings.cs`
- Create: `studio/test/WeftStudio.App.Tests/SettingsRecentWorkspacesTests.cs`

Note: `Settings` currently lives in `WeftStudio.Ui`. Move it to `WeftStudio.App` in this task — App-layer tests need it and it has no Avalonia dependency.

- [ ] **Step 1: Move Settings.cs to WeftStudio.App and extend**

Move `studio/src/WeftStudio.Ui/Settings/Settings.cs` → `studio/src/WeftStudio.App/Settings/Settings.cs`. Update namespace from `WeftStudio.Ui.Settings` to `WeftStudio.App.Settings`. Extend:

```csharp
// studio/src/WeftStudio.App/Settings/Settings.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Settings;

public sealed class Settings
{
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentWorkspace> RecentWorkspaces { get; set; } = new();
    public string? ClientIdOverride { get; set; }
}

public sealed record RecentWorkspace(
    string WorkspaceUrl,
    string LastDatasetName,
    string AuthMode,
    DateTime LastUsedUtc);
```

Also move `SettingsStore.cs` from `WeftStudio.Ui/Settings/` → `WeftStudio.App/Settings/`, updating the namespace identically.

Update every `using WeftStudio.Ui.Settings;` in the UI project to `using WeftStudio.App.Settings;`. Grep first:

```bash
grep -rn "WeftStudio.Ui.Settings" studio/
```

Should find usages in `ShellViewModel.cs` and in `SettingsStoreTests.cs`. Update both.

- [ ] **Step 2: Write a new test for RecentWorkspaces round-trip**

```csharp
// studio/test/WeftStudio.App.Tests/SettingsRecentWorkspacesTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Settings;

namespace WeftStudio.App.Tests;

public class SettingsRecentWorkspacesTests
{
    [Fact]
    public void RecentWorkspaces_round_trips_through_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(dir);
            var data = new Settings
            {
                RecentWorkspaces =
                {
                    new RecentWorkspace("powerbi://x/myorg/a", "ds1", "Interactive", DateTime.UtcNow),
                    new RecentWorkspace("powerbi://x/myorg/b", "ds2", "DeviceCode",  DateTime.UtcNow),
                },
                ClientIdOverride = "abc-123"
            };
            store.Save(data);

            var reloaded = new SettingsStore(dir).Load();
            reloaded.RecentWorkspaces.Should().HaveCount(2);
            reloaded.RecentWorkspaces[0].WorkspaceUrl.Should().Be("powerbi://x/myorg/a");
            reloaded.ClientIdOverride.Should().Be("abc-123");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 3: Update the UI test project reference**

In `studio/test/WeftStudio.Ui.Tests/WeftStudio.Ui.Tests.csproj` nothing needs to change — it already references both App and Ui projects. The existing `SettingsStoreTests.cs` may need its `using` updated.

Update `studio/test/WeftStudio.Ui.Tests/SettingsStoreTests.cs`: change `using WeftStudio.Ui.Settings;` → `using WeftStudio.App.Settings;`. Also change the alias: `using AppSettings = WeftStudio.App.Settings.Settings;`.

- [ ] **Step 4: Build and run full suite**

Run: `dotnet build studio/weft-studio.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test studio/weft-studio.sln`
Expected: existing tests still pass; 1 new test added. Total 47 passed.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/Settings studio/test studio/src/WeftStudio.Ui/Shell
git rm studio/src/WeftStudio.Ui/Settings/Settings.cs studio/src/WeftStudio.Ui/Settings/SettingsStore.cs 2>/dev/null || true
git commit -m "refactor: move Settings to WeftStudio.App + add RecentWorkspaces/ClientIdOverride"
```

---

### Task 6: Weft.Auth — TenantId optional for Interactive/DeviceCode

**Files:**
- Modify: `src/Weft.Auth/AuthOptionsValidator.cs`
- Modify: `src/Weft.Auth/InteractiveAuth.cs`
- Modify: `src/Weft.Auth/DeviceCodeAuth.cs`
- Modify: `test/Weft.Auth.Tests/AuthOptionsValidatorTests.cs` (or create if absent)

- [ ] **Step 1: Inspect current validator and auth implementations**

Run: `cat src/Weft.Auth/AuthOptionsValidator.cs src/Weft.Auth/InteractiveAuth.cs src/Weft.Auth/DeviceCodeAuth.cs`

Identify where `TenantId` is required and where `.WithTenantId(options.TenantId)` is called. The new behavior: for `Interactive` and `DeviceCode` modes, if `TenantId` is null/empty/`"common"`, use the `/common` authority. Service-principal modes still require TenantId.

- [ ] **Step 2: Write failing tests**

Locate existing `test/Weft.Auth.Tests/AuthOptionsValidatorTests.cs` (may exist). If absent, create it. Add these cases (merge with any existing):

```csharp
// test/Weft.Auth.Tests/AuthOptionsValidatorTests.cs (new or append)
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthOptionsValidatorTests
{
    [Theory]
    [InlineData(AuthMode.Interactive)]
    [InlineData(AuthMode.DeviceCode)]
    public void Interactive_and_DeviceCode_accept_empty_TenantId(AuthMode mode)
    {
        var opts = new AuthOptions(
            Mode: mode,
            TenantId: "",
            ClientId: "some-client-id",
            CertThumbprint: null,
            CertStoreLocation: null,
            CertStoreName: null,
            CertFilePath: null,
            CertFilePassword: null,
            ClientSecret: null);

        Action act = () => AuthOptionsValidator.Validate(opts);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(AuthMode.ServicePrincipalSecret)]
    [InlineData(AuthMode.ServicePrincipalCertStore)]
    [InlineData(AuthMode.ServicePrincipalCertFile)]
    public void ServicePrincipal_modes_still_require_TenantId(AuthMode mode)
    {
        var opts = new AuthOptions(
            Mode: mode,
            TenantId: "",
            ClientId: "some-client-id",
            CertThumbprint: "thumb",
            CertStoreLocation: "LocalMachine",
            CertStoreName: "My",
            CertFilePath: "/path.pfx",
            CertFilePassword: "pw",
            ClientSecret: "secret");

        Action act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>()
            .WithMessage("*TenantId*");
    }
}
```

Note: adjust the `AuthOptions` constructor parameter names to match whatever the existing record declares. If it's a positional-ctor `record`, read it from the source and align.

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test test/Weft.Auth.Tests --filter AuthOptionsValidatorTests`
Expected: FAIL — current validator throws for empty TenantId regardless of mode.

- [ ] **Step 4: Modify validator**

```csharp
// src/Weft.Auth/AuthOptionsValidator.cs
// ...existing header + usings...

public static class AuthOptionsValidator
{
    public static void Validate(AuthOptions options)
    {
        // (keep existing checks for ClientId always required, mode-specific fields, etc.)

        var tenantRequired =
            options.Mode is AuthMode.ServicePrincipalSecret
                        or AuthMode.ServicePrincipalCertStore
                        or AuthMode.ServicePrincipalCertFile;

        if (tenantRequired && string.IsNullOrWhiteSpace(options.TenantId))
            throw new AuthOptionsValidationException(
                "TenantId is required for service principal auth modes.");

        // Interactive and DeviceCode modes accept empty TenantId (→ /common authority).
        // ...rest of existing validation...
    }
}
```

Preserve all other validator behavior exactly. If the file is structured with early-return tenant check, replace just that block.

- [ ] **Step 5: Modify InteractiveAuth and DeviceCodeAuth**

In both `InteractiveAuth.cs` and `DeviceCodeAuth.cs`, locate the MSAL builder chain:

```csharp
PublicClientApplicationBuilder
    .Create(options.ClientId)
    .WithTenantId(options.TenantId)
    // ...
```

Replace with conditional authority selection:

```csharp
var builder = PublicClientApplicationBuilder.Create(options.ClientId);

if (string.IsNullOrWhiteSpace(options.TenantId)
    || string.Equals(options.TenantId, "common", StringComparison.OrdinalIgnoreCase))
{
    builder = builder.WithAuthority("https://login.microsoftonline.com/common");
}
else
{
    builder = builder.WithTenantId(options.TenantId);
}

var app = builder.Build();
```

Apply the same change verbatim to both `InteractiveAuth.cs` and `DeviceCodeAuth.cs`. Do NOT touch the three SP auth classes.

- [ ] **Step 6: Run all tests (both solutions)**

```bash
dotnet test weft.sln
dotnet test studio/weft-studio.sln
```

Expected: all tests green on both. CLI behavior preserved for SP modes (they still require TenantId); Interactive/DeviceCode now accept empty TenantId.

- [ ] **Step 7: Commit**

```bash
git add src/Weft.Auth test/Weft.Auth.Tests
git commit -m "feat(auth): TenantId optional for Interactive/DeviceCode (→ /common authority)"
```

---

### Task 7: ConnectionManager.SignInAsync

**Files:**
- Create: `studio/src/WeftStudio.App/Connections/ConnectionManager.cs`
- Create: `studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs`

This task stands up the skeleton + first method. Tasks 8 and 9 extend it.

- [ ] **Step 1: Add NSubstitute to App.Tests**

In `studio/test/WeftStudio.App.Tests/WeftStudio.App.Tests.csproj`, add:

```xml
<PackageReference Include="NSubstitute" Version="5.*" />
```

Alongside the existing FluentAssertions reference.

- [ ] **Step 2: Write failing test**

```csharp
// studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App.Connections;

namespace WeftStudio.App.Tests;

public class ConnectionManagerTests
{
    [Fact]
    public async Task SignInAsync_delegates_to_IAuthProvider_and_returns_AccessToken()
    {
        var auth = Substitute.For<IAuthProvider>();
        var fake = new AccessToken("jwt-token-value", DateTimeOffset.UtcNow.AddHours(1));
        auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(fake);

        var factory = Substitute.For<Func<AuthOptions, IAuthProvider>>();
        factory(Arg.Any<AuthOptions>()).Returns(auth);

        var mgr = new ConnectionManager(factory, reader: Substitute.For<ITargetReader>());
        var opts = new AuthOptions(AuthMode.Interactive, "", "client-id",
            null, null, null, null, null, null);

        var token = await mgr.SignInAsync(opts, CancellationToken.None);

        token.Value.Should().Be("jwt-token-value");
    }
}
```

Note: the `AuthOptions` constructor argument list must match the existing record definition. Check `src/Weft.Auth/AuthOptions.cs` and align.

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ConnectionManagerTests`
Expected: compilation FAIL — `ConnectionManager` missing.

- [ ] **Step 4: Implement ConnectionManager skeleton**

```csharp
// studio/src/WeftStudio.App/Connections/ConnectionManager.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;
using Weft.Core.Abstractions;

namespace WeftStudio.App.Connections;

/// <summary>
/// Orchestrates the three async steps of opening a workspace-hosted model:
/// sign in, list datasets, fetch model. Stateless — callers hold onto
/// intermediate values (token, workspace ref) between calls.
/// </summary>
public sealed class ConnectionManager
{
    private readonly Func<AuthOptions, IAuthProvider> _authProviderFactory;
    private readonly ITargetReader _reader;

    public ConnectionManager(
        Func<AuthOptions, IAuthProvider> authProviderFactory,
        ITargetReader reader)
    {
        _authProviderFactory = authProviderFactory;
        _reader = reader;
    }

    public async Task<AccessToken> SignInAsync(AuthOptions opts, CancellationToken ct)
    {
        var provider = _authProviderFactory(opts);
        return await provider.GetTokenAsync(ct);
    }

    // Task 8 adds ListDatasetsAsync, Task 9 adds FetchModelAsync.
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: +1 test passing. Full suite 49 passed, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add studio/src/WeftStudio.App/Connections/ConnectionManager.cs studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs studio/test/WeftStudio.App.Tests/WeftStudio.App.Tests.csproj
git commit -m "feat(app/connections): ConnectionManager.SignInAsync delegates to IAuthProvider"
```

---

### Task 8: ConnectionManager.ListDatasetsAsync

**Files:**
- Modify: `studio/src/WeftStudio.App/Connections/ConnectionManager.cs`
- Modify: `studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs`

- [ ] **Step 1: Inspect Weft.Xmla.TargetReader to find the dataset-listing method**

Run: `grep -rn "Task<.*List.*>\|ListDatabases\|ListDatasets\|CATALOG_NAME" src/Weft.Xmla/*.cs`

If `ITargetReader` or `TargetReader` already exposes a "list databases" method, reuse it. If not, you'll add one in Step 3.

- [ ] **Step 2: Write failing test**

Append to `ConnectionManagerTests.cs`:

```csharp
[Fact]
public async Task ListDatasetsAsync_returns_DatasetInfo_rows_from_reader()
{
    var reader = Substitute.For<ITargetReader>();
    reader.ListDatabasesAsync(
            Arg.Any<string>(),
            Arg.Any<AccessToken>(),
            Arg.Any<CancellationToken>())
        .Returns(new[] { "DatasetA", "DatasetB" });

    var mgr = new ConnectionManager(_ => Substitute.For<IAuthProvider>(), reader);
    var ws = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/Test");
    var token = new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1));

    var datasets = await mgr.ListDatasetsAsync(ws, token, CancellationToken.None);

    datasets.Select(d => d.Name).Should().Equal("DatasetA", "DatasetB");
}
```

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ListDatasetsAsync`
Expected: FAIL — `ITargetReader.ListDatabasesAsync` (or the equivalent you identified) doesn't exist, or `ConnectionManager.ListDatasetsAsync` doesn't exist.

- [ ] **Step 4: Add ListDatabasesAsync to ITargetReader if missing**

If the interface doesn't already have a listing method, add one. In `src/Weft.Core/Abstractions/ITargetReader.cs`:

```csharp
Task<IReadOnlyList<string>> ListDatabasesAsync(
    string serverUrl,
    AccessToken token,
    CancellationToken ct);
```

Then implement in `src/Weft.Xmla/TargetReader.cs` using ADOMD:

```csharp
public async Task<IReadOnlyList<string>> ListDatabasesAsync(
    string serverUrl, AccessToken token, CancellationToken ct)
{
    using var server = new Server();
    var cs = BuildConnectionString(serverUrl, token);
    server.Connect(cs);
    var names = new List<string>();
    foreach (Database db in server.Databases)
    {
        ct.ThrowIfCancellationRequested();
        names.Add(db.Name);
    }
    return names;
}
```

`BuildConnectionString` is likely already a private helper in `TargetReader`; if not, lift the connection-string logic from `ReadDatabaseAsync` into a reusable method.

- [ ] **Step 5: Implement ListDatasetsAsync on ConnectionManager**

Add to `ConnectionManager.cs`:

```csharp
public async Task<IReadOnlyList<DatasetInfo>> ListDatasetsAsync(
    WorkspaceReference workspace, AccessToken token, CancellationToken ct)
{
    var names = await _reader.ListDatabasesAsync(workspace.Server, token, ct);
    return names
        .Select(n => new DatasetInfo(n, SizeBytes: null, LastUpdatedUtc: null,
                                     RefreshPolicy: null, Owner: null))
        .ToList();
}
```

For v0.1.1 we only populate `Name`. Richer metadata (size/updated/owner) stays null — the grid shows "-" for empty cells.

- [ ] **Step 6: Run tests**

Run: `dotnet test studio/weft-studio.sln && dotnet test weft.sln`
Expected: all green. 50 Studio tests + existing CLI tests.

- [ ] **Step 7: Commit**

```bash
git add src/Weft.Core/Abstractions src/Weft.Xmla studio/src/WeftStudio.App studio/test/WeftStudio.App.Tests
git commit -m "feat(app/connections): ListDatasetsAsync via ITargetReader.ListDatabasesAsync"
```

---

### Task 9: ConnectionManager.FetchModelAsync

**Files:**
- Modify: `studio/src/WeftStudio.App/Connections/ConnectionManager.cs`
- Modify: `studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs`

- [ ] **Step 1: Write failing test**

Append:

```csharp
[Fact]
public async Task FetchModelAsync_returns_ReadOnly_ModelSession()
{
    var reader = Substitute.For<ITargetReader>();
    var fakeDb = OpenFixtureDatabase(); // helper that opens tiny-static.bim
    reader.ReadDatabaseAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
        .Returns(fakeDb);

    var mgr = new ConnectionManager(_ => Substitute.For<IAuthProvider>(), reader);
    var ws = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/Test");
    var ds = new DatasetInfo("Sales", null, null, null, null);
    var token = new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1));

    var session = await mgr.FetchModelAsync(ws, ds, token, CancellationToken.None);

    session.ReadOnly.Should().BeTrue();
    session.SourcePath.Should().BeNull();
    session.Database.Should().NotBeNull();
}

private static Microsoft.AnalysisServices.Tabular.Database OpenFixtureDatabase()
{
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
    return new Weft.Core.Loading.BimFileLoader().Load(path);
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter FetchModelAsync`
Expected: compile FAIL — `FetchModelAsync` missing.

- [ ] **Step 3: Implement**

Append to `ConnectionManager.cs`:

```csharp
public async Task<ModelSession> FetchModelAsync(
    WorkspaceReference workspace,
    DatasetInfo dataset,
    AccessToken token,
    CancellationToken ct)
{
    var db = await _reader.ReadDatabaseAsync(
        workspace.Server, dataset.Name, token, ct);
    return new ModelSession(db, sourcePath: null, readOnly: true);
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: +1 passing. Full suite 51 passed, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/Connections/ConnectionManager.cs studio/test/WeftStudio.App.Tests/ConnectionManagerTests.cs
git commit -m "feat(app/connections): FetchModelAsync returns read-only ModelSession from XMLA"
```

---

## Phase 2 — UI scaffolding

### Task 10: Add Avalonia.Controls.DataGrid + connect dialog skeleton

**Files:**
- Modify: `studio/src/WeftStudio.Ui/WeftStudio.Ui.csproj`
- Create: `studio/src/WeftStudio.Ui/Connect/ConnectDialogState.cs`
- Create: `studio/src/WeftStudio.Ui/Connect/DatasetRow.cs`

No tests — these are small leaf types exercised by Task 11.

- [ ] **Step 1: Add DataGrid package**

Edit `studio/src/WeftStudio.Ui/WeftStudio.Ui.csproj` — add to the existing ItemGroup with Avalonia references:

```xml
<PackageReference Include="Avalonia.Controls.DataGrid" Version="11.2.*" />
```

- [ ] **Step 2: Write the state enum**

```csharp
// studio/src/WeftStudio.Ui/Connect/ConnectDialogState.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.Ui.Connect;

public enum ConnectDialogState
{
    Idle,          // empty or invalid URL
    Ready,         // URL parses; button enabled
    SigningIn,     // MSAL token request in flight
    Fetching,      // listing datasets
    Picker,        // dataset grid visible, waiting for user selection
    Loading,       // downloading the selected model
}
```

- [ ] **Step 3: Write DatasetRow VM wrapper**

```csharp
// studio/src/WeftStudio.Ui/Connect/DatasetRow.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using ReactiveUI;
using WeftStudio.App.Connections;

namespace WeftStudio.Ui.Connect;

public sealed class DatasetRow : ReactiveObject
{
    public DatasetRow(DatasetInfo info)
    {
        Info = info;
        Name = info.Name;
        SizeDisplay = info.SizeBytes is null ? "-" : FormatSize(info.SizeBytes.Value);
        UpdatedDisplay = info.LastUpdatedUtc is null ? "-" : RelativeAge(info.LastUpdatedUtc.Value);
        RefreshPolicy = info.RefreshPolicy ?? "-";
        Owner = info.Owner ?? "-";
    }

    public DatasetInfo Info { get; }
    public string Name { get; }
    public string SizeDisplay { get; }
    public string UpdatedDisplay { get; }
    public string RefreshPolicy { get; }
    public string Owner { get; }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1024} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576} MB",
        _ => $"{bytes / 1_073_741_824} GB",
    };

    private static string RelativeAge(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours   < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays     < 7) return $"{(int)delta.TotalDays}d ago";
        return utc.ToString("yyyy-MM-dd");
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build studio/weft-studio.sln`
Expected: 0 warnings, 0 errors.

```bash
git add studio/src/WeftStudio.Ui
git commit -m "feat(ui/connect): add DataGrid ref + ConnectDialogState + DatasetRow"
```

---

### Task 11: ConnectDialogViewModel — URL validation + state

**Files:**
- Create: `studio/src/WeftStudio.Ui/Connect/ConnectDialogViewModel.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/ConnectDialogViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// studio/test/WeftStudio.Ui.Tests/ConnectDialogViewModelTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;

namespace WeftStudio.Ui.Tests;

public class ConnectDialogViewModelTests
{
    private static ConnectDialogViewModel NewVm() =>
        new(Substitute.For<ConnectionManager>(_ => null!, null!));

    [Fact]
    public void Starts_in_Idle_state()
    {
        var vm = NewVm();
        vm.State.Should().Be(ConnectDialogState.Idle);
        vm.UrlError.Should().BeNull();
    }

    [Fact]
    public void Setting_valid_Url_transitions_to_Ready()
    {
        var vm = NewVm();
        vm.Url = "powerbi://api.powerbi.com/v1.0/myorg/X";
        vm.State.Should().Be(ConnectDialogState.Ready);
        vm.UrlError.Should().BeNull();
    }

    [Fact]
    public void Setting_invalid_Url_stays_Idle_with_error()
    {
        var vm = NewVm();
        vm.Url = "nope";
        vm.State.Should().Be(ConnectDialogState.Idle);
        vm.UrlError.Should().NotBeNull();
    }

    [Fact]
    public void Clearing_Url_returns_to_Idle()
    {
        var vm = NewVm();
        vm.Url = "powerbi://api.powerbi.com/v1.0/myorg/X";
        vm.Url = "";
        vm.State.Should().Be(ConnectDialogState.Idle);
    }
}
```

Note: `Substitute.For<ConnectionManager>(...)` works only if `ConnectionManager` is non-sealed or has virtual methods. If NSubstitute rejects it, introduce an `IConnectionManager` interface with the three async methods in Task 7 and mock that. For v0.1.1 the simplest path: **add `IConnectionManager` now** as a small refactor.

Inline refactor if needed: make `ConnectionManager` implement `IConnectionManager` (same methods, interface extracted). Update Task 7-9 usages accordingly.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ConnectDialogViewModelTests`
Expected: compile FAIL.

- [ ] **Step 3: (Refactor) Extract IConnectionManager interface**

In `studio/src/WeftStudio.App/Connections/`, add:

```csharp
// IConnectionManager.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;
using Weft.Core.Abstractions;

namespace WeftStudio.App.Connections;

public interface IConnectionManager
{
    Task<AccessToken> SignInAsync(AuthOptions opts, CancellationToken ct);
    Task<IReadOnlyList<DatasetInfo>> ListDatasetsAsync(
        WorkspaceReference workspace, AccessToken token, CancellationToken ct);
    Task<ModelSession> FetchModelAsync(
        WorkspaceReference workspace, DatasetInfo dataset, AccessToken token, CancellationToken ct);
}
```

Make `ConnectionManager` implement it: `public sealed class ConnectionManager : IConnectionManager`. Update the existing `ConnectionManagerTests` to use `IConnectionManager` where appropriate (pass the concrete for now — interface-vs-concrete only matters where mocking is needed).

- [ ] **Step 4: Implement ConnectDialogViewModel**

```csharp
// studio/src/WeftStudio.Ui/Connect/ConnectDialogViewModel.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using ReactiveUI;
using WeftStudio.App.Connections;

namespace WeftStudio.Ui.Connect;

public sealed class ConnectDialogViewModel : ReactiveObject
{
    private readonly IConnectionManager _mgr;
    private string _url = "";
    private string? _urlError;
    private ConnectDialogState _state = ConnectDialogState.Idle;
    private string? _errorBanner;
    private WorkspaceReference? _parsed;

    public ConnectDialogViewModel(IConnectionManager mgr) => _mgr = mgr;

    public string Url
    {
        get => _url;
        set
        {
            this.RaiseAndSetIfChanged(ref _url, value);
            TryParseUrl();
        }
    }

    public string? UrlError
    {
        get => _urlError;
        private set => this.RaiseAndSetIfChanged(ref _urlError, value);
    }

    public ConnectDialogState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string? ErrorBanner
    {
        get => _errorBanner;
        private set => this.RaiseAndSetIfChanged(ref _errorBanner, value);
    }

    public ObservableCollection<DatasetRow> Datasets { get; } = new();

    private void TryParseUrl()
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            UrlError = null;
            _parsed = null;
            State = ConnectDialogState.Idle;
            return;
        }

        try
        {
            _parsed = WorkspaceReference.Parse(_url);
            UrlError = null;
            State = ConnectDialogState.Ready;
        }
        catch (WorkspaceUrlException ex)
        {
            _parsed = null;
            UrlError = ex.Message;
            State = ConnectDialogState.Idle;
        }
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test studio/weft-studio.sln --filter ConnectDialogViewModelTests`
Expected: 4 passed. Full suite 54 passed, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add studio/src/WeftStudio.App/Connections studio/src/WeftStudio.Ui/Connect studio/test
git commit -m "feat(ui/connect): ConnectDialogViewModel URL validation + state transitions"
```

---

### Task 12: ConnectDialogViewModel — sign-in, fetch, open

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Connect/ConnectDialogViewModel.cs`
- Modify: `studio/test/WeftStudio.Ui.Tests/ConnectDialogViewModelTests.cs`

- [ ] **Step 1: Append failing tests**

```csharp
[Fact]
public async Task SignIn_success_populates_Datasets_and_moves_to_Picker()
{
    var mgr = Substitute.For<IConnectionManager>();
    mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
        .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
    mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
        .Returns(new[] { new DatasetInfo("DS1", null, null, null, null),
                         new DatasetInfo("DS2", null, null, null, null) });

    var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
    vm.ClientId = "some-client-id";

    await vm.SignInAsync();

    vm.State.Should().Be(ConnectDialogState.Picker);
    vm.Datasets.Should().HaveCount(2);
    vm.ErrorBanner.Should().BeNull();
}

[Fact]
public async Task SignIn_failure_sets_ErrorBanner_and_returns_to_Ready()
{
    var mgr = Substitute.For<IConnectionManager>();
    mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
        .Returns<Task<AccessToken>>(_ => throw new InvalidOperationException("AADSTS50020 no user"));

    var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
    vm.ClientId = "some-client-id";

    await vm.SignInAsync();

    vm.ErrorBanner.Should().Contain("AADSTS50020");
    vm.State.Should().Be(ConnectDialogState.Ready);
}

[Fact]
public async Task Open_selected_returns_ReadOnly_session()
{
    var mgr = Substitute.For<IConnectionManager>();
    mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
        .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
    mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
        .Returns(new[] { new DatasetInfo("DS1", null, null, null, null) });

    var fakeSession = ModelSession.OpenBim(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
    // Swap to a read-only mirror for the assertion:
    var readOnly = new ModelSession(fakeSession.Database, null, readOnly: true);
    mgr.FetchModelAsync(Arg.Any<WorkspaceReference>(), Arg.Any<DatasetInfo>(),
                       Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
        .Returns(readOnly);

    var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
    vm.ClientId = "some-client-id";
    await vm.SignInAsync();
    vm.SelectedRow = vm.Datasets[0];

    var result = await vm.OpenAsync();

    result.Should().NotBeNull();
    result!.ReadOnly.Should().BeTrue();
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ConnectDialogViewModelTests`
Expected: compile FAIL — `ClientId`, `SignInAsync`, `SelectedRow`, `OpenAsync` missing.

- [ ] **Step 3: Extend ConnectDialogViewModel**

Add to the class:

```csharp
// (inside ConnectDialogViewModel)

public string ClientId { get; set; } = "";
public AuthMode AuthMode { get; set; } = AuthMode.Interactive;
public DatasetRow? SelectedRow
{
    get => _selectedRow;
    set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
}
private DatasetRow? _selectedRow;

private AccessToken? _token;

public async Task SignInAsync()
{
    if (_parsed is null || string.IsNullOrWhiteSpace(ClientId)) return;

    ErrorBanner = null;
    State = ConnectDialogState.SigningIn;

    var opts = new AuthOptions(
        Mode: AuthMode,
        TenantId: "",           // /common authority — MSAL auto-detects
        ClientId: ClientId,
        CertThumbprint: null, CertStoreLocation: null, CertStoreName: null,
        CertFilePath: null, CertFilePassword: null, ClientSecret: null);

    try
    {
        _token = await _mgr.SignInAsync(opts, CancellationToken.None);

        State = ConnectDialogState.Fetching;
        var datasets = await _mgr.ListDatasetsAsync(_parsed, _token, CancellationToken.None);

        Datasets.Clear();
        foreach (var d in datasets) Datasets.Add(new DatasetRow(d));

        State = ConnectDialogState.Picker;
    }
    catch (Exception ex)
    {
        ErrorBanner = ex.Message;
        State = ConnectDialogState.Ready;
        _token = null;
    }
}

public async Task<ModelSession?> OpenAsync()
{
    if (_parsed is null || _token is null || SelectedRow is null) return null;

    State = ConnectDialogState.Loading;
    try
    {
        return await _mgr.FetchModelAsync(_parsed, SelectedRow.Info, _token, CancellationToken.None);
    }
    catch (Exception ex)
    {
        ErrorBanner = ex.Message;
        State = ConnectDialogState.Picker;
        return null;
    }
}
```

Ensure `using Weft.Auth;` is present at the top.

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: all tests pass. Full suite 57 passed.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.Ui/Connect studio/test/WeftStudio.Ui.Tests
git commit -m "feat(ui/connect): SignInAsync + OpenAsync on ConnectDialogViewModel"
```

---

### Task 13: Dataset filter by name

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Connect/ConnectDialogViewModel.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/DatasetFilterTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// studio/test/WeftStudio.Ui.Tests/DatasetFilterTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;

namespace WeftStudio.Ui.Tests;

public class DatasetFilterTests
{
    private static ConnectDialogViewModel VmWithDatasets(params string[] names)
    {
        var mgr = Substitute.For<IConnectionManager>();
        var vm = new ConnectDialogViewModel(mgr);
        foreach (var n in names)
            vm.Datasets.Add(new DatasetRow(new DatasetInfo(n, null, null, null, null)));
        return vm;
    }

    [Fact]
    public void Empty_filter_shows_all()
    {
        var vm = VmWithDatasets("alpha", "beta", "gamma");
        vm.FilterText = "";
        vm.VisibleDatasets.Should().HaveCount(3);
    }

    [Fact]
    public void Filter_is_substring_case_insensitive()
    {
        var vm = VmWithDatasets("Sales_Fact", "Inventory", "sales_targets");
        vm.FilterText = "sales";
        vm.VisibleDatasets.Select(d => d.Name)
            .Should().Equal("Sales_Fact", "sales_targets");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter DatasetFilterTests`
Expected: compile FAIL — `FilterText` and `VisibleDatasets` missing.

- [ ] **Step 3: Extend ConnectDialogViewModel**

```csharp
private string _filterText = "";
public string FilterText
{
    get => _filterText;
    set
    {
        this.RaiseAndSetIfChanged(ref _filterText, value);
        RefreshVisibleDatasets();
    }
}

public ObservableCollection<DatasetRow> VisibleDatasets { get; } = new();

private void RefreshVisibleDatasets()
{
    VisibleDatasets.Clear();
    var filter = _filterText;
    foreach (var row in Datasets)
    {
        if (string.IsNullOrWhiteSpace(filter) ||
            row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            VisibleDatasets.Add(row);
        }
    }
}
```

Also call `RefreshVisibleDatasets()` at the end of the `SignInAsync` method (after populating `Datasets`) so the initial list appears in `VisibleDatasets`.

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: +2 passing. Full suite 59 passed.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.Ui/Connect studio/test/WeftStudio.Ui.Tests/DatasetFilterTests.cs
git commit -m "feat(ui/connect): name-only filter produces VisibleDatasets collection"
```

---

## Phase 3 — Shell integration

### Task 14: ShellViewModel.IsReadOnly disables SaveCommand

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/ShellViewModelReadOnlyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// studio/test/WeftStudio.Ui.Tests/ShellViewModelReadOnlyTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class ShellViewModelReadOnlyTests
{
    [Fact]
    public async Task Workspace_session_disables_SaveCommand()
    {
        var s = ModelSession.OpenBim(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var readOnlySession = new ModelSession(s.Database, sourcePath: null, readOnly: true);

        var vm = new ShellViewModel();
        vm.AdoptSession(readOnlySession);

        vm.IsReadOnly.Should().BeTrue();
        (await vm.SaveCommand.CanExecute.FirstAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Bim_session_keeps_SaveCommand_enabled()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
        var vm = new ShellViewModel();
        vm.OpenModel(fixture);

        vm.IsReadOnly.Should().BeFalse();
        (await vm.SaveCommand.CanExecute.FirstAsync()).Should().BeTrue();
    }

    [Fact]
    public void WorkspaceLabel_reflects_session_source()
    {
        var s = ModelSession.OpenBim(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var readOnlySession = new ModelSession(s.Database, sourcePath: null, readOnly: true);

        var vm = new ShellViewModel();
        vm.AdoptSession(readOnlySession, workspaceLabel: "DEV / SalesDS");

        vm.WorkspaceLabel.Should().Be("DEV / SalesDS");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test studio/weft-studio.sln --filter ShellViewModelReadOnlyTests`
Expected: compile FAIL — `IsReadOnly`, `WorkspaceLabel`, `AdoptSession` missing.

- [ ] **Step 3: Extend ShellViewModel**

Inside `ShellViewModel`, add properties and method:

```csharp
private bool _isReadOnly;
public bool IsReadOnly
{
    get => _isReadOnly;
    private set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
}

private string? _workspaceLabel;
public string? WorkspaceLabel
{
    get => _workspaceLabel;
    private set => this.RaiseAndSetIfChanged(ref _workspaceLabel, value);
}

/// <summary>
/// Installs a pre-built ModelSession into the shell (used by the
/// Connect-to-workspace flow). OpenModel still handles the .bim path.
/// </summary>
public void AdoptSession(ModelSession session, string? workspaceLabel = null)
{
    Explorer = new ExplorerViewModel(session);
    IsReadOnly = session.ReadOnly;
    WorkspaceLabel = workspaceLabel;
}
```

Update the `canSave` observable in the ctor:

```csharp
var canSave = this.WhenAnyValue(x => x.Explorer, x => x.IsReadOnly,
    (exp, ro) => exp is not null && !ro);
```

Also clear `IsReadOnly` / `WorkspaceLabel` inside `OpenModel` so reopening a `.bim` after a workspace session resets state:

```csharp
public void OpenModel(string bimPath)
{
    var session = ModelSession.OpenBim(bimPath);
    Explorer = new ExplorerViewModel(session);
    IsReadOnly = false;
    WorkspaceLabel = null;

    // (existing recent-files logic unchanged)
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test studio/weft-studio.sln`
Expected: 3 new passing. Full suite 62 passed.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.Ui/Shell studio/test/WeftStudio.Ui.Tests/ShellViewModelReadOnlyTests.cs
git commit -m "feat(ui/shell): IsReadOnly + WorkspaceLabel + AdoptSession for workspace models"
```

---

### Task 15: ConnectDialog.axaml + wire ShellViewModel.OpenWorkspaceCommand

**Files:**
- Create: `studio/src/WeftStudio.Ui/Connect/ConnectDialog.axaml`
- Create: `studio/src/WeftStudio.Ui/Connect/ConnectDialog.axaml.cs`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` (File menu)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml.cs` (OnConnectToWorkspace)

- [ ] **Step 1: Write ConnectDialog XAML**

```xml
<!-- studio/src/WeftStudio.Ui/Connect/ConnectDialog.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:connect="using:WeftStudio.Ui.Connect"
        x:Class="WeftStudio.Ui.Connect.ConnectDialog"
        x:DataType="connect:ConnectDialogViewModel"
        Title="Connect to workspace"
        Width="820" Height="540"
        WindowStartupLocation="CenterOwner"
        CanResize="False">
  <Grid RowDefinitions="Auto,Auto,Auto,Auto,*,Auto" Margin="18">
    <TextBlock Grid.Row="0" Text="Connect to a workspace" FontWeight="Bold" FontSize="16"/>
    <TextBlock Grid.Row="1" FontSize="12" Margin="0,4,0,12"
               Text="Open a semantic model read-only from a Power BI / Fabric workspace."/>

    <StackPanel Grid.Row="2" Orientation="Vertical" Spacing="4">
      <TextBlock Text="XMLA endpoint URL"/>
      <TextBox Text="{Binding Url}" FontFamily="Cascadia Mono,Menlo,monospace"
               Watermark="powerbi://api.powerbi.com/v1.0/myorg/&lt;workspace&gt;"/>
      <TextBlock Text="{Binding UrlError}" Foreground="#b33" FontSize="11"
                 IsVisible="{Binding UrlError, Converter={x:Static connect:NotNullConverter.Instance}}"/>
    </StackPanel>

    <StackPanel Grid.Row="3" Orientation="Horizontal" Spacing="16" Margin="0,10,0,10">
      <TextBlock Text="Sign-in method" VerticalAlignment="Center"/>
      <RadioButton Content="Interactive" GroupName="auth" IsChecked="True"/>
      <RadioButton Content="Device code" GroupName="auth"/>
    </StackPanel>

    <Grid Grid.Row="4" RowDefinitions="Auto,*">
      <TextBox Grid.Row="0" Text="{Binding FilterText}"
               Watermark="🔎  Filter datasets by name…"
               Margin="0,0,0,6"
               IsVisible="{Binding State, Converter={x:Static connect:StateIsPickerConverter.Instance}}"/>
      <DataGrid Grid.Row="1" ItemsSource="{Binding VisibleDatasets}"
                SelectedItem="{Binding SelectedRow}"
                IsReadOnly="True" CanUserSortColumns="True"
                IsVisible="{Binding State, Converter={x:Static connect:StateIsPickerConverter.Instance}}">
        <DataGrid.Columns>
          <DataGridTextColumn Header="Name"           Binding="{Binding Name}"           Width="*"/>
          <DataGridTextColumn Header="Size"           Binding="{Binding SizeDisplay}"    Width="80"/>
          <DataGridTextColumn Header="Updated"        Binding="{Binding UpdatedDisplay}" Width="110"/>
          <DataGridTextColumn Header="Refresh policy" Binding="{Binding RefreshPolicy}"  Width="130"/>
          <DataGridTextColumn Header="Owner"          Binding="{Binding Owner}"          Width="130"/>
        </DataGrid.Columns>
      </DataGrid>
      <TextBlock Grid.Row="1" Text="{Binding ErrorBanner}"
                 Foreground="#b33" TextWrapping="Wrap"
                 IsVisible="{Binding ErrorBanner, Converter={x:Static connect:NotNullConverter.Instance}}"/>
    </Grid>

    <StackPanel Grid.Row="5" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8" Margin="0,12,0,0">
      <Button Content="Cancel" Click="OnCancel"/>
      <Button Content="Sign in &amp; list datasets" Click="OnSignIn"
              IsVisible="{Binding State, Converter={x:Static connect:StateNotPickerConverter.Instance}}"/>
      <Button Content="Open read-only" Click="OnOpen" Background="#2e6b2e" Foreground="White"
              IsEnabled="{Binding SelectedRow, Converter={x:Static connect:NotNullConverter.Instance}}"
              IsVisible="{Binding State, Converter={x:Static connect:StateIsPickerConverter.Instance}}"/>
    </StackPanel>
  </Grid>
</Window>
```

Add a value-converter helper file:

```csharp
// studio/src/WeftStudio.Ui/Connect/ConnectDialogConverters.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Avalonia.Data.Converters;

namespace WeftStudio.Ui.Connect;

public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c) => v is not null;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StateIsPickerConverter : IValueConverter
{
    public static readonly StateIsPickerConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is ConnectDialogState s && s == ConnectDialogState.Picker;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StateNotPickerConverter : IValueConverter
{
    public static readonly StateNotPickerConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is ConnectDialogState s && s != ConnectDialogState.Picker;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

- [ ] **Step 2: Add ConnectDialog code-behind**

```csharp
// studio/src/WeftStudio.Ui/Connect/ConnectDialog.axaml.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using WeftStudio.App;

namespace WeftStudio.Ui.Connect;

public partial class ConnectDialog : Window
{
    public ConnectDialog() => InitializeComponent();

    public ModelSession? Result { get; private set; }
    public string? WorkspaceLabel { get; private set; }

    private async void OnSignIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectDialogViewModel vm) await vm.SignInAsync();
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectDialogViewModel vm) return;
        var session = await vm.OpenAsync();
        if (session is not null)
        {
            Result = session;
            WorkspaceLabel = $"{vm.SelectedRow?.Name}";
            Close();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 3: Update ShellWindow.axaml — File menu + banner**

Append to the `<MenuItem Header="_File">`:

```xml
<MenuItem Header="_Connect to workspace…" Click="OnConnectToWorkspace"
          InputGesture="Ctrl+Shift+O"/>
```

Place it between **Open…** and the Separator. Also wrap the Grid content in a DockPanel if it isn't already, and add the read-only banner **above** the main Grid:

```xml
<Border DockPanel.Dock="Top" Background="#ffe6c8"
        BorderBrush="#c36012" BorderThickness="0,0,0,2"
        Padding="14,8"
        IsVisible="{Binding IsReadOnly}">
  <TextBlock Foreground="#5a2d08" FontSize="13">
    <Run Text="◉ READ-ONLY snapshot of "/>
    <Run Text="{Binding WorkspaceLabel}" FontStyle="Italic"/>
    <Run Text=" · File &gt; Save disabled, use File &gt; Save As .bim to keep a local copy"/>
  </TextBlock>
</Border>
```

- [ ] **Step 4: Update ShellWindow.axaml.cs — OnConnectToWorkspace**

Add handler (add `using WeftStudio.Ui.Connect;`):

```csharp
private async void OnConnectToWorkspace(object? sender, RoutedEventArgs e)
{
    if (DataContext is not ShellViewModel vm) return;

    var reader = new Weft.Xmla.TargetReader();
    var mgr = new WeftStudio.App.Connections.ConnectionManager(
        authProviderFactory: opts => Weft.Auth.AuthProviderFactory.Create(opts),
        reader: reader);

    var dialogVm = new ConnectDialogViewModel(mgr)
    {
        ClientId = WeftStudio.App.AppSettings.ClientIdProvider.ResolveFromEnvironment(
            commandLineArg: null,
            userOverride: null,
            baked: ""),
    };

    var dialog = new ConnectDialog { DataContext = dialogVm };
    await dialog.ShowDialog(this);

    if (dialog.Result is not null)
        vm.AdoptSession(dialog.Result, workspaceLabel: dialog.WorkspaceLabel);
}
```

Note: `AuthProviderFactory` in the CLI is a static factory. Confirm its actual signature (`Create(AuthOptions)` → `IAuthProvider` is likely). Adjust if needed.

- [ ] **Step 5: Build and run full suite**

Run: `dotnet build studio/weft-studio.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test studio/weft-studio.sln`
Expected: 62 tests still pass.

- [ ] **Step 6: Manual smoke check**

```bash
dotnet run --project studio/src/WeftStudio.Ui
```

Click **File → Connect to workspace…** — the dialog should open (empty URL, Sign-in button disabled because empty ClientId). Close with Cancel.

- [ ] **Step 7: Commit**

```bash
git add studio/src/WeftStudio.Ui
git commit -m "feat(ui): ConnectDialog + File → Connect to workspace wiring + read-only banner"
```

---

### Task 16: Save As .bim + Reload from workspace menu items

**Files:**
- Modify: `studio/src/WeftStudio.App/Persistence/BimSaver.cs` (add `SaveAs`)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs` (add SaveAsCommand, ReloadFromWorkspaceCommand)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` + `.cs`

- [ ] **Step 1: Add BimSaver.SaveAs**

In `BimSaver.cs`:

```csharp
public static void SaveAs(ModelSession session, string path)
{
    var json = JsonSerializer.SerializeDatabase(session.Database,
        new SerializeOptions {
            IgnoreInferredObjects = true,
            IgnoreInferredProperties = true,
            IgnoreTimestamps = true
        });
    File.WriteAllText(path, json);
    // Workspace-origin sessions can also be saved — ReadOnly only gates the in-place Save.
}
```

- [ ] **Step 2: Add commands on ShellViewModel**

```csharp
public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
public ReactiveCommand<Unit, Unit> ReloadFromWorkspaceCommand { get; }

public event EventHandler? SaveAsRequested;
public event EventHandler? ReloadRequested;

// In ctor, after SaveCommand:
var canSaveAs = this.WhenAnyValue(x => x.Explorer).Select(e => e is not null);
SaveAsCommand = ReactiveCommand.Create(() =>
    SaveAsRequested?.Invoke(this, EventArgs.Empty), canSaveAs);

var canReload = this.WhenAnyValue(x => x.IsReadOnly);
ReloadFromWorkspaceCommand = ReactiveCommand.Create(() =>
    ReloadRequested?.Invoke(this, EventArgs.Empty), canReload);
```

- [ ] **Step 3: Add menu items and handlers**

In `ShellWindow.axaml` File menu:

```xml
<MenuItem Header="Save _As .bim…" Command="{Binding SaveAsCommand}"/>
<MenuItem Header="Re_load from workspace" Command="{Binding ReloadFromWorkspaceCommand}"/>
```

Wire the events in `ShellWindow.axaml.cs` constructor (append to existing):

```csharp
Loaded += (_, _) =>
{
    if (DataContext is ShellViewModel vm)
    {
        vm.SaveAsRequested += async (_, _) => await OnSaveAs();
        vm.ReloadRequested += async (_, _) => await OnReload();
    }
};

private async Task OnSaveAs()
{
    if (DataContext is not ShellViewModel vm || vm.Explorer is null) return;

    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
        Title = "Save model as .bim",
        DefaultExtension = "bim",
        FileTypeChoices = new[]
        {
            new FilePickerFileType("Power BI model") { Patterns = new[] { "*.bim" } }
        }
    });
    var path = file?.TryGetLocalPath();
    if (path is not null)
        WeftStudio.App.Persistence.BimSaver.SaveAs(vm.Explorer.Session, path);
}

private async Task OnReload()
{
    // v0.1.1: simplest reload — re-open the Connect dialog pre-filled.
    // Full re-fetch with persisted workspace state is deferred to a later iteration.
    if (DataContext is ShellViewModel vm && vm.IsReadOnly)
        OnConnectToWorkspace(this, new RoutedEventArgs());
    await Task.CompletedTask;
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet build studio/weft-studio.sln && dotnet test studio/weft-studio.sln`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App/Persistence studio/src/WeftStudio.Ui
git commit -m "feat(ui/shell): Save As .bim + Reload from workspace menu items"
```

---

### Task 17: Recent workspaces dropdown

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Connect/ConnectDialogViewModel.cs`
- Modify: `studio/src/WeftStudio.Ui/Connect/ConnectDialog.axaml`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml.cs`

- [ ] **Step 1: Extend ConnectDialogViewModel with a recent-picks list**

```csharp
public List<string> RecentUrls { get; set; } = new();
```

After a successful `OpenAsync`, the caller updates settings — not the dialog.

- [ ] **Step 2: Update ConnectDialog.axaml to use AutoCompleteBox**

Replace the single `TextBox` for URL with:

```xml
<AutoCompleteBox Text="{Binding Url, Mode=TwoWay}"
                 ItemsSource="{Binding RecentUrls}"
                 FontFamily="Cascadia Mono,Menlo,monospace"
                 Watermark="powerbi://api.powerbi.com/v1.0/myorg/&lt;workspace&gt;"
                 FilterMode="Contains"/>
```

- [ ] **Step 3: In OnConnectToWorkspace handler, load recents and save on success**

```csharp
// Before showing the dialog:
var store = new WeftStudio.App.Settings.SettingsStore(
    WeftStudio.App.Settings.SettingsStore.DefaultDirectory);
var settings = store.Load();
dialogVm.RecentUrls = settings.RecentWorkspaces
    .OrderByDescending(w => w.LastUsedUtc)
    .Select(w => w.WorkspaceUrl)
    .Distinct()
    .Take(10)
    .ToList();

// After the dialog returns with a Result:
if (dialog.Result is not null)
{
    vm.AdoptSession(dialog.Result, workspaceLabel: dialog.WorkspaceLabel);

    var updated = store.Load();
    updated.RecentWorkspaces.RemoveAll(w => w.WorkspaceUrl == dialogVm.Url);
    updated.RecentWorkspaces.Insert(0, new WeftStudio.App.Settings.RecentWorkspace(
        WorkspaceUrl: dialogVm.Url,
        LastDatasetName: dialog.WorkspaceLabel ?? "",
        AuthMode: dialogVm.AuthMode.ToString(),
        LastUsedUtc: DateTime.UtcNow));
    if (updated.RecentWorkspaces.Count > 10)
        updated.RecentWorkspaces.RemoveRange(10, updated.RecentWorkspaces.Count - 10);
    store.Save(updated);
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet test studio/weft-studio.sln`
Expected: all green (no new test — this is visual glue).

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.Ui
git commit -m "feat(ui/connect): recent workspaces dropdown + persistence in settings.json"
```

---

## Phase 4 — End-to-end smoke + tag

### Task 18: End-to-end smoke test

**Files:**
- Create: `studio/test/WeftStudio.Ui.Tests/WorkspaceOpenSmokeTests.cs`

- [ ] **Step 1: Write the smoke test**

```csharp
// studio/test/WeftStudio.Ui.Tests/WorkspaceOpenSmokeTests.cs
// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class WorkspaceOpenSmokeTests
{
    [Fact]
    public async Task Connect_dialog_VM_produces_ReadOnly_session_that_shell_adopts()
    {
        // Arrange: fake connection manager.
        var fakeDb = new Weft.Core.Loading.BimFileLoader().Load(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var mgr = Substitute.For<IConnectionManager>();
        mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
        mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DatasetInfo("Sales", null, null, null, null) });
        mgr.FetchModelAsync(Arg.Any<WorkspaceReference>(), Arg.Any<DatasetInfo>(),
                           Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new ModelSession(fakeDb, sourcePath: null, readOnly: true));

        // Act: drive the VM through its full state machine.
        var vm = new ConnectDialogViewModel(mgr)
        {
            Url = "powerbi://api.powerbi.com/v1.0/myorg/DEV",
            ClientId = "some-client-id",
        };
        await vm.SignInAsync();
        vm.SelectedRow = vm.VisibleDatasets[0];
        var session = await vm.OpenAsync();

        // Assert: session is read-only.
        session.Should().NotBeNull();
        session!.ReadOnly.Should().BeTrue();

        // Shell adopts it and Save is disabled.
        var shell = new ShellViewModel();
        shell.AdoptSession(session, workspaceLabel: "DEV / Sales");
        shell.IsReadOnly.Should().BeTrue();
        shell.WorkspaceLabel.Should().Be("DEV / Sales");
        (await shell.SaveCommand.CanExecute.FirstAsync()).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test studio/weft-studio.sln`
Expected: 63 tests pass, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add studio/test/WeftStudio.Ui.Tests/WorkspaceOpenSmokeTests.cs
git commit -m "test(ui): end-to-end workspace-open smoke — VM → ModelSession → shell adopt"
```

---

### Task 19: Tag weft-studio-v0.1.1

- [ ] **Step 1: Verify clean tree + full CI passes locally**

```bash
git status                                     # expect clean
dotnet build weft.sln         -c Release -warnaserror
dotnet build studio/weft-studio.sln -c Release -warnaserror
dotnet test  weft.sln         -c Release
dotnet test  studio/weft-studio.sln -c Release
```

Expected: all green on both solutions.

- [ ] **Step 2: Tag and push**

```bash
git tag weft-studio-v0.1.1 -m "Weft Studio v0.1.1 — open from workspace (read-only snapshot)"
git push origin master
git push origin weft-studio-v0.1.1
```

- [ ] **Step 3: Confirm GitHub Actions goes green**

Visit https://github.com/movmarcos/weft/actions — both `CI` and `Studio CI` should pass.

---

## Total

19 tasks across 4 phases. Each task commits independently so intermediate state is always reviewable.

**Commit summary (approximate):**
- Phase 1 (Tasks 1-9): 9 commits — App-layer foundation + Weft.Auth tenant change.
- Phase 2 (Tasks 10-13): 4 commits — UI scaffolding, ViewModel, filter.
- Phase 3 (Tasks 14-17): 4 commits — Shell integration, dialog wiring, persistence.
- Phase 4 (Tasks 18-19): 2 commits + tag.

**Expected test totals:** 31 (v0.1.0 baseline) + 32 new = **63 tests** after v0.1.1.

**Explicitly deferred to v0.1.2:**
- Baked default ClientId (needs AAD app registration).
- Dataset metadata beyond `Name` (size / updated / owner / refresh policy rely on DMVs or REST APIs that may or may not be straightforward — start null in v0.1.1, enrich later).
- Reload-from-workspace with silent re-auth (v0.1.1 just re-opens the dialog pre-filled).
- Cert/Secret auth in the GUI.
- Multi-tenant workspace switching in a single session.
- Auto-update / in-app updater.
- Concurrent-edit detection between snapshot time and Save As.
