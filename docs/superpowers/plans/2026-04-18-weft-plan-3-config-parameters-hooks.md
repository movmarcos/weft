# Weft Plan 3 — Config + Parameters + Hooks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Plan 2's flag-driven CLI with a YAML-configured one (`weft.yaml`) that supports environment profiles, per-env model parameter injection, pre/post-deploy hooks, deliberate bookmark overrides, and the history-loss pre-flight gate. End-state: `weft deploy --config weft.yaml --target prod` performs an end-to-end deploy with parameters resolved from the profile, history-loss checked before execution, and hooks fired at pipeline boundaries.

**Architecture:** A new `Weft.Config` project holds `WeftConfig` data types, the YAML loader (YamlDotNet), env-var expansion, and profile merging. `Weft.Core` gets `Parameters/` (auto-discovery + resolution + validation) and `Hooks/` (shell-out runner) folders. `Weft.Cli` adds `--config` / `--target` options, threads a single `ResolvedProfile` through every command, wires hook points into `DeployCommand`, and finally wires `IConsoleWriter` through all output (previously dead code). `Weft.Xmla` gets a `BookmarkClearer` and an extended `RetentionCalculator` (Year → Quarter → Month).

**Tech Stack:** .NET 10, YamlDotNet 16.x (YAML parsing), NJsonSchema 11.x (JSON Schema for editor validation), existing TOM + MSAL + System.CommandLine + Spectre.Console from Plans 1–2.

**Reference spec:** `docs/superpowers/specs/2026-04-17-weft-design.md`. Sections this plan implements:
- §5.7 ConfigLoader, §5.8 ParameterResolver, §5.9 HookRunner
- §6 step 6 history-loss pre-flight gate
- §7 Parameter Management (priority order, validation, strictMode)
- §7A.4 incremental-refresh config (`applyOnFirstDeploy`, `applyOnPolicyChange`, `bookmarkMode`, `dynamicPartitionStrategy`)
- §7A.7 `--reset-bookmarks` wiring
- §7A.8 `allowHistoryLoss` profile pin
- §8 Full config schema (`weft.yaml`)
- §9.2 safety mechanisms 11–13 (integrity invariant, history-loss gate, manifests)
- §9.3 observability (`--log-format`, redacted sensitive values)

**Reference plans:** Plan 2's final review captured three specific hand-offs that this plan addresses:
- `IConsoleWriter` + `--log-format` wiring (Plan 2 final review, Important #1).
- Token re-acquisition on long deploys (Plan 2 final review, Important #2).
- Single shared executor inside `DeployCommand.Build` (Plan 2 final review, Minor #9).

**Out of this plan (deferred):**
- TeamCity / Octopus / GitHub Actions wiring — Plan 4.
- `samples/` directory, README, CONTRIBUTING, docs/ walkthroughs — Plan 4.
- Phase-pipeline refactor of DeployCommand (hook points live inline as static calls; a pipeline abstraction is Plan 4 if/when needed).

**Plan 2 carry-over:** `_ = resetBookmarks;` in `DeployCommand.cs` becomes real wiring in Task 15/16 of this plan; the comment can be removed then.

---

## File structure (locked in by this plan)

```
weft/
├── src/
│   ├── Weft.Config/                                     # NEW project
│   │   ├── Weft.Config.csproj
│   │   ├── WeftConfig.cs
│   │   ├── ProfileConfig.cs
│   │   ├── AuthConfigSection.cs
│   │   ├── RefreshConfigSection.cs
│   │   ├── ParameterDeclaration.cs
│   │   ├── HooksConfigSection.cs
│   │   ├── YamlConfigLoader.cs
│   │   ├── EnvVarExpander.cs
│   │   ├── ProfileMerger.cs
│   │   └── WeftConfigValidationException.cs
│   │
│   ├── Weft.Core/
│   │   ├── Parameters/                                  # NEW folder
│   │   │   ├── MParameterDiscoverer.cs
│   │   │   ├── ParameterResolution.cs                   # record: name → value + source
│   │   │   ├── ParameterValueSource.cs                  # enum: Cli|ParamsFile|EnvVar|ProfileYaml|ModelDefault
│   │   │   ├── ParameterValueCoercer.cs
│   │   │   ├── ParameterResolver.cs
│   │   │   └── ParameterApplicationException.cs
│   │   └── Hooks/                                       # NEW folder
│   │       ├── HookPhase.cs                             # enum
│   │       ├── HookDefinition.cs                        # record: phase → command string
│   │       ├── HookRunner.cs
│   │       └── HookContext.cs                           # record: ChangeSet snapshot + env vars
│   │
│   ├── Weft.Xmla/
│   │   ├── BookmarkClearer.cs                           # NEW: emit TMSL clearing annotations
│   │   └── RetentionCalculator.cs                       # MODIFIED: add Quarter/Month support
│   │
│   └── Weft.Cli/
│       ├── Commands/
│       │   ├── DeployCommand.cs                         # HEAVY MODIFICATIONS
│       │   ├── PlanCommand.cs                           # add --config/--target wiring
│       │   ├── RefreshCommand.cs                        # same
│       │   ├── RestoreHistoryCommand.cs                 # same
│       │   ├── ValidateCommand.cs                       # can now also validate weft.yaml
│       │   └── InspectCommand.cs                        # no change
│       ├── Options/
│       │   ├── ConfigFileOption.cs                      # NEW
│       │   └── ProfileResolver.cs                       # HEAVY MODIFICATIONS: produces ResolvedProfile from WeftConfig + flags
│       ├── Output/                                      # existing files WIRED into commands
│       └── Auth/
│           └── TokenManager.cs                          # NEW: caches + refreshes AccessToken on elapsed time
│
├── schemas/                                             # NEW directory
│   └── weft.schema.json                                 # JSON Schema for weft.yaml
│
└── test/
    ├── Weft.Config.Tests/                               # NEW project
    │   ├── Weft.Config.Tests.csproj
    │   ├── YamlConfigLoaderTests.cs
    │   ├── EnvVarExpanderTests.cs
    │   ├── ProfileMergerTests.cs
    │   ├── WeftConfigValidationTests.cs
    │   └── fixtures/
    │       └── weft.yaml                                 # sample config
    │
    ├── Weft.Core.Tests/
    │   ├── Parameters/                                  # NEW
    │   │   ├── MParameterDiscovererTests.cs
    │   │   ├── ParameterResolverTests.cs
    │   │   └── ParameterValueCoercerTests.cs
    │   └── Hooks/                                       # NEW
    │       └── HookRunnerTests.cs
    │
    ├── Weft.Xmla.Tests/
    │   ├── BookmarkClearerTests.cs                      # NEW
    │   └── RetentionCalculatorQuarterMonthTests.cs      # NEW
    │
    └── Weft.Cli.Tests/
        ├── DeployCommandTests.cs                        # NEW tests appended (config-driven)
        ├── TokenManagerTests.cs                         # NEW
        └── fixtures/
            └── weft-e2e.yaml                            # minimal profile for unit-test deploy
```

---

## Tasks

### Task 1: Create `Weft.Config` project + `Weft.Config.Tests`

**Files:**
- Create: `src/Weft.Config/Weft.Config.csproj`
- Create: `test/Weft.Config.Tests/Weft.Config.Tests.csproj`

- [ ] **Step 1: New library + test project**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
mkdir -p src/Weft.Config test/Weft.Config.Tests

cd src/Weft.Config
dotnet new classlib -o . --force
rm -f Class1.cs

cd ../../test/Weft.Config.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

- [ ] **Step 2: Edit csproj files**

Replace `src/Weft.Config/Weft.Config.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="16.3.0" />
    <ProjectReference Include="..\Weft.Core\Weft.Core.csproj" />
  </ItemGroup>
</Project>
```

Replace `test/Weft.Config.Tests/Weft.Config.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <ItemGroup>
      <Using Include="Xunit" />
    </ItemGroup>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Weft.Config\Weft.Config.csproj" />
    <None Update="fixtures\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

(If YamlDotNet 16.3.0 does not exist at build time, use the latest 16.x on NuGet and record the version in the commit message.)

- [ ] **Step 3: Add both to solution + build**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet sln add src/Weft.Config/Weft.Config.csproj test/Weft.Config.Tests/Weft.Config.Tests.csproj
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Weft.Config/ test/Weft.Config.Tests/ weft.sln
git commit -m "feat(config): add Weft.Config class library (YamlDotNet) + test project"
```

---

### Task 2: `WeftConfig` data model

**Files:**
- Create: `src/Weft.Config/WeftConfig.cs`
- Create: `src/Weft.Config/ProfileConfig.cs`
- Create: `src/Weft.Config/AuthConfigSection.cs`
- Create: `src/Weft.Config/RefreshConfigSection.cs`
- Create: `src/Weft.Config/ParameterDeclaration.cs`
- Create: `src/Weft.Config/HooksConfigSection.cs`
- Create: `src/Weft.Config/WeftConfigValidationException.cs`

Pure data classes. No logic. Every file has the standard header:
`// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.`
`// Licensed under the MIT License.`

- [ ] **Step 1: `WeftConfig.cs` (root record)**

```csharp
namespace Weft.Config;

public sealed record WeftConfig(
    int Version,
    SourceConfigSection? Source,
    DefaultsConfigSection? Defaults,
    IReadOnlyDictionary<string, ProfileConfig> Profiles,
    IReadOnlyList<ParameterDeclaration> Parameters,
    IReadOnlyDictionary<string, ProfileOverridesSection> Overrides,
    HooksConfigSection? Hooks);

public sealed record SourceConfigSection(string Format, string Path);

public sealed record DefaultsConfigSection(
    RefreshConfigSection? Refresh,
    bool AllowDrops,
    bool AllowHistoryLoss,
    int TimeoutMinutes);

public sealed record ProfileOverridesSection(
    IReadOnlyDictionary<string, DataSourceOverride>? DataSources);

public sealed record DataSourceOverride(string Server, string Database);
```

- [ ] **Step 2: `ProfileConfig.cs`**

```csharp
namespace Weft.Config;

public sealed record ProfileConfig(
    string Workspace,
    string Database,
    AuthConfigSection Auth,
    RefreshConfigSection? Refresh,
    bool? AllowDrops,
    bool? AllowHistoryLoss,
    IReadOnlyDictionary<string, object?> Parameters);
```

- [ ] **Step 3: `AuthConfigSection.cs`**

```csharp
namespace Weft.Config;

public sealed record AuthConfigSection(
    string Mode,           // matches AuthMode enum name
    string TenantId,
    string ClientId,
    string? ClientSecret,
    string? CertPath,
    string? CertPassword,
    string? CertThumbprint,
    string? CertStoreLocation,
    string? CertStoreName,
    string? RedirectUri);
```

- [ ] **Step 4: `RefreshConfigSection.cs`**

```csharp
namespace Weft.Config;

public sealed record RefreshConfigSection(
    string? Type,                          // "full" | "dataOnly" | "calculate" | "automatic"
    int? MaxParallelism,
    int? PollIntervalSeconds,
    IncrementalPolicyConfig? IncrementalPolicy,
    DynamicPartitionStrategyConfig? DynamicPartitionStrategy);

public sealed record IncrementalPolicyConfig(
    bool ApplyOnFirstDeploy,
    bool ApplyOnPolicyChange,
    string BookmarkMode);                  // "preserve" | "clearAll" | "clearForPolicyChange"

public sealed record DynamicPartitionStrategyConfig(
    string Mode,                           // "newestOnly" | "allTouched" | "none"
    int NewestN);
```

- [ ] **Step 5: `ParameterDeclaration.cs`**

```csharp
namespace Weft.Config;

public sealed record ParameterDeclaration(
    string Name,
    string? Description,
    string Type,                           // "string" | "bool" | "int"
    bool Required,
    object? Default);
```

- [ ] **Step 6: `HooksConfigSection.cs`**

```csharp
namespace Weft.Config;

public sealed record HooksConfigSection(
    string? PrePlan,
    string? PreDeploy,
    string? PostDeploy,
    string? PreRefresh,
    string? PostRefresh,
    string? OnFailure);
```

- [ ] **Step 7: `WeftConfigValidationException.cs`**

```csharp
namespace Weft.Config;

public sealed class WeftConfigValidationException : Exception
{
    public WeftConfigValidationException(string message) : base(message) {}
}
```

- [ ] **Step 8: Build + commit**

```bash
dotnet build
git add src/Weft.Config/
git commit -m "feat(config): WeftConfig data model records (profile, auth, refresh, params, hooks)"
```

---

### Task 3: `YamlConfigLoader` (YAML → `WeftConfig`)

**Files:**
- Create: `src/Weft.Config/YamlConfigLoader.cs`
- Create: `test/Weft.Config.Tests/fixtures/weft.yaml`
- Create: `test/Weft.Config.Tests/YamlConfigLoaderTests.cs`

- [ ] **Step 1: Sample fixture**

`test/Weft.Config.Tests/fixtures/weft.yaml`:
```yaml
version: 1

source:
  format: bim
  path: ./model.bim

defaults:
  refresh:
    type: full
    maxParallelism: 10
    pollIntervalSeconds: 15
    incrementalPolicy:
      applyOnFirstDeploy: true
      applyOnPolicyChange: true
      bookmarkMode: preserve
    dynamicPartitionStrategy:
      mode: newestOnly
      newestN: 1
  allowDrops: false
  allowHistoryLoss: false
  timeoutMinutes: 60

profiles:
  dev:
    workspace: "powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev"
    database: SalesModel
    auth:
      mode: Interactive
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_CLIENT_ID}"
    parameters:
      DatabaseName: EDW_DEV
      ServerName: dev-sql.corp.local
      EnableDebugMeasures: true

  prod:
    workspace: "powerbi://api.powerbi.com/v1.0/myorg/Weft-Prod"
    database: SalesModel
    auth:
      mode: ServicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"
      certStoreLocation: LocalMachine
      certStoreName: My
    allowDrops: false
    allowHistoryLoss: false
    parameters:
      DatabaseName: EDW_PROD
      ServerName: prod-sql.corp.local

parameters:
  - name: DatabaseName
    description: Warehouse database name
    type: string
    required: true
  - name: ServerName
    type: string
    required: true
  - name: EnableDebugMeasures
    type: bool
    required: false

hooks:
  preDeploy: ./hooks/notify-teams.ps1
  postDeploy: ./hooks/tag-git-release.sh
  onFailure: ./hooks/open-incident.ps1
```

- [ ] **Step 2: Failing test**

`test/Weft.Config.Tests/YamlConfigLoaderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class YamlConfigLoaderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Loads_full_weft_yaml()
    {
        var cfg = YamlConfigLoader.LoadFromFile(FixturePath("weft.yaml"));

        cfg.Version.Should().Be(1);
        cfg.Source!.Format.Should().Be("bim");
        cfg.Defaults!.Refresh!.IncrementalPolicy!.BookmarkMode.Should().Be("preserve");
        cfg.Profiles.Should().ContainKeys("dev", "prod");
        cfg.Profiles["prod"].Auth.Mode.Should().Be("ServicePrincipalCertStore");
        cfg.Profiles["prod"].Parameters.Should().ContainKey("DatabaseName")
            .WhoseValue.Should().Be("EDW_PROD");
        cfg.Parameters.Should().ContainSingle(p => p.Name == "DatabaseName" && p.Required);
        cfg.Hooks!.PreDeploy.Should().Be("./hooks/notify-teams.ps1");
    }

    [Fact]
    public void Throws_on_missing_file()
    {
        var act = () => YamlConfigLoader.LoadFromFile("/no/such/weft.yaml");
        act.Should().Throw<FileNotFoundException>();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~YamlConfigLoaderTests
```
Expected: FAIL.

- [ ] **Step 3: Implement loader**

`src/Weft.Config/YamlConfigLoader.cs`:
```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Weft.Config;

public static class YamlConfigLoader
{
    public static WeftConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}", path);

        var yaml = File.ReadAllText(path);
        return LoadFromString(yaml);
    }

    public static WeftConfig LoadFromString(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var dto = deserializer.Deserialize<WeftConfigDto>(yaml)
            ?? throw new WeftConfigValidationException("Empty YAML.");
        return dto.ToDomain();
    }
}
```

And add `src/Weft.Config/WeftConfigDto.cs` (DTO shape YamlDotNet deserializes into — plain fields, not records):
```csharp
namespace Weft.Config;

internal sealed class WeftConfigDto
{
    public int Version { get; set; }
    public SourceDto? Source { get; set; }
    public DefaultsDto? Defaults { get; set; }
    public Dictionary<string, ProfileDto> Profiles { get; set; } = new();
    public List<ParameterDeclarationDto> Parameters { get; set; } = new();
    public Dictionary<string, ProfileOverridesDto> Overrides { get; set; } = new();
    public HooksDto? Hooks { get; set; }

    public WeftConfig ToDomain() => new(
        Version,
        Source?.ToDomain(),
        Defaults?.ToDomain(),
        Profiles.ToDictionary(p => p.Key, p => p.Value.ToDomain()),
        Parameters.Select(p => p.ToDomain()).ToList(),
        Overrides.ToDictionary(o => o.Key, o => o.Value.ToDomain()),
        Hooks?.ToDomain());
}

internal sealed class SourceDto
{
    public string Format { get; set; } = "bim";
    public string Path { get; set; } = "";
    public SourceConfigSection ToDomain() => new(Format, Path);
}

internal sealed class DefaultsDto
{
    public RefreshDto? Refresh { get; set; }
    public bool AllowDrops { get; set; }
    public bool AllowHistoryLoss { get; set; }
    public int TimeoutMinutes { get; set; } = 60;
    public DefaultsConfigSection ToDomain() =>
        new(Refresh?.ToDomain(), AllowDrops, AllowHistoryLoss, TimeoutMinutes);
}

internal sealed class RefreshDto
{
    public string? Type { get; set; }
    public int? MaxParallelism { get; set; }
    public int? PollIntervalSeconds { get; set; }
    public IncrementalPolicyDto? IncrementalPolicy { get; set; }
    public DynamicPartitionStrategyDto? DynamicPartitionStrategy { get; set; }
    public RefreshConfigSection ToDomain() => new(Type, MaxParallelism, PollIntervalSeconds,
        IncrementalPolicy?.ToDomain(), DynamicPartitionStrategy?.ToDomain());
}

internal sealed class IncrementalPolicyDto
{
    public bool ApplyOnFirstDeploy { get; set; } = true;
    public bool ApplyOnPolicyChange { get; set; } = true;
    public string BookmarkMode { get; set; } = "preserve";
    public IncrementalPolicyConfig ToDomain() => new(ApplyOnFirstDeploy, ApplyOnPolicyChange, BookmarkMode);
}

internal sealed class DynamicPartitionStrategyDto
{
    public string Mode { get; set; } = "newestOnly";
    public int NewestN { get; set; } = 1;
    public DynamicPartitionStrategyConfig ToDomain() => new(Mode, NewestN);
}

internal sealed class ProfileDto
{
    public string Workspace { get; set; } = "";
    public string Database { get; set; } = "";
    public AuthDto Auth { get; set; } = new();
    public RefreshDto? Refresh { get; set; }
    public bool? AllowDrops { get; set; }
    public bool? AllowHistoryLoss { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public ProfileConfig ToDomain() => new(Workspace, Database, Auth.ToDomain(),
        Refresh?.ToDomain(), AllowDrops, AllowHistoryLoss, Parameters);
}

internal sealed class AuthDto
{
    public string Mode { get; set; } = "Interactive";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? CertPath { get; set; }
    public string? CertPassword { get; set; }
    public string? CertThumbprint { get; set; }
    public string? CertStoreLocation { get; set; }
    public string? CertStoreName { get; set; }
    public string? RedirectUri { get; set; }
    public AuthConfigSection ToDomain() => new(Mode, TenantId, ClientId, ClientSecret,
        CertPath, CertPassword, CertThumbprint, CertStoreLocation, CertStoreName, RedirectUri);
}

internal sealed class ParameterDeclarationDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public object? Default { get; set; }
    public ParameterDeclaration ToDomain() => new(Name, Description, Type, Required, Default);
}

internal sealed class ProfileOverridesDto
{
    public Dictionary<string, DataSourceOverrideDto>? DataSources { get; set; }
    public ProfileOverridesSection ToDomain() =>
        new(DataSources?.ToDictionary(d => d.Key, d => d.Value.ToDomain()));
}

internal sealed class DataSourceOverrideDto
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public DataSourceOverride ToDomain() => new(Server, Database);
}

internal sealed class HooksDto
{
    public string? PrePlan { get; set; }
    public string? PreDeploy { get; set; }
    public string? PostDeploy { get; set; }
    public string? PreRefresh { get; set; }
    public string? PostRefresh { get; set; }
    public string? OnFailure { get; set; }
    public HooksConfigSection ToDomain() =>
        new(PrePlan, PreDeploy, PostDeploy, PreRefresh, PostRefresh, OnFailure);
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~YamlConfigLoaderTests
```
Expected: 2 PASS.

```bash
git add src/Weft.Config/YamlConfigLoader.cs src/Weft.Config/WeftConfigDto.cs test/Weft.Config.Tests/YamlConfigLoaderTests.cs test/Weft.Config.Tests/fixtures/weft.yaml
git commit -m "feat(config): YamlConfigLoader parses weft.yaml into WeftConfig"
```

---

### Task 4: `EnvVarExpander`

**Files:**
- Create: `src/Weft.Config/EnvVarExpander.cs`
- Create: `test/Weft.Config.Tests/EnvVarExpanderTests.cs`

Expand `${VAR}` references in string fields of a loaded `WeftConfig`. Called after `YamlConfigLoader.LoadFromFile`. Missing env vars throw `WeftConfigValidationException`.

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class EnvVarExpanderTests
{
    [Fact]
    public void Expands_dollar_brace_references_in_strings()
    {
        try
        {
            Environment.SetEnvironmentVariable("WEFT_TEST_TENANT", "tenant-123");
            var input = "${WEFT_TEST_TENANT}";
            EnvVarExpander.Expand(input).Should().Be("tenant-123");
        }
        finally { Environment.SetEnvironmentVariable("WEFT_TEST_TENANT", null); }
    }

    [Fact]
    public void Leaves_unreferenced_strings_alone()
    {
        EnvVarExpander.Expand("plain-text").Should().Be("plain-text");
        EnvVarExpander.Expand(null).Should().BeNull();
    }

    [Fact]
    public void Throws_when_referenced_variable_missing()
    {
        Environment.SetEnvironmentVariable("WEFT_TEST_MISSING", null);
        var act = () => EnvVarExpander.Expand("${WEFT_TEST_MISSING}");
        act.Should().Throw<WeftConfigValidationException>().WithMessage("*WEFT_TEST_MISSING*");
    }

    [Fact]
    public void Expands_multiple_references_in_one_string()
    {
        try
        {
            Environment.SetEnvironmentVariable("WEFT_A", "alpha");
            Environment.SetEnvironmentVariable("WEFT_B", "beta");
            EnvVarExpander.Expand("${WEFT_A}-${WEFT_B}").Should().Be("alpha-beta");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEFT_A", null);
            Environment.SetEnvironmentVariable("WEFT_B", null);
        }
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~EnvVarExpanderTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Config/EnvVarExpander.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Weft.Config;

public static class EnvVarExpander
{
    private static readonly Regex Pattern = new(@"\$\{([A-Z_][A-Z0-9_]*)\}", RegexOptions.Compiled);

    public static string? Expand(string? input)
    {
        if (input is null) return null;
        return Pattern.Replace(input, match =>
        {
            var name = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(name);
            if (value is null)
                throw new WeftConfigValidationException(
                    $"Environment variable '{name}' referenced in config is not set.");
            return value;
        });
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~EnvVarExpanderTests
```
Expected: 4 PASS.

```bash
git add src/Weft.Config/EnvVarExpander.cs test/Weft.Config.Tests/EnvVarExpanderTests.cs
git commit -m "feat(config): EnvVarExpander resolves \${VAR} in strings, errors on missing"
```

---

### Task 5: `ProfileMerger` (defaults + profile → effective profile config)

**Files:**
- Create: `src/Weft.Config/ProfileMerger.cs`
- Create: `test/Weft.Config.Tests/ProfileMergerTests.cs`

Given a loaded `WeftConfig` and a target profile name (e.g., "prod"), merge defaults with profile-specific overrides into an "effective" `ProfileConfig`.

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class ProfileMergerTests
{
    [Fact]
    public void Profile_allowDrops_overrides_defaults_when_set()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));

        var eff = new ProfileMerger().Merge(cfg, "prod");

        eff.AllowDrops.Should().BeFalse();            // prod profile pins false
        eff.AllowHistoryLoss.Should().BeFalse();      // prod profile pins false
    }

    [Fact]
    public void Profile_inherits_defaults_refresh_policy_when_unset()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));

        var eff = new ProfileMerger().Merge(cfg, "dev");

        eff.Refresh!.IncrementalPolicy!.BookmarkMode.Should().Be("preserve");
        eff.Refresh.MaxParallelism.Should().Be(10);
    }

    [Fact]
    public void Throws_on_unknown_profile()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));
        var act = () => new ProfileMerger().Merge(cfg, "ghost");
        act.Should().Throw<WeftConfigValidationException>().WithMessage("*ghost*");
    }
}
```

`ProfileMerger.Merge` returns the effective profile as a new `EffectiveProfileConfig` record to keep the input `ProfileConfig` immutable. Add the record:

- [ ] **Step 2: Add `EffectiveProfileConfig`**

In `src/Weft.Config/ProfileMerger.cs` or a companion file:
```csharp
namespace Weft.Config;

public sealed record EffectiveProfileConfig(
    string ProfileName,
    string Workspace,
    string Database,
    AuthConfigSection Auth,
    RefreshConfigSection Refresh,
    bool AllowDrops,
    bool AllowHistoryLoss,
    int TimeoutMinutes,
    IReadOnlyDictionary<string, object?> Parameters,
    HooksConfigSection Hooks);
```

- [ ] **Step 3: Implement merger**

`src/Weft.Config/ProfileMerger.cs`:
```csharp
namespace Weft.Config;

public sealed class ProfileMerger
{
    private static readonly RefreshConfigSection DefaultRefresh = new(
        Type: "full",
        MaxParallelism: 10,
        PollIntervalSeconds: 15,
        IncrementalPolicy: new IncrementalPolicyConfig(
            ApplyOnFirstDeploy: true, ApplyOnPolicyChange: true, BookmarkMode: "preserve"),
        DynamicPartitionStrategy: new DynamicPartitionStrategyConfig(
            Mode: "newestOnly", NewestN: 1));

    public EffectiveProfileConfig Merge(WeftConfig config, string profileName)
    {
        if (!config.Profiles.TryGetValue(profileName, out var profile))
            throw new WeftConfigValidationException(
                $"Profile '{profileName}' not found in config. Known: {string.Join(", ", config.Profiles.Keys)}.");

        var defaults = config.Defaults;
        var refresh = MergeRefresh(defaults?.Refresh, profile.Refresh);
        var allowDrops = profile.AllowDrops ?? defaults?.AllowDrops ?? false;
        var allowHistoryLoss = profile.AllowHistoryLoss ?? defaults?.AllowHistoryLoss ?? false;
        var timeout = defaults?.TimeoutMinutes ?? 60;
        var hooks = config.Hooks ?? new HooksConfigSection(null, null, null, null, null, null);

        return new EffectiveProfileConfig(
            ProfileName: profileName,
            Workspace: profile.Workspace,
            Database: profile.Database,
            Auth: profile.Auth,
            Refresh: refresh,
            AllowDrops: allowDrops,
            AllowHistoryLoss: allowHistoryLoss,
            TimeoutMinutes: timeout,
            Parameters: profile.Parameters,
            Hooks: hooks);
    }

    private static RefreshConfigSection MergeRefresh(RefreshConfigSection? d, RefreshConfigSection? p) =>
        new(
            Type: p?.Type ?? d?.Type ?? DefaultRefresh.Type,
            MaxParallelism: p?.MaxParallelism ?? d?.MaxParallelism ?? DefaultRefresh.MaxParallelism,
            PollIntervalSeconds: p?.PollIntervalSeconds ?? d?.PollIntervalSeconds ?? DefaultRefresh.PollIntervalSeconds,
            IncrementalPolicy: MergeIncremental(d?.IncrementalPolicy, p?.IncrementalPolicy),
            DynamicPartitionStrategy: MergeDynamic(d?.DynamicPartitionStrategy, p?.DynamicPartitionStrategy));

    private static IncrementalPolicyConfig MergeIncremental(
        IncrementalPolicyConfig? d, IncrementalPolicyConfig? p) =>
        new(
            ApplyOnFirstDeploy: p?.ApplyOnFirstDeploy ?? d?.ApplyOnFirstDeploy ?? true,
            ApplyOnPolicyChange: p?.ApplyOnPolicyChange ?? d?.ApplyOnPolicyChange ?? true,
            BookmarkMode: p?.BookmarkMode ?? d?.BookmarkMode ?? "preserve");

    private static DynamicPartitionStrategyConfig MergeDynamic(
        DynamicPartitionStrategyConfig? d, DynamicPartitionStrategyConfig? p) =>
        new(
            Mode: p?.Mode ?? d?.Mode ?? "newestOnly",
            NewestN: p?.NewestN ?? d?.NewestN ?? 1);
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ProfileMergerTests
```
Expected: 3 PASS.

```bash
git add src/Weft.Config/ProfileMerger.cs test/Weft.Config.Tests/ProfileMergerTests.cs
git commit -m "feat(config): ProfileMerger resolves defaults + profile overrides"
```

---

### Task 6: JSON Schema for `weft.yaml`

**Files:**
- Create: `schemas/weft.schema.json`

This is a static JSON Schema document that describes the allowed shape of `weft.yaml` for editor validation (VS Code YAML extension, JetBrains IDEs). Not consumed by Weft's runtime yet — it lives at a predictable path the docs can reference.

- [ ] **Step 1: Author schema**

`schemas/weft.schema.json`:
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Weft configuration",
  "type": "object",
  "required": ["version", "profiles"],
  "properties": {
    "version": { "type": "integer", "enum": [1] },
    "source": {
      "type": "object",
      "required": ["format", "path"],
      "properties": {
        "format": { "type": "string", "enum": ["bim", "folder"] },
        "path":   { "type": "string" }
      }
    },
    "defaults": { "$ref": "#/$defs/defaults" },
    "profiles": {
      "type": "object",
      "additionalProperties": { "$ref": "#/$defs/profile" }
    },
    "parameters": {
      "type": "array",
      "items": { "$ref": "#/$defs/parameterDeclaration" }
    },
    "overrides": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "properties": {
          "dataSources": {
            "type": "object",
            "additionalProperties": {
              "type": "object",
              "properties": {
                "server":   { "type": "string" },
                "database": { "type": "string" }
              }
            }
          }
        }
      }
    },
    "hooks": { "$ref": "#/$defs/hooks" }
  },
  "$defs": {
    "defaults": {
      "type": "object",
      "properties": {
        "refresh": { "$ref": "#/$defs/refresh" },
        "allowDrops": { "type": "boolean" },
        "allowHistoryLoss": { "type": "boolean" },
        "timeoutMinutes": { "type": "integer", "minimum": 1 }
      }
    },
    "profile": {
      "type": "object",
      "required": ["workspace", "database", "auth"],
      "properties": {
        "workspace": { "type": "string" },
        "database":  { "type": "string" },
        "auth":      { "$ref": "#/$defs/auth" },
        "refresh":   { "$ref": "#/$defs/refresh" },
        "allowDrops": { "type": "boolean" },
        "allowHistoryLoss": { "type": "boolean" },
        "parameters": {
          "type": "object",
          "additionalProperties": true
        }
      }
    },
    "auth": {
      "type": "object",
      "required": ["mode", "tenantId", "clientId"],
      "properties": {
        "mode": {
          "type": "string",
          "enum": [
            "ServicePrincipalSecret",
            "ServicePrincipalCertFile",
            "ServicePrincipalCertStore",
            "Interactive",
            "DeviceCode"
          ]
        },
        "tenantId": { "type": "string" },
        "clientId": { "type": "string" },
        "clientSecret":     { "type": "string" },
        "certPath":         { "type": "string" },
        "certPassword":     { "type": "string" },
        "certThumbprint":   { "type": "string" },
        "certStoreLocation":{ "type": "string", "enum": ["LocalMachine", "CurrentUser"] },
        "certStoreName":    { "type": "string" },
        "redirectUri":      { "type": "string" }
      }
    },
    "refresh": {
      "type": "object",
      "properties": {
        "type": { "type": "string", "enum": ["full", "dataOnly", "calculate", "automatic"] },
        "maxParallelism": { "type": "integer", "minimum": 1 },
        "pollIntervalSeconds": { "type": "integer", "minimum": 1 },
        "incrementalPolicy": {
          "type": "object",
          "properties": {
            "applyOnFirstDeploy":  { "type": "boolean" },
            "applyOnPolicyChange": { "type": "boolean" },
            "bookmarkMode": {
              "type": "string",
              "enum": ["preserve", "clearAll", "clearForPolicyChange"]
            }
          }
        },
        "dynamicPartitionStrategy": {
          "type": "object",
          "properties": {
            "mode":    { "type": "string", "enum": ["newestOnly", "allTouched", "none"] },
            "newestN": { "type": "integer", "minimum": 0 }
          }
        }
      }
    },
    "parameterDeclaration": {
      "type": "object",
      "required": ["name", "type"],
      "properties": {
        "name":        { "type": "string" },
        "description": { "type": "string" },
        "type":        { "type": "string", "enum": ["string", "bool", "int"] },
        "required":    { "type": "boolean" },
        "default":     {}
      }
    },
    "hooks": {
      "type": "object",
      "properties": {
        "prePlan":     { "type": "string" },
        "preDeploy":   { "type": "string" },
        "postDeploy":  { "type": "string" },
        "preRefresh":  { "type": "string" },
        "postRefresh": { "type": "string" },
        "onFailure":   { "type": "string" }
      }
    }
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add schemas/weft.schema.json
git commit -m "docs(schema): weft.schema.json for editor validation of weft.yaml"
```

---

### Task 7: `MParameterDiscoverer`

**Files:**
- Create: `src/Weft.Core/Parameters/MParameterDiscoverer.cs`
- Create: `test/Weft.Core.Tests/Parameters/MParameterDiscovererTests.cs`

Discover every M parameter expression in a source `Database`. In TOM, M parameters are `NamedExpression` items on `Model.Expressions` with `Kind = ExpressionKind.M` whose `Expression` literal matches the shape of a declared parameter (has `Type.ParameterType` annotation OR is wrapped in `type text meta [...]`).

For v1, the conservative detection rule: **a parameter expression has a `@"^""\w+"" meta"` prefix OR its name starts with lowercase/capital letter and the expression is a single string literal**. Real-world detection needs the full M AST — we use a practical heuristic here that covers Power BI Desktop parameters.

Simpler rule actually used by Power BI: **every `NamedExpression` with `Kind == ExpressionKind.M` that has an `IsParameterQuery` annotation OR uses the canonical `#type ... meta [IsParameterQuery=true]` wrapping is a parameter**. In TOM, the annotation is `IsParameterQuery` — check annotations first.

- [ ] **Step 1: Failing test**

`test/Weft.Core.Tests/Parameters/MParameterDiscovererTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class MParameterDiscovererTests
{
    [Fact]
    public void Returns_empty_when_model_has_no_expressions()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        new MParameterDiscoverer().Discover(db).Should().BeEmpty();
    }

    [Fact]
    public void Finds_parameter_expressions_by_IsParameterQuery_annotation()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();

        var pExpr = new NamedExpression
        {
            Name = "DatabaseName",
            Kind = ExpressionKind.M,
            Expression = "\"EDW\" meta [IsParameterQuery=true, Type=\"Text\"]"
        };
        pExpr.Annotations.Add(new Annotation { Name = "IsParameterQuery", Value = "true" });
        db.Model.Expressions.Add(pExpr);

        var nonParam = new NamedExpression
        {
            Name = "NotAParam",
            Kind = ExpressionKind.M,
            Expression = "let x = 1 in x"
        };
        db.Model.Expressions.Add(nonParam);

        var found = new MParameterDiscoverer().Discover(db);
        found.Select(p => p.Name).Should().Equal("DatabaseName");
        found.Single().ExpressionText.Should().StartWith("\"EDW\"");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~MParameterDiscovererTests
```
Expected: FAIL.

- [ ] **Step 2: Implement discoverer**

`src/Weft.Core/Parameters/MParameterDiscoverer.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Parameters;

public sealed record DiscoveredMParameter(string Name, string ExpressionText, NamedExpression Source);

public sealed class MParameterDiscoverer
{
    public IReadOnlyList<DiscoveredMParameter> Discover(Database database)
    {
        var result = new List<DiscoveredMParameter>();
        foreach (var expr in database.Model.Expressions)
        {
            if (expr.Kind != ExpressionKind.M) continue;
            if (!IsParameterQuery(expr)) continue;
            result.Add(new DiscoveredMParameter(expr.Name, expr.Expression, expr));
        }
        return result;
    }

    private static bool IsParameterQuery(NamedExpression expr)
    {
        var annotation = expr.Annotations.Find("IsParameterQuery");
        return string.Equals(annotation?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~MParameterDiscovererTests
```
Expected: 2 PASS.

```bash
git add src/Weft.Core/Parameters/MParameterDiscoverer.cs test/Weft.Core.Tests/Parameters/MParameterDiscovererTests.cs
git commit -m "feat(core): MParameterDiscoverer finds IsParameterQuery expressions"
```

---

### Task 8: `ParameterValueCoercer`

**Files:**
- Create: `src/Weft.Core/Parameters/ParameterValueCoercer.cs`
- Create: `test/Weft.Core.Tests/Parameters/ParameterValueCoercerTests.cs`

Convert config values (objects from YAML: strings, bools, ints, null) into the M literal expression the parameter body expects. A `string`-typed parameter becomes `"value"`; a `bool` becomes `true`/`false`; an `int` becomes a bare number.

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class ParameterValueCoercerTests
{
    [Fact]
    public void String_value_becomes_quoted_M_literal() =>
        ParameterValueCoercer.ToMLiteral("string", "EDW").Should().Be("\"EDW\"");

    [Fact]
    public void Bool_true_becomes_M_true_literal() =>
        ParameterValueCoercer.ToMLiteral("bool", true).Should().Be("true");

    [Fact]
    public void Bool_string_true_becomes_M_true_literal() =>
        ParameterValueCoercer.ToMLiteral("bool", "true").Should().Be("true");

    [Fact]
    public void Int_value_becomes_bare_number() =>
        ParameterValueCoercer.ToMLiteral("int", 42).Should().Be("42");

    [Fact]
    public void Type_mismatch_throws()
    {
        var act = () => ParameterValueCoercer.ToMLiteral("int", "not-a-number");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Escapes_quote_in_string_value() =>
        ParameterValueCoercer.ToMLiteral("string", "a\"b").Should().Be("\"a\"\"b\"");
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~ParameterValueCoercerTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Core/Parameters/ParameterValueCoercer.cs`:
```csharp
using System.Globalization;

namespace Weft.Core.Parameters;

public static class ParameterValueCoercer
{
    public static string ToMLiteral(string declaredType, object? rawValue)
    {
        switch (declaredType.ToLowerInvariant())
        {
            case "string":
            {
                var s = rawValue?.ToString() ?? "";
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            case "bool":
            {
                var b = rawValue switch
                {
                    bool x => x,
                    string s => bool.Parse(s),
                    _ => throw new FormatException($"Cannot coerce '{rawValue}' to bool.")
                };
                return b ? "true" : "false";
            }
            case "int":
            {
                var i = rawValue switch
                {
                    int x => x,
                    long x => checked((int)x),
                    string s => int.Parse(s, CultureInfo.InvariantCulture),
                    _ => throw new FormatException($"Cannot coerce '{rawValue}' to int.")
                };
                return i.ToString(CultureInfo.InvariantCulture);
            }
            default:
                throw new NotSupportedException($"Unsupported declared type: '{declaredType}'.");
        }
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ParameterValueCoercerTests
```
Expected: 6 PASS.

```bash
git add src/Weft.Core/Parameters/ParameterValueCoercer.cs test/Weft.Core.Tests/Parameters/ParameterValueCoercerTests.cs
git commit -m "feat(core): ParameterValueCoercer converts YAML values into M literals"
```

---

### Task 9: `ParameterResolver` — validate and apply

**Files:**
- Create: `src/Weft.Core/Parameters/ParameterValueSource.cs`
- Create: `src/Weft.Core/Parameters/ParameterResolution.cs`
- Create: `src/Weft.Core/Parameters/ParameterApplicationException.cs`
- Create: `src/Weft.Core/Parameters/ParameterResolver.cs`
- Create: `test/Weft.Core.Tests/Parameters/ParameterResolverTests.cs`

- [ ] **Step 1: Supporting types**

`ParameterValueSource.cs`:
```csharp
namespace Weft.Core.Parameters;

public enum ParameterValueSource { Cli, ParamsFile, EnvVar, ProfileYaml, ModelDefault }
```

`ParameterResolution.cs`:
```csharp
namespace Weft.Core.Parameters;

public sealed record ParameterResolution(
    string Name,
    string DeclaredType,
    object? RawValue,
    ParameterValueSource Source);
```

`ParameterApplicationException.cs`:
```csharp
namespace Weft.Core.Parameters;

public sealed class ParameterApplicationException : Exception
{
    public ParameterApplicationException(string message) : base(message) {}
}
```

- [ ] **Step 2: Failing tests**

`test/Weft.Core.Tests/Parameters/ParameterResolverTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class ParameterResolverTests
{
    private static Database MakeDbWithParam(string name, string initialLiteral)
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var e = new NamedExpression
        {
            Name = name,
            Kind = ExpressionKind.M,
            Expression = initialLiteral
        };
        e.Annotations.Add(new Annotation { Name = "IsParameterQuery", Value = "true" });
        db.Model.Expressions.Add(e);
        return db;
    }

    [Fact]
    public void Resolution_priority_cli_beats_profile()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();

        var resolutions = resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new Weft.Config.ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?> { ["DatabaseName"] = "EDW_YAML" },
            cliOverrides: new Dictionary<string, string> { ["DatabaseName"] = "EDW_CLI" },
            paramsFileValues: null);

        resolutions.Single().RawValue.Should().Be("EDW_CLI");
        resolutions.Single().Source.Should().Be(ParameterValueSource.Cli);
    }

    [Fact]
    public void Required_parameter_without_value_throws()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();
        var act = () => resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new Weft.Config.ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?>(),
            cliOverrides: null,
            paramsFileValues: null);
        act.Should().Throw<ParameterApplicationException>().WithMessage("*DatabaseName*");
    }

    [Fact]
    public void Apply_rewrites_parameter_expression_in_place()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();

        var resolutions = resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new Weft.Config.ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?> { ["DatabaseName"] = "EDW_PROD" },
            cliOverrides: null,
            paramsFileValues: null);

        resolver.Apply(db, resolutions);

        db.Model.Expressions["DatabaseName"].Expression.Should().Be("\"EDW_PROD\"");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~ParameterResolverTests
```
Expected: FAIL.

- [ ] **Step 3: Implement resolver**

`src/Weft.Core/Parameters/ParameterResolver.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Config;

namespace Weft.Core.Parameters;

public sealed class ParameterResolver
{
    private readonly MParameterDiscoverer _discoverer = new();

    public IReadOnlyList<ParameterResolution> Resolve(
        Database sourceDb,
        IEnumerable<ParameterDeclaration> declarations,
        IReadOnlyDictionary<string, object?>? profileValues,
        IReadOnlyDictionary<string, string>? cliOverrides,
        IReadOnlyDictionary<string, object?>? paramsFileValues)
    {
        var discovered = _discoverer.Discover(sourceDb).ToDictionary(p => p.Name, StringComparer.Ordinal);
        var declsByName = declarations.ToDictionary(d => d.Name, StringComparer.Ordinal);

        var resolutions = new List<ParameterResolution>();
        foreach (var (name, decl) in declsByName)
        {
            (object? value, ParameterValueSource source) resolved;
            if (cliOverrides is not null && cliOverrides.TryGetValue(name, out var cliValue))
                resolved = (cliValue, ParameterValueSource.Cli);
            else if (paramsFileValues is not null && paramsFileValues.TryGetValue(name, out var fileValue))
                resolved = (fileValue, ParameterValueSource.ParamsFile);
            else if (Environment.GetEnvironmentVariable($"WEFT_PARAM_{name}") is { } envValue)
                resolved = (envValue, ParameterValueSource.EnvVar);
            else if (profileValues is not null && profileValues.TryGetValue(name, out var yamlValue))
                resolved = (yamlValue, ParameterValueSource.ProfileYaml);
            else if (decl.Default is not null)
                resolved = (decl.Default, ParameterValueSource.ModelDefault);
            else if (decl.Required)
                throw new ParameterApplicationException(
                    $"Required parameter '{name}' has no value (CLI, params file, env var, profile YAML, or declaration default).");
            else continue;

            resolutions.Add(new ParameterResolution(name, decl.Type, resolved.value, resolved.source));
        }
        return resolutions;
    }

    public void Apply(Database sourceDb, IEnumerable<ParameterResolution> resolutions)
    {
        var discovered = _discoverer.Discover(sourceDb).ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var r in resolutions)
        {
            if (!discovered.TryGetValue(r.Name, out var param))
                throw new ParameterApplicationException(
                    $"Parameter '{r.Name}' declared in config but not present in source model.");

            var literal = ParameterValueCoercer.ToMLiteral(r.DeclaredType, r.RawValue);
            var metaSuffix = ExtractMetaSuffix(param.ExpressionText);
            param.Source.Expression = literal + (metaSuffix ?? "");
        }
    }

    private static string? ExtractMetaSuffix(string expression)
    {
        var idx = expression.IndexOf(" meta ", StringComparison.Ordinal);
        return idx >= 0 ? expression[idx..] : null;
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ParameterResolverTests
```
Expected: 3 PASS.

```bash
git add src/Weft.Core/Parameters/ test/Weft.Core.Tests/Parameters/
git commit -m "feat(core): ParameterResolver (priority: CLI > file > env > YAML > default) and Apply"
```

---

### Task 10: `HookRunner`

**Files:**
- Create: `src/Weft.Core/Hooks/HookPhase.cs`
- Create: `src/Weft.Core/Hooks/HookDefinition.cs`
- Create: `src/Weft.Core/Hooks/HookContext.cs`
- Create: `src/Weft.Core/Hooks/HookRunner.cs`
- Create: `test/Weft.Core.Tests/Hooks/HookRunnerTests.cs`

A hook is an external command (shell script, .ps1, .sh, or any executable) triggered at one of six pipeline phases. The runner writes the `HookContext` JSON to stdin and captures stdout/stderr for logs. On failure, hooks return non-zero; Weft logs and continues unless the hook is `onFailure` (which gets suppressed anyway).

- [ ] **Step 1: Data types**

`src/Weft.Core/Hooks/HookPhase.cs`:
```csharp
namespace Weft.Core.Hooks;

public enum HookPhase { PrePlan, PreDeploy, PostDeploy, PreRefresh, PostRefresh, OnFailure }
```

`src/Weft.Core/Hooks/HookDefinition.cs`:
```csharp
namespace Weft.Core.Hooks;

public sealed record HookDefinition(HookPhase Phase, string Command);
```

`src/Weft.Core/Hooks/HookContext.cs`:
```csharp
using Weft.Core.Diffing;

namespace Weft.Core.Hooks;

public sealed record HookContext(
    string ProfileName,
    string WorkspaceUrl,
    string DatabaseName,
    HookPhase Phase,
    ChangeSetSnapshot ChangeSet);

public sealed record ChangeSetSnapshot(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Dropped,
    IReadOnlyList<string> Altered,
    IReadOnlyList<string> Unchanged)
{
    public static ChangeSetSnapshot From(ChangeSet cs) => new(
        Added: cs.TablesToAdd.Select(t => t.Name).ToList(),
        Dropped: cs.TablesToDrop.ToList(),
        Altered: cs.TablesToAlter.Select(t => t.Name).ToList(),
        Unchanged: cs.TablesUnchanged.ToList());
}
```

- [ ] **Step 2: Failing test**

`test/Weft.Core.Tests/Hooks/HookRunnerTests.cs`:
```csharp
using FluentAssertions;
using Weft.Core.Diffing;
using Weft.Core.Hooks;
using Xunit;

namespace Weft.Core.Tests.Hooks;

public class HookRunnerTests
{
    [Fact]
    public async Task Runs_a_shell_command_and_captures_exit_code()
    {
        var runner = new HookRunner();
        var ctx = new HookContext(
            ProfileName: "test",
            WorkspaceUrl: "powerbi://x",
            DatabaseName: "D",
            Phase: HookPhase.PreDeploy,
            ChangeSet: new ChangeSetSnapshot(
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>()));

        // `true` is a POSIX command that exits 0. On Windows this test is skipped.
        if (OperatingSystem.IsWindows())
            return;

        var result = await runner.RunAsync(new HookDefinition(HookPhase.PreDeploy, "true"), ctx);
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Nonzero_exit_is_surfaced_but_does_not_throw()
    {
        if (OperatingSystem.IsWindows())
            return;

        var runner = new HookRunner();
        var ctx = new HookContext("t", "x", "D", HookPhase.PreDeploy,
            new ChangeSetSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>()));

        var result = await runner.RunAsync(new HookDefinition(HookPhase.PreDeploy, "false"), ctx);
        result.ExitCode.Should().NotBe(0);
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~HookRunnerTests
```
Expected: FAIL.

- [ ] **Step 3: Implement runner**

`src/Weft.Core/Hooks/HookRunner.cs`:
```csharp
using System.Diagnostics;
using System.Text.Json;

namespace Weft.Core.Hooks;

public sealed record HookRunResult(int ExitCode, string Stdout, string Stderr);

public sealed class HookRunner
{
    public async Task<HookRunResult> RunAsync(HookDefinition hook, HookContext context, CancellationToken ct = default)
    {
        var (fileName, args) = SplitCommand(hook.Command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["WEFT_HOOK_PHASE"] = context.Phase.ToString();
        psi.Environment["WEFT_HOOK_PROFILE"] = context.ProfileName;
        psi.Environment["WEFT_HOOK_DATABASE"] = context.DatabaseName;

        using var p = Process.Start(psi)!;
        var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = false });
        await p.StandardInput.WriteAsync(json);
        p.StandardInput.Close();

        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);

        return new HookRunResult(p.ExitCode, stdout, stderr);
    }

    private static (string FileName, string[] Args) SplitCommand(string command)
    {
        // Simple split: first token is executable, rest are args. Users needing quoting
        // should pass a script file (e.g. './hooks/notify.ps1 arg1 arg2').
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) throw new ArgumentException("Empty hook command.", nameof(command));
        return (tokens[0], tokens[1..]);
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~HookRunnerTests
```
Expected: 2 PASS (or 2 skip on Windows — both OK).

```bash
git add src/Weft.Core/Hooks/ test/Weft.Core.Tests/Hooks/
git commit -m "feat(core): HookRunner (shell-out with HookContext JSON on stdin)"
```

---

### Task 11: `BookmarkClearer`

**Files:**
- Create: `src/Weft.Xmla/BookmarkClearer.cs`
- Create: `test/Weft.Xmla.Tests/BookmarkClearerTests.cs`

Emit a TMSL `alter` sequence that removes the `RefreshBookmark` annotation from every partition of the specified tables. The cleared TMSL is executed before refresh when `bookmarkMode` dictates clearing.

- [ ] **Step 1: Failing test**

`test/Weft.Xmla.Tests/BookmarkClearerTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Partitions;
using Weft.Xmla;
using Xunit;

namespace Weft.Xmla.Tests;

public class BookmarkClearerTests
{
    [Fact]
    public void Emits_sequence_that_clears_bookmark_annotations_on_named_tables()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var t = new Table { Name = "FactSales" };
        var p = new Partition
        {
            Name = "FactSales",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        };
        p.Annotations.Add(new Annotation { Name = PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });
        t.Partitions.Add(p);
        db.Model.Tables.Add(t);

        var json = new BookmarkClearer().BuildTmsl(db, new[] { "FactSales" });

        json.Should().Contain("\"alter\"");
        json.Should().Contain("\"FactSales\"");
        // The emitted Partition block must not contain the RefreshBookmark annotation.
        json.Should().NotContain("\"value\": \"wm-001\"");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~BookmarkClearerTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Xmla/BookmarkClearer.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Partitions;

namespace Weft.Xmla;

public sealed class BookmarkClearer
{
    public string BuildTmsl(Database target, IEnumerable<string> tableNames)
    {
        var operations = new JsonArray();
        var dbName = target.Name;

        foreach (var tableName in tableNames)
        {
            if (!target.Model.Tables.ContainsName(tableName)) continue;
            var t = target.Model.Tables[tableName];

            foreach (var partition in t.Partitions)
            {
                var bookmark = partition.Annotations.Find(PartitionAnnotationNames.RefreshBookmark);
                if (bookmark is null) continue;

                operations.Add(new JsonObject
                {
                    ["delete"] = new JsonObject
                    {
                        ["object"] = new JsonObject
                        {
                            ["database"]  = dbName,
                            ["table"]     = tableName,
                            ["partition"] = partition.Name,
                            ["annotation"] = PartitionAnnotationNames.RefreshBookmark
                        }
                    }
                });
            }
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
dotnet test --filter FullyQualifiedName~BookmarkClearerTests
```
Expected: PASS.

```bash
git add src/Weft.Xmla/BookmarkClearer.cs test/Weft.Xmla.Tests/BookmarkClearerTests.cs
git commit -m "feat(xmla): BookmarkClearer emits TMSL annotation-delete sequence"
```

---

### Task 12: Extend `RetentionCalculator` to Quarter/Month granularity

**Files:**
- Modify: `src/Weft.Core/RefreshPolicy/RetentionCalculator.cs`
- Create: `test/Weft.Core.Tests/RefreshPolicy/RetentionCalculatorQuarterMonthTests.cs`

Plan 1 only supported `RefreshGranularityType.Year`. Plan 3 adds Quarter and Month.

- [ ] **Step 1: Failing tests**

`test/Weft.Core.Tests/RefreshPolicy/RetentionCalculatorQuarterMonthTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RetentionCalculatorQuarterMonthTests
{
    private static BasicRefreshPolicy Policy(RefreshGranularityType g, int periods) => new()
    {
        RollingWindowGranularity = g,
        RollingWindowPeriods = periods,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void Quarter_granularity_lists_partitions_outside_new_window()
    {
        // Today 2026-04-17 => current quarter Q2-2026
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(RefreshGranularityType.Quarter, 8),
            newPolicy: Policy(RefreshGranularityType.Quarter, 4),
            existingPartitionNames: new[]
            {
                "Quarter2024Q3", "Quarter2024Q4",
                "Quarter2025Q1", "Quarter2025Q2", "Quarter2025Q3", "Quarter2025Q4",
                "Quarter2026Q1", "Quarter2026Q2"
            });
        // New window keeps 4 quarters ending at Q2-2026: Q3-25, Q4-25, Q1-26, Q2-26.
        lost.Should().BeEquivalentTo(new[] { "Quarter2024Q3", "Quarter2024Q4", "Quarter2025Q1", "Quarter2025Q2" });
    }

    [Fact]
    public void Month_granularity_lists_partitions_outside_new_window()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(RefreshGranularityType.Month, 24),
            newPolicy: Policy(RefreshGranularityType.Month, 6),
            existingPartitionNames: new[]
            {
                "Month2025-10", "Month2025-11", "Month2025-12",
                "Month2026-01", "Month2026-02", "Month2026-03", "Month2026-04"
            });
        // Kept: Nov-25 .. Apr-26 (6 months). Lost: Oct-25.
        lost.Should().BeEquivalentTo(new[] { "Month2025-10" });
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~RetentionCalculatorQuarterMonthTests
```
Expected: FAIL (still throws NotSupportedException for Quarter/Month).

- [ ] **Step 2: Implement Quarter/Month**

Extend `src/Weft.Core/RefreshPolicy/RetentionCalculator.cs` to handle three granularities. The existing "no loss when window unchanged or grew" early-return stays. Replace the Year-only `NotSupportedException` with a switch:
```csharp
public IReadOnlyList<string> PartitionsRemovedBy(
    BasicRefreshPolicy oldPolicy,
    BasicRefreshPolicy newPolicy,
    IEnumerable<string> existingPartitionNames)
{
    if (newPolicy.RollingWindowPeriods >= oldPolicy.RollingWindowPeriods
        && newPolicy.RollingWindowGranularity == oldPolicy.RollingWindowGranularity)
    {
        return Array.Empty<string>();
    }

    return newPolicy.RollingWindowGranularity switch
    {
        RefreshGranularityType.Year    => YearLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
        RefreshGranularityType.Quarter => QuarterLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
        RefreshGranularityType.Month   => MonthLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
        _ => throw new NotSupportedException(
            $"Granularity {newPolicy.RollingWindowGranularity} not supported by RetentionCalculator.")
    };
}

private IReadOnlyList<string> YearLoss(int periods, IEnumerable<string> names)
{
    var keep = Enumerable.Range(0, periods).Select(i => _today.Year - i).ToHashSet();
    return names
        .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^Year\d{4}$"))
        .Where(n => !keep.Contains(int.Parse(n.AsSpan(4))))
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();
}

private IReadOnlyList<string> QuarterLoss(int periods, IEnumerable<string> names)
{
    var currentQuarter = (_today.Year, Q: (_today.Month - 1) / 3 + 1);
    var kept = new HashSet<(int Y, int Q)>();
    for (int i = 0; i < periods; i++)
    {
        var q = currentQuarter.Q - i;
        var y = currentQuarter.Year;
        while (q <= 0) { q += 4; y--; }
        kept.Add((y, q));
    }
    return names
        .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^Quarter(\d{4})Q([1-4])$"))
        .Where(n =>
        {
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^Quarter(\d{4})Q([1-4])$");
            return !kept.Contains((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
        })
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();
}

private IReadOnlyList<string> MonthLoss(int periods, IEnumerable<string> names)
{
    var kept = new HashSet<(int Y, int M)>();
    for (int i = 0; i < periods; i++)
    {
        var cursor = new DateOnly(_today.Year, _today.Month, 1).AddMonths(-i);
        kept.Add((cursor.Year, cursor.Month));
    }
    return names
        .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^Month(\d{4})-(\d{2})$"))
        .Where(n =>
        {
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^Month(\d{4})-(\d{2})$");
            return !kept.Contains((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
        })
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();
}
```

Remove the old `IsYearPartition` / `YearOf` helpers since they are no longer needed.

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RetentionCalculator
```
Expected: existing Year tests still pass + 2 new Quarter/Month tests pass.

```bash
git add src/Weft.Core/RefreshPolicy/RetentionCalculator.cs test/Weft.Core.Tests/RefreshPolicy/RetentionCalculatorQuarterMonthTests.cs
git commit -m "feat(core): RetentionCalculator supports Year, Quarter, and Month granularity"
```

---

### Task 13: `HistoryLossGate` (pre-flight check)

**Files:**
- Create: `src/Weft.Core/RefreshPolicy/HistoryLossGate.cs`
- Create: `test/Weft.Core.Tests/RefreshPolicy/HistoryLossGateTests.cs`

Given a `ChangeSet`, target `Database`, and `allowHistoryLoss` flag, compute per-table which partitions would be lost on `ApplyRefreshPolicy` with the new policy, and return a list of violations. Blocks the deploy unless the flag is true.

- [ ] **Step 1: Failing test**

`test/Weft.Core.Tests/RefreshPolicy/HistoryLossGateTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class HistoryLossGateTests
{
    private static BasicRefreshPolicy P(int years) => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = years,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void No_violation_when_no_policy_changes()
    {
        var target = new Database { Name = "D", CompatibilityLevel = 1600 };
        target.Model = new Model();
        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));

        gate.Check(
            changeSet: EmptyChangeSet(),
            target: target,
            allowHistoryLoss: false).Should().BeEmpty();
    }

    [Fact]
    public void Violation_when_policy_shrinks_and_not_allowed()
    {
        var target = Fixture(P(5), new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        var srcTableWithShorterPolicy = new Table { Name = "FactSales", RefreshPolicy = P(3) };
        var diff = new TableDiff(
            Name: "FactSales",
            Classification: TableClassification.IncrementalRefreshPolicy,
            RefreshPolicyChanged: true,
            ColumnsAdded: Array.Empty<string>(), ColumnsRemoved: Array.Empty<string>(),
            ColumnsModified: Array.Empty<string>(),
            MeasuresAdded: Array.Empty<string>(), MeasuresRemoved: Array.Empty<string>(),
            MeasuresModified: Array.Empty<string>(),
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: srcTableWithShorterPolicy,
            TargetTable: target.Model.Tables["FactSales"]);

        var cs = new ChangeSet(
            Array.Empty<TablePlan>(), Array.Empty<string>(), new[] { diff },
            Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));
        var violations = gate.Check(cs, target, allowHistoryLoss: false);

        violations.Should().ContainSingle()
            .Which.LostPartitions.Should().Contain("Year2021");
    }

    [Fact]
    public void Returns_empty_when_allowHistoryLoss_true_even_on_shrink()
    {
        var target = Fixture(P(5), new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        var diff = new TableDiff(
            "FactSales", TableClassification.IncrementalRefreshPolicy, true,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), PartitionStrategy.PreserveTarget,
            new Table { Name = "FactSales", RefreshPolicy = P(3) },
            target.Model.Tables["FactSales"]);

        var cs = new ChangeSet(
            Array.Empty<TablePlan>(), Array.Empty<string>(), new[] { diff },
            Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));
        gate.Check(cs, target, allowHistoryLoss: true).Should().BeEmpty();
    }

    private static ChangeSet EmptyChangeSet() => new(
        Array.Empty<TablePlan>(), Array.Empty<string>(),
        Array.Empty<TableDiff>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>());

    private static Database Fixture(BasicRefreshPolicy policy, string[] partitions)
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var t = new Table { Name = "FactSales", RefreshPolicy = policy };
        foreach (var p in partitions)
        {
            t.Partitions.Add(new Partition
            {
                Name = p,
                Mode = ModeType.Import,
                Source = new MPartitionSource { Expression = "x" }
            });
        }
        db.Model.Tables.Add(t);
        return db;
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~HistoryLossGateTests
```
Expected: FAIL.

- [ ] **Step 2: Implement gate**

`src/Weft.Core/RefreshPolicy/HistoryLossGate.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;

namespace Weft.Core.RefreshPolicy;

public sealed record HistoryLossViolation(
    string TableName,
    IReadOnlyList<string> LostPartitions);

public sealed class HistoryLossGate
{
    private readonly RetentionCalculator _calc;

    public HistoryLossGate(RetentionCalculator calc)
    {
        _calc = calc;
    }

    public IReadOnlyList<HistoryLossViolation> Check(
        ChangeSet changeSet, Database target, bool allowHistoryLoss)
    {
        if (allowHistoryLoss) return Array.Empty<HistoryLossViolation>();

        var violations = new List<HistoryLossViolation>();
        foreach (var alter in changeSet.TablesToAlter)
        {
            if (alter.Classification != TableClassification.IncrementalRefreshPolicy) continue;
            if (!alter.RefreshPolicyChanged) continue;

            var oldPolicy = alter.TargetTable.RefreshPolicy as BasicRefreshPolicy;
            var newPolicy = alter.SourceTable.RefreshPolicy as BasicRefreshPolicy;
            if (oldPolicy is null || newPolicy is null) continue;

            if (!target.Model.Tables.ContainsName(alter.Name)) continue;
            var existing = target.Model.Tables[alter.Name].Partitions.Select(p => p.Name).ToList();

            var lost = _calc.PartitionsRemovedBy(oldPolicy, newPolicy, existing);
            if (lost.Count > 0)
                violations.Add(new HistoryLossViolation(alter.Name, lost));
        }
        return violations;
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~HistoryLossGateTests
```
Expected: 3 PASS.

```bash
git add src/Weft.Core/RefreshPolicy/HistoryLossGate.cs test/Weft.Core.Tests/RefreshPolicy/HistoryLossGateTests.cs
git commit -m "feat(core): HistoryLossGate blocks deploys that shrink the rolling window without opt-in"
```

---

### Task 14: `TokenManager` (refresh on elapsed time)

**Files:**
- Create: `src/Weft.Cli/Auth/TokenManager.cs`
- Create: `test/Weft.Cli.Tests/TokenManagerTests.cs`

A lightweight wrapper around `IAuthProvider` that caches the current `AccessToken` and re-acquires it when elapsed time exceeds a threshold (default 30 minutes, safely below Entra's typical 60-75 min TTL).

- [ ] **Step 1: Failing test**

`test/Weft.Cli.Tests/TokenManagerTests.cs`:
```csharp
using FluentAssertions;
using NSubstitute;
using Weft.Cli.Auth;
using Weft.Core.Abstractions;
using Xunit;

namespace Weft.Cli.Tests;

public class TokenManagerTests
{
    [Fact]
    public async Task First_call_acquires_and_subsequent_reuse_cached()
    {
        var inner = Substitute.For<IAuthProvider>();
        inner.GetTokenAsync(default).ReturnsForAnyArgs(
            _ => Task.FromResult(new AccessToken("t1", DateTimeOffset.UtcNow.AddHours(1))));

        var mgr = new TokenManager(inner, TimeSpan.FromMinutes(30));

        var t1 = await mgr.GetTokenAsync();
        var t2 = await mgr.GetTokenAsync();

        t1.Value.Should().Be("t1");
        t2.Value.Should().Be("t1");
        await inner.ReceivedWithAnyArgs(1).GetTokenAsync(default);
    }

    [Fact]
    public async Task Refreshes_when_elapsed_time_exceeds_threshold()
    {
        var inner = Substitute.For<IAuthProvider>();
        var calls = 0;
        inner.GetTokenAsync(default).ReturnsForAnyArgs(_ =>
        {
            calls++;
            return Task.FromResult(new AccessToken("t" + calls, DateTimeOffset.UtcNow.AddHours(1)));
        });

        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var mgr = new TokenManager(inner, TimeSpan.FromMinutes(30), clock);

        (await mgr.GetTokenAsync()).Value.Should().Be("t1");
        clock.Advance(TimeSpan.FromMinutes(10));
        (await mgr.GetTokenAsync()).Value.Should().Be("t1");    // still cached
        clock.Advance(TimeSpan.FromMinutes(25));                // total 35
        (await mgr.GetTokenAsync()).Value.Should().Be("t2");    // refreshed
    }

    private sealed class FakeClock : ISystemClock
    {
        private DateTimeOffset _now;
        public FakeClock(DateTimeOffset start) { _now = start; }
        public DateTimeOffset UtcNow => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~TokenManagerTests
```
Expected: FAIL.

- [ ] **Step 2: Implement**

`src/Weft.Cli/Auth/TokenManager.cs`:
```csharp
using Weft.Core.Abstractions;

namespace Weft.Cli.Auth;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class TokenManager : IAuthProvider
{
    private readonly IAuthProvider _inner;
    private readonly TimeSpan _refreshAfter;
    private readonly ISystemClock _clock;
    private AccessToken? _cached;
    private DateTimeOffset _acquiredAt;

    public TokenManager(IAuthProvider inner, TimeSpan refreshAfter, ISystemClock? clock = null)
    {
        _inner = inner;
        _refreshAfter = refreshAfter;
        _clock = clock ?? new SystemClock();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null && _clock.UtcNow - _acquiredAt < _refreshAfter)
            return _cached;

        _cached = await _inner.GetTokenAsync(cancellationToken);
        _acquiredAt = _clock.UtcNow;
        return _cached;
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~TokenManagerTests
```
Expected: 2 PASS.

```bash
git add src/Weft.Cli/Auth/TokenManager.cs test/Weft.Cli.Tests/TokenManagerTests.cs
git commit -m "feat(cli): TokenManager caches AccessToken and refreshes after elapsed threshold"
```

---

### Task 15: `ResolvedProfile` — single input to DeployCommand

**Files:**
- Modify: `src/Weft.Cli/Options/ProfileResolver.cs`

Promote `ResolvedProfile` (already a record in Plan 2) to carry every knob DeployCommand needs so `RunAsync`'s 13 flat parameters collapse into one.

- [ ] **Step 1: Expand `ResolvedProfile`**

Replace the existing `ResolvedProfile` record in `src/Weft.Cli/Options/ProfileResolver.cs`:
```csharp
using Weft.Auth;
using Weft.Config;

namespace Weft.Cli.Options;

public sealed record ResolvedProfile(
    string ProfileName,
    string WorkspaceUrl,
    string DatabaseName,
    string SourcePath,
    string ArtifactsDirectory,
    AuthOptions Auth,
    RefreshConfigSection Refresh,
    bool AllowDrops,
    bool AllowHistoryLoss,
    bool NoRefresh,
    bool ResetBookmarks,
    string? EffectiveDate,
    IReadOnlyDictionary<string, object?> ParameterValues,
    IReadOnlyDictionary<string, string>? ParameterCliOverrides,
    IReadOnlyList<ParameterDeclaration> ParameterDeclarations,
    HooksConfigSection Hooks);
```

- [ ] **Step 2: Extend `ProfileResolver` to build `ResolvedProfile` from `(WeftConfig | null, CLI flags)`**

Add a `Build` method to `ProfileResolver` (keep the existing `BuildAuthOptions` static method as-is for backwards compat):
```csharp
public static ResolvedProfile Build(
    WeftConfig? config,
    string profileName,
    string sourcePath,
    string artifactsDirectory,
    bool noRefresh,
    bool resetBookmarks,
    string? effectiveDate,
    Dictionary<string, string>? cliParameters,
    // CLI flag overrides (take precedence over YAML):
    string? workspaceOverride = null,
    string? databaseOverride = null,
    AuthMode? authModeOverride = null,
    string? tenantOverride = null,
    string? clientOverride = null,
    string? clientSecretOverride = null,
    string? certPathOverride = null,
    string? certPasswordOverride = null,
    string? certThumbprintOverride = null)
{
    EffectiveProfileConfig effective;
    if (config is null)
    {
        // CLI-only (pre-YAML flow). Require workspace + database + auth flags.
        var auth = BuildAuthOptions(
            authModeOverride ?? AuthMode.Interactive, tenantOverride, clientOverride,
            clientSecretOverride, certPathOverride, certPasswordOverride, certThumbprintOverride);
        effective = new EffectiveProfileConfig(
            ProfileName: profileName,
            Workspace: workspaceOverride ?? throw new InvalidOperationException("--workspace required without --config."),
            Database: databaseOverride ?? throw new InvalidOperationException("--database required without --config."),
            Auth: new AuthConfigSection(auth.Mode.ToString(), auth.TenantId, auth.ClientId,
                auth.ClientSecret, auth.CertPath, auth.CertPassword, auth.CertThumbprint,
                auth.CertStoreLocation.ToString(), auth.CertStoreName.ToString(), auth.RedirectUri),
            Refresh: new RefreshConfigSection("full", 10, 15,
                new IncrementalPolicyConfig(true, true, "preserve"),
                new DynamicPartitionStrategyConfig("newestOnly", 1)),
            AllowDrops: false,
            AllowHistoryLoss: false,
            TimeoutMinutes: 60,
            Parameters: new Dictionary<string, object?>(),
            Hooks: new HooksConfigSection(null, null, null, null, null, null));
    }
    else
    {
        effective = new ProfileMerger().Merge(config, profileName);
    }

    var authOptions = BuildAuthOptionsFromSection(effective.Auth, authModeOverride,
        clientSecretOverride, certPathOverride, certPasswordOverride, certThumbprintOverride);

    return new ResolvedProfile(
        ProfileName: effective.ProfileName,
        WorkspaceUrl: workspaceOverride ?? effective.Workspace,
        DatabaseName: databaseOverride ?? effective.Database,
        SourcePath: sourcePath,
        ArtifactsDirectory: artifactsDirectory,
        Auth: authOptions,
        Refresh: effective.Refresh,
        AllowDrops: effective.AllowDrops,
        AllowHistoryLoss: effective.AllowHistoryLoss,
        NoRefresh: noRefresh,
        ResetBookmarks: resetBookmarks,
        EffectiveDate: effectiveDate,
        ParameterValues: effective.Parameters,
        ParameterCliOverrides: cliParameters,
        ParameterDeclarations: config?.Parameters ?? Array.Empty<ParameterDeclaration>(),
        Hooks: effective.Hooks);
}

private static AuthOptions BuildAuthOptionsFromSection(
    AuthConfigSection section,
    AuthMode? overrideMode,
    string? secretOverride, string? certPathOverride,
    string? certPasswordOverride, string? certThumbprintOverride)
{
    var mode = overrideMode ?? Enum.Parse<AuthMode>(section.Mode);
    return new AuthOptions(
        Mode: mode,
        TenantId: EnvVarExpander.Expand(section.TenantId) ?? "",
        ClientId: EnvVarExpander.Expand(section.ClientId) ?? "",
        ClientSecret: secretOverride ?? EnvVarExpander.Expand(section.ClientSecret),
        CertPath: certPathOverride ?? EnvVarExpander.Expand(section.CertPath),
        CertPassword: certPasswordOverride ?? EnvVarExpander.Expand(section.CertPassword),
        CertThumbprint: certThumbprintOverride ?? EnvVarExpander.Expand(section.CertThumbprint),
        CertStoreLocation: Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreLocation>(
            section.CertStoreLocation, out var loc) ? loc : System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine,
        CertStoreName: Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreName>(
            section.CertStoreName, out var nm) ? nm : System.Security.Cryptography.X509Certificates.StoreName.My,
        RedirectUri: section.RedirectUri);
}
```

Add a `Weft.Config` project reference to `Weft.Cli`:
```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet add src/Weft.Cli/Weft.Cli.csproj reference src/Weft.Config/Weft.Config.csproj
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Cli/Options/ProfileResolver.cs src/Weft.Cli/Weft.Cli.csproj
git commit -m "feat(cli): ProfileResolver.Build produces ResolvedProfile from WeftConfig or CLI flags"
```

---

### Task 16: Rewrite `DeployCommand.RunAsync` to accept `ResolvedProfile`

**Files:**
- Modify: `src/Weft.Cli/Commands/DeployCommand.cs`
- Modify: `test/Weft.Cli.Tests/DeployCommandTests.cs`

Collapse the 13-parameter `RunAsync` into `RunAsync(ResolvedProfile profile, IAuthProvider auth, ITargetReader target, IXmlaExecutor executor, IRefreshRunner refreshRunner, IPartitionManifestStore manifestStore)`. Add hook + parameter + history-loss + bookmark wiring.

- [ ] **Step 1: Update `DeployCommand.RunAsync` signature and body**

Replace the full `RunAsync` method with:
```csharp
public static async Task<int> RunAsync(
    ResolvedProfile profile,
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
    catch (Exception ex) { Console.Error.WriteLine($"Auth failed: {ex.Message}"); return ExitCodes.AuthError; }

    // 2. Load source
    Microsoft.AnalysisServices.Tabular.Database srcDb;
    try { srcDb = ModelLoaderFactory.For(profile.SourcePath).Load(profile.SourcePath); }
    catch (Exception ex) { Console.Error.WriteLine($"Source load failed: {ex.Message}"); return ExitCodes.SourceLoadError; }

    // 2a. Apply parameters
    try
    {
        var resolver = new ParameterResolver();
        var resolutions = resolver.Resolve(
            srcDb,
            profile.ParameterDeclarations,
            profile.ParameterValues,
            profile.ParameterCliOverrides,
            paramsFileValues: null);
        resolver.Apply(srcDb, resolutions);
    }
    catch (ParameterApplicationException ex)
    {
        Console.Error.WriteLine($"Parameter resolution failed: {ex.Message}");
        return ExitCodes.DiffValidationError;
    }

    // 3. Read target
    Microsoft.AnalysisServices.Tabular.Database tgtDb;
    try { tgtDb = await targetReader.ReadAsync(profile.WorkspaceUrl, profile.DatabaseName, token, cancellationToken); }
    catch (Exception ex) { Console.Error.WriteLine($"Target read failed: {ex.Message}"); return ExitCodes.TargetReadError; }

    var preManifest = new PartitionManifestReader().Read(tgtDb);
    var prePath = manifestStore.Write(preManifest, profile.ArtifactsDirectory, "pre-partitions");
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
    if (plan.ChangeSet.TablesToDrop.Count > 0 && !profile.AllowDrops)
    {
        Console.Error.WriteLine(
            $"Refusing to drop tables without allowDrops: {string.Join(", ", plan.ChangeSet.TablesToDrop)}");
        return ExitCodes.DiffValidationError;
    }

    // 5a. Pre-flight: history-loss
    var gate = new HistoryLossGate(new RetentionCalculator());
    var historyViolations = gate.Check(plan.ChangeSet, tgtDb, profile.AllowHistoryLoss);
    if (historyViolations.Count > 0)
    {
        foreach (var v in historyViolations)
            Console.Error.WriteLine(
                $"History-loss violation on {v.TableName}: would remove {string.Join(", ", v.LostPartitions)}");
        Console.Error.WriteLine(
            "Set allowHistoryLoss: true in the profile (and pass --allow-history-loss) to proceed.");
        return ExitCodes.DiffValidationError;
    }

    // 6. Run pre-plan hook
    await RunHookAsync(profile.Hooks.PrePlan, HookPhase.PrePlan, profile, plan.ChangeSet);

    if (plan.ChangeSet.IsEmpty)
        Console.Out.WriteLine("Nothing to deploy.");

    // 7. Write plan TMSL
    Directory.CreateDirectory(profile.ArtifactsDirectory);
    var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
    var planPath = Path.Combine(profile.ArtifactsDirectory, $"{ts}-{profile.DatabaseName}-plan.tmsl");
    await File.WriteAllTextAsync(planPath, plan.TmslJson, cancellationToken);

    // 8. Pre-deploy hook + execute
    await RunHookAsync(profile.Hooks.PreDeploy, HookPhase.PreDeploy, profile, plan.ChangeSet);

    if (!plan.ChangeSet.IsEmpty)
    {
        var exec = await executor.ExecuteAsync(
            profile.WorkspaceUrl, profile.DatabaseName, token, plan.TmslJson, cancellationToken);
        foreach (var m in exec.Messages) Console.Out.WriteLine(m);
        if (!exec.Success)
        {
            await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet);
            Console.Error.WriteLine("TMSL execution failed.");
            return ExitCodes.TmslExecutionError;
        }
    }

    // 9. Post-deploy manifest + integrity gate
    var postDb = await targetReader.ReadAsync(profile.WorkspaceUrl, profile.DatabaseName, token, cancellationToken);
    var postManifest = new PartitionManifestReader().Read(postDb);
    var postPath = manifestStore.Write(postManifest, profile.ArtifactsDirectory, "post-partitions");
    Console.Out.WriteLine($"Post-deploy manifest: {postPath}");

    var droppedTables = new HashSet<string>(plan.ChangeSet.TablesToDrop, StringComparer.Ordinal);
    foreach (var (tableName, prePartitions) in preManifest.Tables)
    {
        if (droppedTables.Contains(tableName)) continue;
        if (!postManifest.Tables.TryGetValue(tableName, out var postPartitions))
        {
            await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet);
            Console.Error.WriteLine($"Partition integrity violation: table '{tableName}' missing post-deploy.");
            return ExitCodes.PartitionIntegrityError;
        }
        var postNames = postPartitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var missing = prePartitions.Where(p => !postNames.Contains(p.Name)).Select(p => p.Name).ToList();
        if (missing.Count > 0)
        {
            await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet);
            Console.Error.WriteLine(
                $"Partition integrity violation on '{tableName}': missing post-deploy: {string.Join(", ", missing)}");
            return ExitCodes.PartitionIntegrityError;
        }
    }

    // 10. Bookmark clearing + refresh hook + refresh
    await RunHookAsync(profile.Hooks.PreRefresh, HookPhase.PreRefresh, profile, plan.ChangeSet);

    if (!profile.NoRefresh && !plan.ChangeSet.IsEmpty)
    {
        var bookmarkMode = profile.ResetBookmarks ? "clearAll"
            : profile.Refresh.IncrementalPolicy?.BookmarkMode ?? "preserve";
        if (bookmarkMode != "preserve")
        {
            var tablesToClear = bookmarkMode switch
            {
                "clearAll" => plan.ChangeSet.RefreshTargets.ToList(),
                "clearForPolicyChange" => plan.ChangeSet.TablesToAlter
                    .Where(d => d.RefreshPolicyChanged).Select(d => d.Name).ToList(),
                _ => new List<string>()
            };
            if (tablesToClear.Count > 0)
            {
                var clearTmsl = new BookmarkClearer().BuildTmsl(postDb, tablesToClear);
                var clearRes = await executor.ExecuteAsync(
                    profile.WorkspaceUrl, profile.DatabaseName, token, clearTmsl, cancellationToken);
                if (!clearRes.Success)
                {
                    await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet);
                    Console.Error.WriteLine("Bookmark clearing failed.");
                    return ExitCodes.RefreshError;
                }
            }
        }

        var req = new RefreshRequest(profile.WorkspaceUrl, profile.DatabaseName, token, plan.ChangeSet, profile.EffectiveDate);
        var rrx = await refreshRunner.RefreshAsync(req,
            progress: new Progress<string>(line => Console.Out.WriteLine(line)),
            cancellationToken: cancellationToken);
        if (!rrx.Success)
        {
            await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet);
            Console.Error.WriteLine("Refresh failed.");
            return ExitCodes.RefreshError;
        }
    }

    await RunHookAsync(profile.Hooks.PostRefresh, HookPhase.PostRefresh, profile, plan.ChangeSet);
    await RunHookAsync(profile.Hooks.PostDeploy, HookPhase.PostDeploy, profile, plan.ChangeSet);

    // 11. Receipt
    var receipt = new
    {
        ts, profile.DatabaseName, profile.WorkspaceUrl, profile.ProfileName,
        add = plan.ChangeSet.TablesToAdd.Select(t => t.Name).ToArray(),
        drop = plan.ChangeSet.TablesToDrop.ToArray(),
        alter = plan.ChangeSet.TablesToAlter.Select(t => t.Name).ToArray(),
        unchanged = plan.ChangeSet.TablesUnchanged.ToArray(),
        preManifest = prePath,
        postManifest = postPath,
        planTmsl = planPath,
        refreshSkipped = profile.NoRefresh
    };
    var receiptPath = Path.Combine(profile.ArtifactsDirectory, $"{ts}-{profile.DatabaseName}-receipt.json");
    await File.WriteAllTextAsync(receiptPath,
        JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
    Console.Out.WriteLine($"Receipt: {receiptPath}");
    return ExitCodes.Success;
}

private static async Task RunHookAsync(string? command, HookPhase phase, ResolvedProfile profile, ChangeSet changeSet)
{
    if (string.IsNullOrWhiteSpace(command)) return;
    try
    {
        var ctx = new HookContext(profile.ProfileName, profile.WorkspaceUrl, profile.DatabaseName, phase,
            ChangeSetSnapshot.From(changeSet));
        var result = await new HookRunner().RunAsync(new HookDefinition(phase, command), ctx);
        if (!string.IsNullOrEmpty(result.Stdout)) Console.Out.WriteLine(result.Stdout);
        if (!string.IsNullOrEmpty(result.Stderr)) Console.Error.WriteLine(result.Stderr);
        if (result.ExitCode != 0)
            Console.Error.WriteLine($"Hook '{phase}' exited {result.ExitCode} (non-fatal).");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Hook '{phase}' failed: {ex.Message} (continuing).");
    }
}
```

Add `using Weft.Core.Hooks;`, `using Weft.Core.Parameters;`, `using Weft.Core.RefreshPolicy;` to the top of the file.

- [ ] **Step 2: Update `Build()` to construct `ResolvedProfile` and call new `RunAsync`**

Replace the `SetAction` body in `Build()`:
```csharp
cmd.SetAction(async (parse, ct) =>
{
    WeftConfig? config = null;
    var configPath = parse.GetValue(configOpt);
    if (!string.IsNullOrEmpty(configPath))
    {
        try { config = YamlConfigLoader.LoadFromFile(configPath); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Config load failed: {ex.Message}");
            return ExitCodes.ConfigError;
        }
    }

    ResolvedProfile profile;
    try
    {
        profile = ProfileResolver.Build(
            config: config,
            profileName: parse.GetValue(targetOpt) ?? "default",
            sourcePath: parse.GetValue(src) ?? config?.Source?.Path ?? "",
            artifactsDirectory: parse.GetValue(artifacts)!,
            noRefresh: parse.GetValue(noRefresh),
            resetBookmarks: parse.GetValue(resetBookmarks),
            effectiveDate: parse.GetValue(effectiveDate),
            cliParameters: null,
            workspaceOverride: parse.GetValue(workspace),
            databaseOverride: parse.GetValue(database),
            authModeOverride: parse.GetValue(authMode),
            tenantOverride: parse.GetValue(tenant),
            clientOverride: parse.GetValue(client),
            clientSecretOverride: parse.GetValue(clientSecret),
            certPathOverride: parse.GetValue(certPath),
            certPasswordOverride: parse.GetValue(certPwd),
            certThumbprintOverride: parse.GetValue(certThumb));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Profile resolution failed: {ex.Message}");
        return ExitCodes.ConfigError;
    }

    var innerAuth = AuthProviderFactory.Create(profile.Auth);
    var tokenMgr = new TokenManager(innerAuth, TimeSpan.FromMinutes(30));
    var sharedExecutor = new XmlaExecutor();

    return await RunAsync(
        profile,
        tokenMgr,
        new TargetReader(),
        sharedExecutor,
        new RefreshRunner(sharedExecutor),
        new FilePartitionManifestStore(),
        ct);
});
```

Also add `--config` and `--target` options to the `Build()` method:
```csharp
var configOpt = new Option<string?>("--config") { Description = "Path to weft.yaml." };
var targetOpt = new Option<string?>("--target") { Description = "Profile name in weft.yaml." };
cmd.Options.Add(configOpt); cmd.Options.Add(targetOpt);
// make --workspace, --database, --source optional when --config is provided (no change needed — System.CommandLine
// validates on build, but consumers get a clear error from ProfileResolver).
```

Mark `--source`, `--workspace`, `--database` as `Required = false` (update `CommonOptions` if needed — they were `Required = true` in Plan 2; now they become optional when a config is passed. This is a breaking change for the CLI signature but aligns with Plan 3's config-driven flow).

- [ ] **Step 3: Fix existing tests to pass `ResolvedProfile` instead of flat params**

Rewrite the 4 existing tests in `test/Weft.Cli.Tests/DeployCommandTests.cs` to build a `ResolvedProfile` directly (no config file needed):
```csharp
private static ResolvedProfile MakeProfile(string src, string artifactsDir, bool allowDrops = false) =>
    new(
        ProfileName: "test",
        WorkspaceUrl: "powerbi://x",
        DatabaseName: "TinyStatic",
        SourcePath: src,
        ArtifactsDirectory: artifactsDir,
        Auth: new Weft.Auth.AuthOptions(Weft.Auth.AuthMode.Interactive, "t", "c"),
        Refresh: new Weft.Config.RefreshConfigSection("full", 10, 15,
            new Weft.Config.IncrementalPolicyConfig(true, true, "preserve"),
            new Weft.Config.DynamicPartitionStrategyConfig("newestOnly", 1)),
        AllowDrops: allowDrops,
        AllowHistoryLoss: false,
        NoRefresh: false,
        ResetBookmarks: false,
        EffectiveDate: null,
        ParameterValues: new Dictionary<string, object?>(),
        ParameterCliOverrides: null,
        ParameterDeclarations: Array.Empty<Weft.Config.ParameterDeclaration>(),
        Hooks: new Weft.Config.HooksConfigSection(null, null, null, null, null, null));
```

Update each test's `DeployCommand.RunAsync` call to use `MakeProfile(...)` as the first argument, drop the removed parameters, keep the interface arguments. Example for the happy-path test:
```csharp
var exit = await DeployCommand.RunAsync(
    MakeProfile(src, artifacts),
    auth: CliTestHost.MakeAuth(),
    targetReader: CliTestHost.StubTarget(tgtDb),
    executor: CliTestHost.MakeExecutor(),
    refreshRunner: CliTestHost.MakeRefreshRunner(),
    manifestStore: new FilePartitionManifestStore());
```

For the `allowDrops` test, pass `allowDrops: true` when making the profile — wait, actually that test expects the drop to be REFUSED, so pass `allowDrops: false`. The existing test logic is correct; just route it through the profile.

Add `Weft.Config` project reference to `Weft.Cli.Tests`:
```bash
dotnet add test/Weft.Cli.Tests/Weft.Cli.Tests.csproj reference src/Weft.Config/Weft.Config.csproj
```

- [ ] **Step 4: Build + run + commit**

```bash
dotnet build
dotnet test --filter FullyQualifiedName~DeployCommandTests
```
Expected: 4 PASS.

```bash
git add src/Weft.Cli/Commands/DeployCommand.cs src/Weft.Cli/Options/ProfileResolver.cs test/Weft.Cli.Tests/DeployCommandTests.cs test/Weft.Cli.Tests/Weft.Cli.Tests.csproj
git commit -m "refactor(cli): DeployCommand accepts ResolvedProfile; wire config + hooks + params + history-loss + bookmark-mode"
```

---

### Task 17: `--config` / `--target` options for other commands

**Files:**
- Modify: `src/Weft.Cli/Options/CommonOptions.cs` (add `ConfigFileOption`, `TargetProfileOption`)
- Modify: `src/Weft.Cli/Commands/PlanCommand.cs`, `RefreshCommand.cs`, `RestoreHistoryCommand.cs`

- [ ] **Step 1: Add common options**

In `src/Weft.Cli/Options/CommonOptions.cs`:
```csharp
public static Option<string?> ConfigFileOption() =>
    new("--config") { Description = "Path to weft.yaml (optional; falls back to CLI flags)." };

public static Option<string?> TargetProfileOption() =>
    new("--target") { Description = "Profile name in weft.yaml." };
```

- [ ] **Step 2: Add them to the three commands**

For each of PlanCommand, RefreshCommand, RestoreHistoryCommand, add:
```csharp
var config = CommonOptions.ConfigFileOption();
var target = CommonOptions.TargetProfileOption();
cmd.Options.Add(config); cmd.Options.Add(target);
```

PlanCommand continues to use `--target-snapshot` (different concept — a .bim file on disk). Refresh and RestoreHistory can honor `--config`/`--target` to fill in workspace/database/auth if not passed as flags (same pattern as DeployCommand's `SetAction` body).

Minimal impl: if `--config` and `--target` are provided, load the config and populate workspace/database/auth fallbacks. Otherwise, require workspace/database/auth flags (existing behavior).

Update each command's `SetAction` to match DeployCommand's pattern. (Copy the config-loading block.)

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add src/Weft.Cli/Options/CommonOptions.cs src/Weft.Cli/Commands/
git commit -m "feat(cli): --config and --target options on plan/refresh/restore-history"
```

---

### Task 18: Wire `IConsoleWriter` through `DeployCommand`

**Files:**
- Modify: `src/Weft.Cli/Commands/DeployCommand.cs`

Per Plan 2's final review, `IConsoleWriter` is unused. Wire it through DeployCommand's `Console.Out.WriteLine` / `Console.Error.WriteLine` call sites, accept an optional `IConsoleWriter` parameter, default to `HumanConsoleWriter`. `--log-format json` selects `JsonConsoleWriter`.

- [ ] **Step 1: Extend `DeployCommand.RunAsync` signature**

Add `IConsoleWriter? writer = null` as the last parameter; default to `new HumanConsoleWriter()`. Replace every `Console.Out.WriteLine(x)` with `writer.Info(x)` (or `Plan(...)` for multi-line summaries) and every `Console.Error.WriteLine(x)` with `writer.Error(x)`.

```csharp
public static async Task<int> RunAsync(
    ResolvedProfile profile,
    IAuthProvider auth,
    ITargetReader targetReader,
    IXmlaExecutor executor,
    IRefreshRunner refreshRunner,
    IPartitionManifestStore manifestStore,
    CancellationToken cancellationToken = default,
    IConsoleWriter? writer = null)
{
    writer ??= new HumanConsoleWriter();
    // replace all Console.Out.WriteLine / Console.Error.WriteLine calls...
}
```

- [ ] **Step 2: Wire `--log-format` in `Build()`**

In the `Build()` `SetAction` body, after parsing:
```csharp
var logFormat = parse.GetValue(CommonOptions.LogFormatOption()) ?? "human";
IConsoleWriter writer = logFormat.Equals("json", StringComparison.OrdinalIgnoreCase)
    ? new JsonConsoleWriter()
    : new HumanConsoleWriter();
```

Pass `writer` through to `RunAsync`.

Also add `LogFormatOption()` to the command's options if not already present.

Remove the XML-doc from `IConsoleWriter.cs` etc that says "unused by commands" — it's used now.

- [ ] **Step 3: Build + run existing tests + commit**

```bash
dotnet build
dotnet test
```
Expected: all pass (no new tests in this task; later tasks add output-format tests).

```bash
git add src/Weft.Cli/Commands/DeployCommand.cs src/Weft.Cli/Output/IConsoleWriter.cs src/Weft.Cli/Output/HumanConsoleWriter.cs src/Weft.Cli/Output/JsonConsoleWriter.cs
git commit -m "feat(cli): wire IConsoleWriter + --log-format through DeployCommand"
```

---

### Task 19: End-to-end CLI test driven by `weft.yaml`

**Files:**
- Create: `test/Weft.Cli.Tests/fixtures/weft-e2e.yaml`
- Create: `test/Weft.Cli.Tests/DeployCommandConfigTests.cs`

Exercise the full `weft.yaml → ResolvedProfile → DeployCommand` path in-process against the Plan-1 `tiny-static.bim` fixture.

- [ ] **Step 1: Fixture**

`test/Weft.Cli.Tests/fixtures/weft-e2e.yaml`:
```yaml
version: 1

source:
  format: bim
  path: tiny-static.bim      # resolved relative to --source flag in test

defaults:
  allowDrops: true
  allowHistoryLoss: false

profiles:
  test:
    workspace: "powerbi://x"
    database: TinyStatic
    auth:
      mode: Interactive
      tenantId: tenant
      clientId: client
    parameters: {}
```

Add copy-to-output in `Weft.Cli.Tests.csproj`:
```xml
<ItemGroup>
  <None Update="fixtures\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: Failing test**

`test/Weft.Cli.Tests/DeployCommandConfigTests.cs`:
```csharp
using FluentAssertions;
using Weft.Cli.Commands;
using Weft.Cli.Options;
using Weft.Cli.Tests.Helpers;
using Weft.Config;
using Weft.Core.Loading;
using Weft.Xmla;
using Xunit;

namespace Weft.Cli.Tests;

public class DeployCommandConfigTests
{
    [Fact]
    public async Task Deploys_using_config_file_and_target_profile()
    {
        var fixturesRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models");
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "weft-e2e.yaml");
        var bimPath = Path.Combine(fixturesRoot, "tiny-static.bim");

        var config = YamlConfigLoader.LoadFromFile(yamlPath);
        var profile = ProfileResolver.Build(
            config: config,
            profileName: "test",
            sourcePath: bimPath,
            artifactsDirectory: Directory.CreateTempSubdirectory().FullName,
            noRefresh: true,
            resetBookmarks: false,
            effectiveDate: null,
            cliParameters: null);

        var tgtDb = new BimFileLoader().Load(bimPath);

        var exit = await DeployCommand.RunAsync(
            profile,
            auth: CliTestHost.MakeAuth(),
            targetReader: CliTestHost.StubTarget(tgtDb),
            executor: CliTestHost.MakeExecutor(),
            refreshRunner: CliTestHost.MakeRefreshRunner(),
            manifestStore: new FilePartitionManifestStore());

        exit.Should().Be(ExitCodes.Success);
        Directory.GetFiles(profile.ArtifactsDirectory, "*-receipt.json").Should().NotBeEmpty();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~DeployCommandConfigTests
```
Expected: PASS (config-driven deploy with empty change set still writes manifests + receipt).

- [ ] **Step 3: Commit**

```bash
git add test/Weft.Cli.Tests/fixtures/ test/Weft.Cli.Tests/DeployCommandConfigTests.cs test/Weft.Cli.Tests/Weft.Cli.Tests.csproj
git commit -m "test(cli): end-to-end deploy via weft.yaml + target profile"
```

---

### Task 20: Final verification + tag

- [ ] **Step 1: Full pipeline**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet clean
dotnet build -warnaserror
dotnet test
```
Expected:
- 0 build warnings, 0 errors.
- Test count well over 90 (Plan 1+2 had 75; Plan 3 adds ~25 tests).
- Integration test still skipped locally.

- [ ] **Step 2: Tag**

```bash
git tag -a plan-3-config-parameters-hooks-complete -m "Weft Plan 3: Config + Parameters + Hooks complete"
git log --oneline | head -40
```

---

## Spec coverage check (run after Task 20)

| Spec section | Plan-3 task(s) |
|---|---|
| §5.7 ConfigLoader | Tasks 1–5 |
| §5.8 ParameterResolver | Tasks 7, 8, 9 |
| §5.9 HookRunner | Task 10 |
| §6 step 6 history-loss pre-flight | Tasks 12, 13, 16 |
| §7 Parameter Management (priority, validation) | Task 9 |
| §7A.4 incremental-refresh config | Task 16 (wired via ResolvedProfile / profile.Refresh) |
| §7A.7 `--reset-bookmarks` | Tasks 11, 16 |
| §7A.8 `allowHistoryLoss` pin | Tasks 5, 13, 16 |
| §8 Full config schema | Tasks 2, 3, 6 (JSON Schema) |
| §9 exit codes (config error) | Task 16 (`ExitCodes.ConfigError` returned on YAML failure) |
| §9.2 history-loss + manifest safety mechanisms | Tasks 13, 16 |
| §9.3 observability (`--log-format`) | Task 18 |
| `TokenManager` (Plan-2 carry-over) | Task 14 |
| `IConsoleWriter` wiring (Plan-2 carry-over) | Task 18 |

Items NOT in this plan and tracked elsewhere:
- `--params-file` flag (Plan 3 resolver accepts it as a map but the CLI doesn't wire a `--params-file` option yet — add in a follow-up task if needed).
- `overrides.<profile>.dataSources` (data-source connection-string overrides) — wired via config loading, but not yet applied to source `Database`. Plan 4.
- `parameters.strictMode` (fail on extra declarations) — not implemented; declaration-vs-model drift is currently a WARN in spec. Plan 4.
- Samples + README + docs — Plan 4.
- TeamCity / Octopus step templates — Plan 4.

---

## Done criteria for Plan 3

- [ ] All 20 tasks committed.
- [ ] `dotnet test` passes, 0 warnings, 0 errors.
- [ ] `weft deploy --config weft.yaml --target prod` resolves profile + parameters, runs hooks, enforces history-loss gate, honors bookmark mode, writes receipt with profile name.
- [ ] `TokenManager` caches/refreshes tokens on long deploys.
- [ ] `IConsoleWriter` wired through `DeployCommand` with `--log-format json` option.
- [ ] JSON Schema for `weft.yaml` committed at `schemas/weft.schema.json`.
- [ ] Tag `plan-3-config-parameters-hooks-complete` exists.

When all items above are checked, Plan 3 is complete and Plan 4 (Packaging, CI/CD, Docs, Samples) can begin.
