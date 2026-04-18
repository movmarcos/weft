# Weft Plan 4 — Packaging + CI/CD + Docs + Samples Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Plan-1-to-3 branch into a shippable open-source release: fix Plan-3 review deferrals, add SemVer-driven release automation, ship `weft` as a cross-platform CLI binary and `Weft.Core` as a NuGet package, provide TeamCity + Octopus + GitHub Actions CI/CD integration, write end-user documentation, and build four runnable samples. End-state: a tagged `v1.0.0` GitHub release with binaries for `win-x64` / `linux-x64` / `osx-arm64`, a publishable NuGet package, and everything a newcomer needs to clone the repo and run `weft deploy --config weft.yaml --target dev` against their own Power BI workspace.

**Architecture:** No new runtime code beyond the five Plan-3 cleanups. New directories: `.github/workflows/` (GitHub Actions), `build/teamcity/` (Kotlin DSL), `build/octopus/step-templates/` (JSON), `samples/` (four self-contained example projects), `docs/` (user-facing walkthroughs). `Weft.Core.csproj` gains pack metadata. `GitVersion.yml` + `release-please-config.json` drive SemVer and changelogs. The final commit merges `feature/plan-1-core-mvp` into `master` and cuts `v1.0.0`.

**Tech Stack:** .NET 10 publish RIDs (win-x64, linux-x64, osx-arm64), GitVersion 6.x, release-please 4.x, GitHub Actions, TeamCity Kotlin DSL (Kotlin 1.9), Octopus step templates (JSON v1), Markdown docs with sensible internal anchors.

**Reference spec:** `docs/superpowers/specs/2026-04-17-weft-design.md`. Sections this plan implements:
- §11 Repo layout (final shape)
- §12 CI/CD Integration (TeamCity + Octopus + GitHub Actions + release/versioning)
- §13 Out-of-scope items — all ship as Plan 4 deliverables EXCEPT the explicit non-goals (Managed Identity auth, PBIX deployment, AAS/SSAS on-prem)
- Docs list from §11: `getting-started.md`, `authentication.md`, `parameters.md`, `partition-preservation.md`, `incremental-refresh.md`, `restore-history.md`, `hooks.md`, `troubleshooting.md`

**Reference — Plan-3 review deferrals (Tasks 1–5 of this plan):**
1. Auth-mode override ambiguity in `refresh`/`restore-history` (`--auth Interactive` indistinguishable from default).
2. `ParameterApplicationException` maps to `DiffValidationError`; deserves its own exit code.
3. `HookRunner.SplitCommand` is whitespace-split, not shell-parsed — document the limitation.
4. `EffectiveProfileConfig.TimeoutMinutes` computed but not wired to refresh poll budget.
5. `HookRunner` sequential stdout/stderr drain can deadlock on large stderr.

**Out of this plan (no follow-up plan currently):**
- Managed Identity auth, PBIX reports, Azure AS / SSAS on-prem (explicit §13 non-goals).
- Public plugin system (§13 non-goal).
- Rollback command (§13 non-goal — re-deploy an older git revision).

---

## File structure (locked in by this plan)

```
weft/
├── README.md                                          # NEW
├── CONTRIBUTING.md                                    # NEW
├── CHANGELOG.md                                       # NEW (release-please managed)
├── GitVersion.yml                                     # NEW
├── release-please-config.json                         # NEW
├── release-please-manifest.json                       # NEW (auto-written)
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                                     # NEW: PR build + test
│   │   ├── release-please.yml                         # NEW: automated releases on main
│   │   └── release-artifacts.yml                      # NEW: attach binaries + nupkg on tag
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md                              # NEW
│   │   └── feature_request.md                         # NEW
│   └── pull_request_template.md                       # NEW
├── build/
│   ├── teamcity/
│   │   └── settings.kts                               # NEW
│   └── octopus/
│       ├── README.md                                  # NEW
│       └── step-templates/
│           ├── weft-deploy.json                       # NEW
│           └── weft-refresh.json                      # NEW
├── docs/
│   ├── getting-started.md                             # NEW
│   ├── authentication.md                              # NEW
│   ├── parameters.md                                  # NEW
│   ├── partition-preservation.md                      # NEW
│   ├── incremental-refresh.md                         # NEW
│   ├── restore-history.md                             # NEW
│   ├── hooks.md                                       # NEW
│   └── troubleshooting.md                             # NEW
├── samples/
│   ├── 01-simple-bim/
│   │   ├── model.bim
│   │   ├── weft.yaml
│   │   └── README.md
│   ├── 02-tabular-editor-folder/
│   │   ├── Model/
│   │   │   ├── database.json
│   │   │   └── tables/{DimDate,FactSales}.json
│   │   ├── weft.yaml
│   │   └── README.md
│   ├── 03-with-parameters/
│   │   ├── model.bim
│   │   ├── weft.yaml
│   │   └── README.md
│   └── 04-full-pipeline/
│       ├── model.bim
│       ├── weft.yaml
│       ├── hooks/
│       │   ├── notify.sh
│       │   └── notify.ps1
│       └── README.md
├── src/
│   ├── Weft.Core/
│   │   └── Weft.Core.csproj                           # MODIFIED: pack metadata
│   └── Weft.Cli/
│       ├── Commands/
│       │   ├── RefreshCommand.cs                      # MODIFIED: nullable --auth
│       │   └── RestoreHistoryCommand.cs               # MODIFIED: nullable --auth
│       ├── ExitCodes.cs                               # MODIFIED: add ParameterError=10
│       └── Commands/
│           └── DeployCommand.cs                       # MODIFIED: use ParameterError + TimeoutMinutes
└── test/
    └── (no new test projects — extend existing ones for Tasks 1-5)
```

---

## Tasks

### Task 1: Auth-mode override nullability (Plan-3 deferral)

**Files:**
- Modify: `src/Weft.Cli/Options/CommonOptions.cs` (make `AuthModeOption` nullable)
- Modify: `src/Weft.Cli/Commands/RefreshCommand.cs` (use nullable to distinguish "explicit --auth" from "unset")
- Modify: `src/Weft.Cli/Commands/RestoreHistoryCommand.cs` (same)
- Modify: `src/Weft.Cli/Commands/DeployCommand.cs` (same in `Build`)

- [ ] **Step 1: Change `AuthModeOption` to return `Option<AuthMode?>`**

Edit `src/Weft.Cli/Options/CommonOptions.cs`:
```csharp
public static Option<AuthMode?> AuthModeOption() =>
    new("--auth") { Description = "Auth mode (overrides config)." };
```
Remove the `DefaultValueFactory = _ => AuthMode.Interactive`.

- [ ] **Step 2: Update `DeployCommand.Build` to fall back to Interactive only when neither config nor CLI provides auth**

In `src/Weft.Cli/Commands/DeployCommand.cs`, find the `cmd.SetAction` body. The `authModeOverride: parse.GetValue(authMode)` line now passes `AuthMode?`. This already matches `ProfileResolver.Build`'s `AuthMode? authModeOverride` parameter — no change needed inside DeployCommand.

But `ProfileResolver.Build`'s null-handling branch for `config is null` uses `authModeOverride ?? AuthMode.Interactive` — keep that. The non-null-config branch uses `overrideMode ?? Enum.Parse<AuthMode>(section.Mode)` — this is correct (explicit CLI override wins, else YAML mode wins).

- [ ] **Step 3: Fix `RefreshCommand.cs` auth-mode resolution**

Replace the ambiguous line in `RefreshCommand.cs`:
```csharp
var mode = parse.GetValue(authMode) == AuthMode.Interactive ? Enum.Parse<AuthMode>(eff.Auth.Mode) : parse.GetValue(authMode);
```
With:
```csharp
var mode = parse.GetValue(authMode) ?? Enum.Parse<AuthMode>(eff.Auth.Mode);
```

In the else branch (no config), the old `parse.GetValue(authMode)` was non-nullable. Now it's nullable; fix:
```csharp
authOpts = ProfileResolver.BuildAuthOptions(
    parse.GetValue(authMode) ?? AuthMode.Interactive,
    parse.GetValue(tenant), parse.GetValue(client),
    parse.GetValue(clientSecret), parse.GetValue(certPath),
    parse.GetValue(certPwd), parse.GetValue(certThumb));
```

- [ ] **Step 4: Same fix in `RestoreHistoryCommand.cs`**

Replace the identical pattern with the same two changes.

- [ ] **Step 5: Build + test**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet build
dotnet test
```
Expected: all 107 tests still pass, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/Weft.Cli/Options/CommonOptions.cs src/Weft.Cli/Commands/RefreshCommand.cs src/Weft.Cli/Commands/RestoreHistoryCommand.cs
git commit -m "fix(cli): nullable --auth distinguishes explicit override from default"
```

---

### Task 2: Dedicated `ParameterError` exit code (Plan-3 deferral)

**Files:**
- Modify: `src/Weft.Cli/ExitCodes.cs`
- Modify: `src/Weft.Cli/Commands/DeployCommand.cs`
- Modify: `test/Weft.Cli.Tests/ExitCodesTests.cs`

- [ ] **Step 1: Add `ParameterError` constant**

`src/Weft.Cli/ExitCodes.cs`, append after `PartitionIntegrityError = 9`:
```csharp
public const int ParameterError = 10;
```

- [ ] **Step 2: Return `ParameterError` from DeployCommand param resolution catch**

In `src/Weft.Cli/Commands/DeployCommand.cs`, in the `RunAsync` method, find the `catch (ParameterApplicationException ex)` block (phase 2a) and change:
```csharp
return ExitCodes.DiffValidationError;
```
To:
```csharp
return ExitCodes.ParameterError;
```

- [ ] **Step 3: Extend `ExitCodesTests.cs`**

Append to the existing test:
```csharp
[Fact]
public void ParameterError_is_10() => ExitCodes.ParameterError.Should().Be(10);
```

- [ ] **Step 4: Build + test + commit**

```bash
dotnet test
```
Expected: PASS.

```bash
git add src/Weft.Cli/ExitCodes.cs src/Weft.Cli/Commands/DeployCommand.cs test/Weft.Cli.Tests/ExitCodesTests.cs
git commit -m "fix(cli): dedicated ParameterError exit code 10 for parameter-resolution failures"
```

---

### Task 3: `HookRunner` concurrent stdout/stderr drain (Plan-3 deferral)

**Files:**
- Modify: `src/Weft.Core/Hooks/HookRunner.cs`
- Modify: `test/Weft.Core.Tests/Hooks/HookRunnerTests.cs` (add large-output test)

- [ ] **Step 1: Switch to concurrent reads**

In `src/Weft.Core/Hooks/HookRunner.cs`, replace the sequential drain in `RunAsync`:
```csharp
var stdout = await p.StandardOutput.ReadToEndAsync(ct);
var stderr = await p.StandardError.ReadToEndAsync(ct);
await p.WaitForExitAsync(ct);
```
With concurrent:
```csharp
var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
var stderrTask = p.StandardError.ReadToEndAsync(ct);
await Task.WhenAll(stdoutTask, stderrTask);
await p.WaitForExitAsync(ct);
var stdout = await stdoutTask;
var stderr = await stderrTask;
```

- [ ] **Step 2: Failing test for large stderr**

Append to `HookRunnerTests.cs`:
```csharp
[Fact]
public async Task Captures_large_stderr_without_deadlock()
{
    if (OperatingSystem.IsWindows()) return;

    var runner = new HookRunner();
    var ctx = new HookContext("t", "x", "D", HookPhase.PreDeploy,
        new ChangeSetSnapshot(Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>()));

    // Produce ~200KB of stderr to exercise the pipe buffer.
    var cmd = "/bin/sh -c printf_%.0s_X_100000_>&2";
    // Actually, the tokenizer whitespace-splits. Use a helper script.
    var script = Path.GetTempFileName();
    File.WriteAllText(script, "#!/bin/sh\nhead -c 200000 /dev/urandom >&2\necho ok\n");
    File.SetUnixFileMode(script,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

    try
    {
        var result = await runner.RunAsync(new HookDefinition(HookPhase.PreDeploy, script), ctx);
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().StartWith("ok");
        result.Stderr.Length.Should().BeGreaterThan(100_000);
    }
    finally { File.Delete(script); }
}
```

- [ ] **Step 3: Build + test + commit**

```bash
dotnet test --filter FullyQualifiedName~HookRunnerTests
```
Expected: 4 PASS (3 existing + 1 new, or 4 skip on Windows).

```bash
git add src/Weft.Core/Hooks/HookRunner.cs test/Weft.Core.Tests/Hooks/HookRunnerTests.cs
git commit -m "fix(core): HookRunner drains stdout and stderr concurrently to avoid deadlock"
```

---

### Task 4: Wire `TimeoutMinutes` into refresh poll budget (Plan-3 deferral)

**Files:**
- Modify: `src/Weft.Cli/Options/ProfileResolver.cs` (add `TimeoutMinutes` to `ResolvedProfile`)
- Modify: `src/Weft.Cli/Commands/DeployCommand.cs` (apply a `CancellationTokenSource` with the timeout to refresh)

- [ ] **Step 1: Extend `ResolvedProfile` with `TimeoutMinutes`**

In `src/Weft.Cli/Options/ProfileResolver.cs`, add `int TimeoutMinutes` to the `ResolvedProfile` record (as the final field):
```csharp
public sealed record ResolvedProfile(
    string ProfileName,
    // ... existing fields ...
    HooksConfigSection Hooks,
    int TimeoutMinutes);
```

Update `ProfileResolver.Build` return value to pass `effective.TimeoutMinutes` (always 60 in the no-config branch):
```csharp
return new ResolvedProfile(
    // ... existing fields ...,
    Hooks: effective.Hooks,
    TimeoutMinutes: effective.TimeoutMinutes);
```

Both branches of `Build` compute `effective` with `TimeoutMinutes` already — they just need to be passed through.

- [ ] **Step 2: Apply the timeout around the refresh call in `DeployCommand.RunAsync`**

Wrap the `refreshRunner.RefreshAsync(...)` call in a combined `CancellationTokenSource`:
```csharp
using var refreshTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
refreshTimeoutCts.CancelAfter(TimeSpan.FromMinutes(profile.TimeoutMinutes));

var rrx = await refreshRunner.RefreshAsync(req,
    progress: new Progress<string>(line => writer.Info(line)),
    cancellationToken: refreshTimeoutCts.Token);
```

Do the same around the `executor.ExecuteAsync(clearTmsl, ...)` call.

- [ ] **Step 3: Update `DeployCommandTests` `MakeProfile` helper to pass `TimeoutMinutes`**

In `test/Weft.Cli.Tests/DeployCommandTests.cs`, add `TimeoutMinutes: 60` to the `MakeProfile` helper's `ResolvedProfile` construction.

Do the same in `test/Weft.Integration.Tests/EndToEndDeployTests.cs` if that test constructs `ResolvedProfile` directly.

- [ ] **Step 4: Build + test + commit**

```bash
dotnet build
dotnet test
```
Expected: all tests pass.

```bash
git add src/Weft.Cli/Options/ProfileResolver.cs src/Weft.Cli/Commands/DeployCommand.cs test/Weft.Cli.Tests/DeployCommandTests.cs test/Weft.Integration.Tests/EndToEndDeployTests.cs
git commit -m "fix(cli): wire profile.TimeoutMinutes into refresh + bookmark-clear cancellation"
```

---

### Task 5: `HookRunner` command-parsing documentation (Plan-3 deferral)

**Files:**
- Modify: `src/Weft.Core/Hooks/HookRunner.cs` (XML-doc the `SplitCommand` limitation)

- [ ] **Step 1: Add XML-doc remark**

Add to the `HookRunner` class:
```csharp
/// <summary>
/// Spawns the hook executable as a child process with the <see cref="HookContext"/>
/// serialized to JSON on stdin. Child processes inherit sanitized environment —
/// known secret-bearing variables (<c>WEFT_CLIENT_SECRET</c>, <c>WEFT_CERT_PASSWORD</c>,
/// <c>WEFT_CERT_THUMBPRINT</c>, and any <c>WEFT_PARAM_*</c> containing
/// PASSWORD/SECRET/KEY/TOKEN) are removed before <see cref="Process.Start()"/>.
/// </summary>
/// <remarks>
/// The <see cref="HookDefinition.Command"/> string is WHITESPACE-TOKENIZED, not shell-parsed.
/// Quoted arguments are NOT supported. If a hook needs complex arguments (spaces, pipes,
/// redirects), point the command at a shell script and handle parsing there:
/// <code>
/// hooks:
///   preDeploy: ./hooks/notify.sh       # ← shell script handles its own args
/// </code>
/// </remarks>
public sealed class HookRunner
```

And on `SplitCommand`:
```csharp
// Whitespace-split tokenizer — see class-level remarks for the rationale.
private static (string FileName, string[] Args) SplitCommand(string command)
```

- [ ] **Step 2: Commit (no new test needed — doc-only)**

```bash
git add src/Weft.Core/Hooks/HookRunner.cs
git commit -m "docs(core): HookRunner XML-doc the whitespace-tokenizer limitation and env sanitization"
```

---

### Task 6: `GitVersion.yml`

**Files:**
- Create: `GitVersion.yml`

- [ ] **Step 1: Author config**

`GitVersion.yml`:
```yaml
mode: ContinuousDelivery
branches:
  main:
    tag: ''
    increment: Minor
  master:
    tag: ''
    increment: Minor
  feature:
    regex: ^features?[/-]
    tag: alpha
    increment: Inherit
  pull-request:
    regex: ^(pull|pull\-requests|pr)[/-]
    tag: PullRequest
    increment: Inherit
ignore:
  sha: []
merge-message-formats: {}
```

- [ ] **Step 2: Commit**

```bash
git add GitVersion.yml
git commit -m "chore(release): add GitVersion.yml for SemVer-from-git-history"
```

---

### Task 7: `release-please` configuration

**Files:**
- Create: `release-please-config.json`
- Create: `release-please-manifest.json`

- [ ] **Step 1: Config**

`release-please-config.json`:
```json
{
  "release-type": "simple",
  "bump-minor-pre-major": true,
  "bump-patch-for-minor-pre-major": true,
  "include-component-in-tag": false,
  "separate-pull-requests": false,
  "changelog-sections": [
    { "type": "feat", "section": "Features" },
    { "type": "fix", "section": "Bug Fixes" },
    { "type": "perf", "section": "Performance" },
    { "type": "refactor", "section": "Refactoring" },
    { "type": "docs", "section": "Documentation" },
    { "type": "chore", "section": "Chores", "hidden": true },
    { "type": "test", "section": "Tests", "hidden": true }
  ],
  "packages": {
    ".": {
      "package-name": "weft",
      "component": "weft"
    }
  }
}
```

- [ ] **Step 2: Manifest seeded at 0.0.0**

`release-please-manifest.json`:
```json
{
  ".": "0.0.0"
}
```

- [ ] **Step 3: Commit**

```bash
git add release-please-config.json release-please-manifest.json
git commit -m "chore(release): add release-please config (semantic-release-from-commits)"
```

---

### Task 8: GitHub Actions CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Workflow**

`.github/workflows/ci.yml`:
```yaml
name: CI

on:
  pull_request:
    branches: [main, master]
  push:
    branches: [main, master]

jobs:
  build-test:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore -warnaserror

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.os }}
          path: '**/test-results.trx'
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: GitHub Actions CI matrix (ubuntu/windows/macos) build + test on PR"
```

---

### Task 9: GitHub Actions release-please workflow

**Files:**
- Create: `.github/workflows/release-please.yml`

- [ ] **Step 1: Workflow**

`.github/workflows/release-please.yml`:
```yaml
name: Release Please

on:
  push:
    branches: [main, master]

permissions:
  contents: write
  pull-requests: write

jobs:
  release-please:
    runs-on: ubuntu-latest
    steps:
      - uses: googleapis/release-please-action@v4
        with:
          config-file: release-please-config.json
          manifest-file: release-please-manifest.json
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/release-please.yml
git commit -m "ci: release-please workflow (conventional-commit → CHANGELOG + tag)"
```

---

### Task 10: GitHub Actions release-artifacts workflow

**Files:**
- Create: `.github/workflows/release-artifacts.yml`

- [ ] **Step 1: Workflow**

`.github/workflows/release-artifacts.yml`:
```yaml
name: Release Artifacts

on:
  release:
    types: [published]

permissions:
  contents: write
  packages: write

jobs:
  publish-binaries:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: windows-latest
            rid: win-x64
          - os: macos-latest
            rid: osx-arm64
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish weft
        run: |
          dotnet publish src/Weft.Cli/Weft.Cli.csproj \
            --configuration Release \
            --runtime ${{ matrix.rid }} \
            --self-contained false \
            --output publish/${{ matrix.rid }} \
            /p:Version=${{ github.event.release.tag_name }}

      - name: Zip
        shell: bash
        run: |
          cd publish
          if [ "${{ matrix.os }}" = "windows-latest" ]; then
            7z a weft-${{ github.event.release.tag_name }}-${{ matrix.rid }}.zip ${{ matrix.rid }}/*
          else
            zip -r weft-${{ github.event.release.tag_name }}-${{ matrix.rid }}.zip ${{ matrix.rid }}/
          fi

      - name: Upload to release
        uses: softprops/action-gh-release@v2
        with:
          files: publish/weft-${{ github.event.release.tag_name }}-${{ matrix.rid }}.zip

  publish-nuget:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Pack Weft.Core
        run: |
          dotnet pack src/Weft.Core/Weft.Core.csproj \
            --configuration Release \
            --output nupkgs \
            /p:Version=${{ github.event.release.tag_name }}
      - name: Push to nuget.org
        if: secrets.NUGET_API_KEY != ''
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          dotnet nuget push nupkgs/*.nupkg \
            --api-key "$NUGET_API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/release-artifacts.yml
git commit -m "ci: release-artifacts workflow (3-RID binaries + NuGet pack on release)"
```

---

### Task 11: `Weft.Core.csproj` pack metadata

**Files:**
- Modify: `src/Weft.Core/Weft.Core.csproj`

- [ ] **Step 1: Append pack metadata**

Add to `src/Weft.Core/Weft.Core.csproj` inside the `<Project>` root:
```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackageId>Weft.Core</PackageId>
  <Authors>Marcos Magri / Weft contributors</Authors>
  <Description>Pure-logic core of Weft — diff-based Power BI semantic-model deployment. Load .bim / TE folder models, compute a surgical ChangeSet that preserves target partitions + bookmarks, emit TMSL.</Description>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/marcosmagri/weft</PackageProjectUrl>
  <RepositoryUrl>https://github.com/marcosmagri/weft</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageTags>powerbi;tabular;tmsl;deployment;semantic-model</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

(If the project URL / repository URL differ, the user will update them on first release.)

- [ ] **Step 2: Verify pack works locally**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet pack src/Weft.Core/Weft.Core.csproj --configuration Release --output /tmp/nupkgs /p:Version=0.0.1-preview
ls /tmp/nupkgs/
```
Expected: `Weft.Core.0.0.1-preview.nupkg` and `Weft.Core.0.0.1-preview.snupkg` files.

Clean up:
```bash
rm -rf /tmp/nupkgs
```

- [ ] **Step 3: Commit**

```bash
git add src/Weft.Core/Weft.Core.csproj
git commit -m "chore(core): add NuGet pack metadata to Weft.Core.csproj"
```

---

### Task 12: Top-level README

**Files:**
- Create: `README.md`

- [ ] **Step 1: Author README**

`README.md`:
````markdown
# Weft

> Diff-based deploys for Power BI semantic models — without losing your partitions.

`weft` is a cross-platform .NET CLI that deploys Power BI / Fabric semantic models over the XMLA endpoint. Unlike a full `createOrReplace`, it compares your `.bim` source against the live target and emits a surgical TMSL script that:

- Creates new tables from source.
- Alters changed tables **without touching their partitions** — historical partitions created by refresh policies stay intact.
- Preserves every partition's `RefreshBookmark` annotation.
- Blocks deploys that would silently shrink an incremental-refresh rolling window (history-loss gate).
- Runs hooks at each lifecycle phase (pre-deploy, post-deploy, on-failure, etc.).
- Resolves model parameters per environment from a YAML profile.

## Quickstart

```bash
# Install .NET 10 SDK, then:
git clone https://github.com/marcosmagri/weft.git
cd weft
dotnet build

# Validate a .bim file
./src/Weft.Cli/bin/Debug/net10.0/weft validate --source samples/01-simple-bim/model.bim

# Deploy (needs a Premium workspace + AAD app with XMLA Admin permission)
./src/Weft.Cli/bin/Debug/net10.0/weft deploy \
  --config samples/01-simple-bim/weft.yaml \
  --target dev
```

See [docs/getting-started.md](docs/getting-started.md) for a walkthrough.

## Commands

| Command | What it does |
|---|---|
| `weft validate --source <path>` | Parse and validate a `.bim` or TE folder model. |
| `weft plan --source <.bim> --target-snapshot <.bim>` | Offline diff against a captured target snapshot. |
| `weft deploy --config weft.yaml --target <profile>` | Full pipeline: auth → diff → execute → refresh. |
| `weft refresh --tables a,b,c` | Refresh selected tables. |
| `weft restore-history --table <name>` | Re-materialize historical partitions per policy. |
| `weft inspect partitions --target-snapshot <.bim>` | List partitions and bookmarks from a snapshot. |

## Authentication

Five modes, all via MSAL:

- `ServicePrincipalCertStore` — Windows cert store (ops / Octopus).
- `ServicePrincipalCertFile` — `.pfx` file + password.
- `ServicePrincipalSecret` — client secret (last resort).
- `Interactive` — browser popup (dev).
- `DeviceCode` — headless dev boxes.

See [docs/authentication.md](docs/authentication.md).

## Integrations

- **GitHub Actions:** `.github/workflows/ci.yml` for PR CI, `release-artifacts.yml` for binaries + NuGet on tag.
- **TeamCity:** `build/teamcity/settings.kts` (Kotlin DSL).
- **Octopus Deploy:** `build/octopus/step-templates/weft-deploy.json` — import once, use in any project.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Authentication](docs/authentication.md)
- [Parameters](docs/parameters.md)
- [Partition Preservation](docs/partition-preservation.md)
- [Incremental Refresh](docs/incremental-refresh.md)
- [Restore History](docs/restore-history.md)
- [Hooks](docs/hooks.md)
- [Troubleshooting](docs/troubleshooting.md)

## License

MIT. See [LICENSE](LICENSE).
````

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: top-level README with quickstart + command overview"
```

---

### Task 13: `CONTRIBUTING.md`

**Files:**
- Create: `CONTRIBUTING.md`

- [ ] **Step 1: Author**

`CONTRIBUTING.md`:
````markdown
# Contributing to Weft

Thanks for your interest. A few ground rules:

## Development setup

1. Install .NET 10 SDK (`brew install --cask dotnet-sdk` on macOS).
2. Clone + restore:
   ```bash
   git clone https://github.com/marcosmagri/weft.git
   cd weft
   dotnet restore
   ```
3. Build + test:
   ```bash
   dotnet build -warnaserror
   dotnet test
   ```
   All must pass with 0 warnings before a PR lands.

## Commit messages

We use [Conventional Commits](https://www.conventionalcommits.org/) because `release-please` generates the changelog from them:

- `feat(core): add XYZ` → shows up as a Feature in the next minor release.
- `fix(cli): correct ABC` → Bug Fix in the next patch release.
- `docs: update quickstart` → Documentation section.
- `chore`, `test`, `refactor`, `perf` also accepted.

A single commit can close the plan task; a PR can bundle multiple.

## Testing expectations

- Every public method in `Weft.Core` has unit tests.
- Every CLI command has at least one happy-path test using the mock harness (`CliTestHost` + NSubstitute).
- Integration tests (`test/Weft.Integration.Tests`) run in CI only; they skip locally without the `WEFT_INT_*` env vars.
- New safety gates (drop, history-loss, integrity) MUST have a test covering the refusal path.

## Running integration tests locally

```bash
export WEFT_INT_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/weft-ci'
export WEFT_INT_DATABASE='TinyStatic'
export WEFT_INT_TENANT_ID='<tenant-guid>'
export WEFT_INT_CLIENT_ID='<app-guid>'
export WEFT_INT_CLIENT_SECRET='<secret>'
dotnet test test/Weft.Integration.Tests
```

## Filing bugs

Use `.github/ISSUE_TEMPLATE/bug_report.md`. Include:
- Weft version (`weft --version` once we ship it; or commit SHA).
- .NET SDK version (`dotnet --version`).
- `weft plan` output if the bug is deploy-related.
- Minimal `.bim` fixture if possible.

## Pull requests

1. Branch off `main`.
2. One logical change per PR; split big ones.
3. All PR builds go green before review.
4. Squash-merge unless commit-by-commit history adds value.

## Security

See [SECURITY.md](SECURITY.md) — do NOT file auth / secret vulnerabilities as public issues. Email the maintainer.

## License

By contributing, you agree your contributions are licensed under the project's MIT license.
````

- [ ] **Step 2: Commit**

```bash
git add CONTRIBUTING.md
git commit -m "docs: CONTRIBUTING.md (dev setup, conventional commits, testing expectations)"
```

---

### Task 14: GitHub issue templates + PR template

**Files:**
- Create: `.github/ISSUE_TEMPLATE/bug_report.md`
- Create: `.github/ISSUE_TEMPLATE/feature_request.md`
- Create: `.github/pull_request_template.md`

- [ ] **Step 1: Bug report**

`.github/ISSUE_TEMPLATE/bug_report.md`:
```markdown
---
name: Bug report
about: Report an unexpected failure during validate / plan / deploy / refresh / restore-history / inspect
title: "[BUG] "
labels: bug
assignees: ''
---

**What happened**
A clear description of the unexpected behaviour.

**Command that failed**
```
weft <command> <args...>
```

**Exit code**
<Paste from `echo $?` / `$LASTEXITCODE`>

**Environment**
- Weft version (or commit SHA):
- .NET SDK (`dotnet --version`):
- OS:

**Target**
- Power BI Premium capacity? Fabric workspace?
- Model size (# tables, # partitions on largest fact):

**Plan output (redact secrets)**
```
<paste `weft plan` output or ./artifacts/<timestamp>-*-plan.tmsl>
```

**Expected vs. actual**
```

- [ ] **Step 2: Feature request**

`.github/ISSUE_TEMPLATE/feature_request.md`:
```markdown
---
name: Feature request
about: Suggest a new feature or enhancement
title: "[FEATURE] "
labels: enhancement
assignees: ''
---

**The problem**
What specific pain point or missing capability are you running into?

**The proposed solution**
What would the ideal behaviour look like?

**Alternatives considered**
What other approaches did you consider?

**Additional context**
Anything else relevant — links to Power BI docs, related issues, etc.
```

- [ ] **Step 3: PR template**

`.github/pull_request_template.md`:
```markdown
## Summary
<1-3 bullet points describing the change>

## Motivation
<Why is this change needed? Link to the issue if applicable.>

## Testing
- [ ] `dotnet build -warnaserror` passes with 0 warnings
- [ ] `dotnet test` passes
- [ ] New safety gates (if any) have refusal-path tests

## Type of change
- [ ] `feat` — new capability
- [ ] `fix` — bug fix
- [ ] `docs` — documentation only
- [ ] `refactor` — no behaviour change
- [ ] `test` — test additions only
- [ ] `chore` — build / tooling

## Checklist
- [ ] Commit message uses Conventional Commit style
- [ ] CHANGELOG will be generated by release-please (no manual edits)
- [ ] Docs updated if public CLI surface changed
```

- [ ] **Step 4: Commit**

```bash
git add .github/ISSUE_TEMPLATE/ .github/pull_request_template.md
git commit -m "docs(github): bug + feature issue templates and PR template"
```

---

### Task 15: Sample 01 — simple .bim

**Files:**
- Create: `samples/01-simple-bim/model.bim`
- Create: `samples/01-simple-bim/weft.yaml`
- Create: `samples/01-simple-bim/README.md`

- [ ] **Step 1: Copy the existing tiny-static fixture into the sample**

```bash
cp test/Weft.Core.Tests/fixtures/models/tiny-static.bim samples/01-simple-bim/model.bim
```

- [ ] **Step 2: Sample config**

`samples/01-simple-bim/weft.yaml`:
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
  allowDrops: false
  allowHistoryLoss: false
  timeoutMinutes: 60

profiles:
  dev:
    workspace: "${WEFT_DEV_WORKSPACE}"
    database: "${WEFT_DEV_DATABASE}"
    auth:
      mode: Interactive
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_CLIENT_ID}"

  prod:
    workspace: "${WEFT_PROD_WORKSPACE}"
    database: "${WEFT_PROD_DATABASE}"
    auth:
      mode: ServicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"
      certStoreLocation: LocalMachine
      certStoreName: My
```

- [ ] **Step 3: README**

`samples/01-simple-bim/README.md`:
````markdown
# Sample 01 — Simple `.bim`

The minimal Weft setup: a single `.bim` file, no parameters, no hooks.

## Model

`TinyStatic` — two tables (`DimDate`, `FactSales`), one measure, one relationship.

## Environment

Set these env vars, then run:

```bash
export WEFT_TENANT_ID='<your tenant id>'
export WEFT_CLIENT_ID='<your app id>'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyStatic'

weft validate --source ./model.bim
weft deploy --config ./weft.yaml --target dev
```

The first `deploy` creates the two tables and runs a refresh. The second is a no-op (nothing changed).
````

- [ ] **Step 4: Commit**

```bash
git add samples/01-simple-bim/
git commit -m "docs(samples): 01-simple-bim (bim file + interactive/SP-cert config)"
```

---

### Task 16: Sample 02 — Tabular Editor folder

**Files:**
- Create: `samples/02-tabular-editor-folder/Model/database.json`
- Create: `samples/02-tabular-editor-folder/Model/tables/DimDate.json`
- Create: `samples/02-tabular-editor-folder/Model/tables/FactSales.json`
- Create: `samples/02-tabular-editor-folder/weft.yaml`
- Create: `samples/02-tabular-editor-folder/README.md`

- [ ] **Step 1: Copy fixture**

```bash
mkdir -p samples/02-tabular-editor-folder/Model/tables
cp test/Weft.Core.Tests/fixtures/models/tiny-folder/database.json samples/02-tabular-editor-folder/Model/database.json
cp test/Weft.Core.Tests/fixtures/models/tiny-folder/tables/*.json samples/02-tabular-editor-folder/Model/tables/
```

- [ ] **Step 2: weft.yaml**

`samples/02-tabular-editor-folder/weft.yaml`:
```yaml
version: 1

source:
  format: folder
  path: ./Model

defaults:
  allowDrops: false
  allowHistoryLoss: false

profiles:
  dev:
    workspace: "${WEFT_DEV_WORKSPACE}"
    database: "${WEFT_DEV_DATABASE}"
    auth:
      mode: Interactive
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_CLIENT_ID}"
```

- [ ] **Step 3: README**

`samples/02-tabular-editor-folder/README.md`:
````markdown
# Sample 02 — Tabular Editor "Save to Folder"

This demonstrates `format: folder` sources — the layout Tabular Editor produces when you use **Save to Folder** instead of a single `.bim`.

## Layout

```
Model/
  database.json
  tables/
    DimDate.json
    FactSales.json
```

Weft stitches the per-table JSON files back into the `database.json` in memory and deserializes through the standard TOM serializer.

## Usage

```bash
export WEFT_TENANT_ID='...'
export WEFT_CLIENT_ID='...'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyFolder'

weft validate --source ./Model
weft deploy --config ./weft.yaml --target dev
```
````

- [ ] **Step 4: Commit**

```bash
git add samples/02-tabular-editor-folder/
git commit -m "docs(samples): 02-tabular-editor-folder (folder-format source)"
```

---

### Task 17: Sample 03 — With parameters

**Files:**
- Create: `samples/03-with-parameters/model.bim`
- Create: `samples/03-with-parameters/weft.yaml`
- Create: `samples/03-with-parameters/README.md`

- [ ] **Step 1: Author the `.bim` with parameter expressions**

`samples/03-with-parameters/model.bim`:
```json
{
  "name": "ParameterizedModel",
  "compatibilityLevel": 1600,
  "model": {
    "culture": "en-US",
    "expressions": [
      {
        "name": "DatabaseName",
        "kind": "m",
        "expression": "\"EDW\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]",
        "annotations": [
          { "name": "IsParameterQuery", "value": "true" }
        ]
      },
      {
        "name": "ServerName",
        "kind": "m",
        "expression": "\"localhost\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]",
        "annotations": [
          { "name": "IsParameterQuery", "value": "true" }
        ]
      }
    ],
    "tables": [
      {
        "name": "DimDate",
        "columns": [
          { "name": "Date", "dataType": "dateTime", "sourceColumn": "Date" }
        ],
        "partitions": [
          {
            "name": "DimDate",
            "mode": "import",
            "source": { "type": "m", "expression": "let Source = #table({\"Date\"}, {{#date(2025,1,1)}}) in Source" }
          }
        ]
      }
    ]
  }
}
```

- [ ] **Step 2: weft.yaml**

`samples/03-with-parameters/weft.yaml`:
```yaml
version: 1

source:
  format: bim
  path: ./model.bim

parameters:
  - name: DatabaseName
    description: Warehouse database name
    type: string
    required: true
  - name: ServerName
    type: string
    required: true

profiles:
  dev:
    workspace: "${WEFT_DEV_WORKSPACE}"
    database: "${WEFT_DEV_DATABASE}"
    auth:
      mode: Interactive
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_CLIENT_ID}"
    parameters:
      DatabaseName: "EDW_DEV"
      ServerName: "dev-sql.corp.local"

  prod:
    workspace: "${WEFT_PROD_WORKSPACE}"
    database: "${WEFT_PROD_DATABASE}"
    auth:
      mode: ServicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"
    parameters:
      DatabaseName: "EDW_PROD"
      ServerName: "prod-sql.corp.local"
```

- [ ] **Step 3: README**

`samples/03-with-parameters/README.md`:
````markdown
# Sample 03 — Parameterized model

Shows per-environment parameter injection. The same `.bim` ships to dev and prod with different `DatabaseName` / `ServerName` values.

## The model

Two M parameters, `DatabaseName` and `ServerName`, declared with `IsParameterQuery=true`. Their defaults are `"EDW"` and `"localhost"` — production-safe fallbacks that a deploy without config would use.

## The config

The `parameters:` block declares required parameters at the top level. Each profile's `parameters:` map provides per-env values. At deploy time, Weft:

1. Auto-discovers every `IsParameterQuery` expression in the source model.
2. Resolves each from (priority order): CLI `--param KEY=VALUE` → `--params-file` → env `WEFT_PARAM_<name>` → profile YAML → declaration default.
3. Fails the deploy if a `required: true` parameter has no value anywhere.
4. Rewrites the M expression literal in-memory, preserving any `meta [...]` suffix.

## Usage

```bash
weft deploy --config ./weft.yaml --target prod
# → rewrites DatabaseName → "EDW_PROD", ServerName → "prod-sql.corp.local"
#   before diffing against target
```

Ad-hoc override (e.g., a hotfix deploy):

```bash
export WEFT_PARAM_DatabaseName="EDW_PROD_HOTFIX"
weft deploy --config ./weft.yaml --target prod
# → DatabaseName="EDW_PROD_HOTFIX" wins over the profile YAML
```
````

- [ ] **Step 4: Commit**

```bash
git add samples/03-with-parameters/
git commit -m "docs(samples): 03-with-parameters (per-env parameter injection)"
```

---

### Task 18: Sample 04 — Full pipeline with hooks

**Files:**
- Create: `samples/04-full-pipeline/model.bim`
- Create: `samples/04-full-pipeline/weft.yaml`
- Create: `samples/04-full-pipeline/hooks/notify.sh`
- Create: `samples/04-full-pipeline/hooks/notify.ps1`
- Create: `samples/04-full-pipeline/README.md`

- [ ] **Step 1: Reuse the simple .bim**

```bash
cp samples/01-simple-bim/model.bim samples/04-full-pipeline/model.bim
```

- [ ] **Step 2: Hook scripts**

`samples/04-full-pipeline/hooks/notify.sh`:
```bash
#!/usr/bin/env bash
set -euo pipefail

# Read HookContext JSON from stdin
json=$(cat)
phase="${WEFT_HOOK_PHASE:-unknown}"
profile="${WEFT_HOOK_PROFILE:-unknown}"
db="${WEFT_HOOK_DATABASE:-unknown}"

echo "[${phase}] profile=${profile} db=${db}"
echo "  payload: $(echo "$json" | head -c 200)..."

# Stand-in for Teams/Slack POST:
# curl -X POST -H 'Content-Type: application/json' \
#   -d "{\"text\":\"[${phase}] ${profile}/${db}\"}" "$TEAMS_WEBHOOK"
```

`samples/04-full-pipeline/hooks/notify.ps1`:
```powershell
# Windows counterpart
$json = [Console]::In.ReadToEnd()
$phase = $env:WEFT_HOOK_PHASE
$profile = $env:WEFT_HOOK_PROFILE
$db = $env:WEFT_HOOK_DATABASE

Write-Host "[${phase}] profile=${profile} db=${db}"
Write-Host "  payload: $($json.Substring(0, [Math]::Min(200, $json.Length)))..."
```

- [ ] **Step 3: weft.yaml**

`samples/04-full-pipeline/weft.yaml`:
```yaml
version: 1

source:
  format: bim
  path: ./model.bim

defaults:
  allowDrops: false
  allowHistoryLoss: false

profiles:
  prod:
    workspace: "${WEFT_PROD_WORKSPACE}"
    database: "${WEFT_PROD_DATABASE}"
    auth:
      mode: ServicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"

hooks:
  preDeploy: ./hooks/notify.sh
  postDeploy: ./hooks/notify.sh
  onFailure: ./hooks/notify.sh
```

- [ ] **Step 4: README**

`samples/04-full-pipeline/README.md`:
````markdown
# Sample 04 — Full pipeline with hooks

Shows every Weft lifecycle piece in one place: SP cert auth, hooks on preDeploy / postDeploy / onFailure, and safe defaults for prod (`allowDrops: false`, `allowHistoryLoss: false`).

## Hook protocol

On each phase, Weft spawns the configured command as a child process. The child receives:

- **stdin**: `HookContext` JSON (`{ profileName, workspaceUrl, databaseName, phase, changeSet: { added, dropped, altered, unchanged } }`).
- **env**: `WEFT_HOOK_PHASE`, `WEFT_HOOK_PROFILE`, `WEFT_HOOK_DATABASE` (convenience).
- Sanitized env: known secret-bearing variables (`WEFT_CLIENT_SECRET`, `WEFT_CERT_PASSWORD`, `WEFT_CERT_THUMBPRINT`, any `WEFT_PARAM_*` containing PASSWORD/SECRET/KEY/TOKEN) are **removed** before spawn.

## Writing hooks

Unix: `./hooks/notify.sh` (chmod +x). Windows: `./hooks/notify.ps1`. Cross-platform: use `pwsh` and target either. See [docs/hooks.md](../../docs/hooks.md).

## Usage

```bash
export WEFT_TENANT_ID=...
export WEFT_SP_CLIENT_ID=...
export WEFT_CERT_THUMBPRINT=...
export WEFT_PROD_WORKSPACE='powerbi://...'
export WEFT_PROD_DATABASE='TinyStatic'

chmod +x hooks/notify.sh
weft deploy --config ./weft.yaml --target prod
```
````

- [ ] **Step 5: Make the shell script executable in git**

```bash
chmod +x samples/04-full-pipeline/hooks/notify.sh
git add samples/04-full-pipeline/
git update-index --chmod=+x samples/04-full-pipeline/hooks/notify.sh
git commit -m "docs(samples): 04-full-pipeline (SP cert + hooks)"
```

---

### Task 19: Docs — getting-started + authentication

**Files:**
- Create: `docs/getting-started.md`
- Create: `docs/authentication.md`

- [ ] **Step 1: getting-started.md**

`docs/getting-started.md`:
````markdown
# Getting started with Weft

This walkthrough deploys a tiny model to your own Power BI Premium workspace.

## 1. Prerequisites

- A **Power BI Premium capacity** (Premium per user or Premium per capacity / Fabric) with the XMLA endpoint enabled.
- An **AAD app registration** with XMLA Admin rights on the workspace. See [docs/authentication.md](authentication.md) for the cert + SP walkthrough.
- **.NET 10 SDK** locally (`dotnet --version` returns `10.x.x`).

## 2. Install

Either download the binary for your OS from the GitHub Releases page, or build from source:

```bash
git clone https://github.com/marcosmagri/weft.git
cd weft
dotnet publish src/Weft.Cli/Weft.Cli.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained false \
  --output ./bin
./bin/weft --help
```

## 3. Grab the sample

```bash
cp -r samples/01-simple-bim /tmp/my-first-weft
cd /tmp/my-first-weft
```

## 4. Validate

```bash
weft validate --source ./model.bim
# OK: 'TinyStatic' loaded with 2 table(s).
```

## 5. Set env vars

```bash
export WEFT_TENANT_ID='<your tenant guid>'
export WEFT_CLIENT_ID='<your aad app id>'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyStatic'
```

## 6. Deploy

```bash
weft deploy --config ./weft.yaml --target dev --artifacts ./artifacts
```

First deploy creates the model and refreshes. Second deploy is a no-op.

## 7. Inspect artifacts

```bash
ls artifacts/
# 20260418-140500-TinyStatic-pre-partitions.json
# 20260418-140500-TinyStatic-plan.tmsl
# 20260418-140500-TinyStatic-post-partitions.json
# 20260418-140500-TinyStatic-receipt.json
```

Every deploy — successful or not — leaves a timestamped trail. Commit these to your release artifacts bucket for audit.

## 8. Next steps

- [Authentication](authentication.md) — setting up SP + cert for CI/CD.
- [Parameters](parameters.md) — per-env DB names and connection strings.
- [Partition preservation](partition-preservation.md) — the core guarantee.
- [Hooks](hooks.md) — notifying Teams/Slack at lifecycle phases.
````

- [ ] **Step 2: authentication.md**

`docs/authentication.md`:
````markdown
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
````

- [ ] **Step 3: Commit**

```bash
git add docs/getting-started.md docs/authentication.md
git commit -m "docs: getting-started + authentication walkthroughs"
```

---

### Task 20: Docs — parameters + partition-preservation + incremental-refresh

**Files:**
- Create: `docs/parameters.md`
- Create: `docs/partition-preservation.md`
- Create: `docs/incremental-refresh.md`

- [ ] **Step 1: parameters.md**

`docs/parameters.md`:
````markdown
# Parameters

Weft auto-discovers every M parameter in your source model and lets you override its value per environment.

## What counts as a parameter

Any `NamedExpression` in `model.expressions` with:
- `kind: "m"`
- An `annotations` entry named `IsParameterQuery` with value `"true"`

This is the shape Power BI Desktop creates when you define a parameter via **Manage parameters**.

## Resolution priority (highest wins)

1. CLI: `--param DatabaseName=EDW_PROD_HOTFIX` (repeatable).
2. Params file: `--params-file ./my-params.json` (coming in a future release).
3. Env var: `WEFT_PARAM_DatabaseName`.
4. Profile YAML: `profiles.<env>.parameters.DatabaseName`.
5. Declaration default: `parameters[].default` in the top-level `parameters:` list (or the M expression's literal).

If a `required: true` declaration has no value anywhere, the deploy fails with exit code `10` (`ParameterError`).

## Declaring parameters

```yaml
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
```

Types: `string`, `bool`, `int`. Weft coerces YAML scalars → M literals (`"EDW"` → `"EDW"`, `true` → `true`, `42` → `42`, with proper M escaping for strings containing quotes).

## Applying per-env values

```yaml
profiles:
  dev:
    parameters:
      DatabaseName: EDW_DEV
      ServerName: dev-sql.corp.local
      EnableDebugMeasures: true

  prod:
    parameters:
      DatabaseName: EDW_PROD
      ServerName: prod-sql.corp.local
      # EnableDebugMeasures unset → falls through to declaration default / model literal
```

## How it works under the hood

At deploy time (phase 2a):
1. `ParameterResolver` builds a `(name → value, source)` map for every declared parameter.
2. `MParameterDiscoverer` finds every `IsParameterQuery` expression in the source model.
3. For each resolution, `ParameterValueCoercer` emits the M literal (`"EDW_PROD"` for string, etc.).
4. The M expression is rewritten: `"EDW" meta [...]` → `"EDW_PROD" meta [...]`. The `meta [...]` suffix is preserved.

The diff then sees the resolved model, so parameters travel with the TMSL to the target.

## Hotfix overrides without editing YAML

```bash
export WEFT_PARAM_DatabaseName="EDW_PROD_HOTFIX"
weft deploy --config weft.yaml --target prod
```

Or one-off:

```bash
weft deploy --config weft.yaml --target prod --param DatabaseName=EDW_PROD_HOTFIX
```
````

- [ ] **Step 2: partition-preservation.md**

`docs/partition-preservation.md`:
````markdown
# Partition preservation — the core guarantee

This is Weft's load-bearing promise: **no partition created by a prior refresh is dropped by a deploy.**

## The problem

When Power BI Desktop publishes a model, it does a `createOrReplace` on the whole database. That wipes every partition the refresh cycle has created since the last publish — including the historical partitions that an incremental-refresh policy materialized months or years ago. Since the default refresh only touches the last N days, those historical partitions don't come back automatically.

For tenants running dynamic partitioning (a custom script creates a new partition per month and refreshes it once), a `createOrReplace` is worse: it drops every dynamic partition, forcing a full rebuild.

## The Weft approach

Weft does a three-way diff before emitting TMSL:

1. **Source** — your committed `.bim` or TE folder.
2. **Target** — the live model read via XMLA.
3. **ChangeSet** — what needs to change.

Every table falls into one of three classifications (see `Weft.Core.Diffing.TableClassification`):

- **Static** — no refresh policy, partitions in source match target. TMSL: no change.
- **DynamicallyPartitioned** — target has partitions not present in source. Weft preserves target's partition collection; schema changes apply to columns/measures only.
- **IncrementalRefreshPolicy** — the table has a `RefreshPolicy` object. Weft preserves partitions and delegates rolling-window management to Power BI's refresh engine.

For classes 2 and 3, the emitted TMSL is an `alter` operation on the table's schema that **explicitly re-attaches every existing partition from target**, including each partition's `RefreshBookmark` annotation. The `PartitionIntegrityValidator` sanity-checks the generated TMSL before it's sent.

## What's in the TMSL

For an altered `FactSales` table that had 48 historical partitions:

```json
{
  "createOrReplace": {
    "object": { "database": "SalesModel", "table": "FactSales" },
    "table": {
      "name": "FactSales",
      "columns": [...],
      "measures": [...],
      "partitions": [
        { "name": "Year2020", "source": {...}, "annotations": [{"name":"RefreshBookmark","value":"2021-12-31T23:59:59Z"}] },
        { "name": "Year2021", ... },
        ...
        { "name": "Month2026-04", ... }
      ]
    }
  }
}
```

All 48 partitions travel through the alter. Their bookmarks come along too — so "detect data changes" still knows what's fresh.

## Integrity checks

Two gates run on every deploy:

1. **Pre-emit** (`PartitionIntegrityValidator`): scans the generated TMSL for any partition delete targeting a preserved table. Throws `PartitionIntegrityException` before the TMSL leaves the CLI.
2. **Post-deploy** (integrity gate in `DeployCommand`): re-reads the target, compares the partition manifest to the pre-deploy snapshot. Any preserved table that lost partitions → exit code `9` (`PartitionIntegrityError`).

## What you need to do

Nothing. This is the default behaviour for every deploy.

The only case requiring your input is a deliberate rolling-window shrink — see [incremental-refresh.md](incremental-refresh.md).
````

- [ ] **Step 3: incremental-refresh.md**

`docs/incremental-refresh.md`:
````markdown
# Incremental refresh

Weft understands Power BI's `RefreshPolicy` object and treats incremental-refresh tables specially.

## What Weft looks for

A `Table` with a non-null `RefreshPolicy` (a `BasicRefreshPolicy` in TOM):

```json
{
  "name": "FactSales",
  "refreshPolicy": {
    "policyType": "basic",
    "rollingWindowGranularity": "year",
    "rollingWindowPeriods": 5,
    "incrementalGranularity": "day",
    "incrementalPeriods": 10,
    "sourceExpression": [ "let Source = ... in Source" ]
  }
}
```

Weft classifies this table as `IncrementalRefreshPolicy`. Partitions are preserved unconditionally (see [partition-preservation.md](partition-preservation.md)); the refresh engine's `ApplyRefreshPolicy` semantics handle the rolling window.

## The history-loss gate

If you shrink `rollingWindowPeriods` (e.g., from 5 years to 3 years), the refresh engine will **drop the oldest partitions** next time `ApplyRefreshPolicy=true`. Weft refuses this by default.

### What happens at deploy

Phase 5a runs `HistoryLossGate`:

```
FactSales: rollingWindowPeriods 5 → 3 would evict partitions:
  Year2021, Year2022, Year2023
Refusing deploy without profile.allowHistoryLoss=true.
Exit code: 6 (DiffValidationError)
```

### When you actually want the shrink

```yaml
profiles:
  prod:
    allowHistoryLoss: true     # explicit opt-in
```

Now the deploy proceeds; the refresh engine prunes old years; `restore-history` can bring them back if the warehouse still has the data.

## Per-table refresh-type matrix

After every deploy, Weft refreshes affected tables via `RefreshRunner`. The refresh type is chosen per table (see `Weft.Xmla.RefreshTypeSelector`):

| Classification | New (added) | Altered w/ policy change | Altered w/ schema only |
|---|---|---|---|
| `IncrementalRefreshPolicy` | `Policy` + `ApplyRefreshPolicy=true` | `Policy` + `ApplyRefreshPolicy=true` | `Policy` (no apply) |
| `DynamicallyPartitioned` | `Full` on all partitions | `Full` on all partitions | `Full` on all partitions |
| `Static` | `Full` | `Full` | `Full` |

The `Policy` refresh lets the engine roll the window forward (and/or apply a new policy) without Weft orchestrating per-partition refreshes.

## Bookmark modes

`RefreshBookmark` is the "detect data changes" watermark. Weft preserves it across deploys by default. You can override per profile:

```yaml
defaults:
  refresh:
    incrementalPolicy:
      bookmarkMode: preserve    # preserve | clearAll | clearForPolicyChange
```

- `preserve` — keep bookmarks (default).
- `clearAll` — emit an annotation-delete TMSL before refresh so every partition re-checks its source.
- `clearForPolicyChange` — clear only on tables whose policy changed this deploy.

CLI shortcut:

```bash
weft deploy --config weft.yaml --target prod --reset-bookmarks
```

Equivalent to `bookmarkMode: clearAll` for this run only.
````

- [ ] **Step 4: Commit**

```bash
git add docs/parameters.md docs/partition-preservation.md docs/incremental-refresh.md
git commit -m "docs: parameters + partition-preservation + incremental-refresh guides"
```

---

### Task 21: Docs — restore-history + hooks + troubleshooting

**Files:**
- Create: `docs/restore-history.md`
- Create: `docs/hooks.md`
- Create: `docs/troubleshooting.md`

- [ ] **Step 1: restore-history.md**

`docs/restore-history.md`:
````markdown
# Restore history

If historical partitions are missing — because of a manual `allowHistoryLoss` shrink, a botched deploy, or a warehouse re-seed — `weft restore-history` re-materializes them.

## When to use it

- After `allowHistoryLoss: true` dropped partitions you now need back.
- After a manual TMSL execution outside Weft lost partitions.
- When bringing a test environment back to parity with prod.

## Prerequisites

The target table must still have a `RefreshPolicy` (so the engine knows how to materialize). The warehouse source must still contain the historical data you want to restore.

## Usage

```bash
weft restore-history \
  --config weft.yaml \
  --target prod \
  --table FactSales \
  --from 2020-01-01 \
  --to 2023-12-31 \
  --effective-date 2023-12-31
```

Under the hood this issues a TMSL `refresh` with `type: "full"`, `applyRefreshPolicy: true`, and `effectiveDate` set. The engine walks the policy's source expression with `RangeStart` / `RangeEnd` bound to each period in the requested range.

## Options

- `--table` (required) — the table to restore.
- `--from` / `--to` (optional) — ISO dates. Used as the `effectiveDate` window if provided; otherwise the policy's natural rolling window applies.
- `--effective-date` — explicit override; defaults to `--to` or today.

## Caveats

- **Only recovers what the source still has.** If the warehouse archived `2020` data, restoring it will re-materialize an empty partition. Verify source retention before running.
- **Can be slow.** Restoring 5 years of monthly partitions on a 10B-row fact table can run for hours. Schedule accordingly.
- **Locks the table for the duration.** Concurrent reports querying `FactSales` during restore will see the old data until the refresh completes.

## Example scenario

"We shrunk the rolling window from 5y to 3y last quarter. Finance now needs Q4-2021 back for audit."

```bash
weft restore-history --config weft.yaml --target prod \
  --table FactSales --from 2021-10-01 --to 2021-12-31
```

If the warehouse has the data, Q4-2021 partitions reappear. If not, the partitions reappear empty and you need a data team to replay.
````

- [ ] **Step 2: hooks.md**

`docs/hooks.md`:
````markdown
# Hooks

Weft fires hooks at six pipeline phases. Each hook is an external command you configure in `weft.yaml`.

## Phases

| Phase | When it fires | Typical use |
|---|---|---|
| `prePlan` | Before diff is computed | Announce "starting deploy" |
| `preDeploy` | After plan, before TMSL execute | Require approval, lock a change-window |
| `postDeploy` | After TMSL succeeds, before refresh | Tag a git release |
| `preRefresh` | Before refresh TMSL | Scale up capacity |
| `postRefresh` | After refresh succeeds | Scale down, notify success |
| `onFailure` | On any deploy-phase failure | Page oncall, open an incident |

## Config

```yaml
hooks:
  preDeploy: ./hooks/pre-deploy.sh
  postDeploy: ./hooks/tag-git-release.sh
  preRefresh: ./hooks/scale-up.ps1
  postRefresh: ./hooks/scale-down.ps1
  onFailure: ./hooks/open-incident.sh
```

Paths are relative to where you run `weft` (or absolute).

## Protocol

Weft spawns the command as a child process. The child receives:

- **stdin**: HookContext JSON:
  ```json
  {
    "profileName": "prod",
    "workspaceUrl": "powerbi://api.powerbi.com/v1.0/myorg/Weft-Prod",
    "databaseName": "SalesModel",
    "phase": "PreDeploy",
    "changeSet": {
      "added": ["NewTable"],
      "dropped": [],
      "altered": ["FactSales"],
      "unchanged": ["DimDate"]
    }
  }
  ```
- **env** (convenience):
  - `WEFT_HOOK_PHASE` — matches `changeSet.phase`
  - `WEFT_HOOK_PROFILE` — profile name
  - `WEFT_HOOK_DATABASE` — database name

## Environment sanitization

Before spawning, Weft removes these variables from the child environment:

- `WEFT_CLIENT_SECRET`
- `WEFT_CERT_PASSWORD`
- `WEFT_CERT_THUMBPRINT`
- Any `WEFT_PARAM_*` whose name contains `PASSWORD`, `SECRET`, `KEY`, or `TOKEN` (case-insensitive).

Secrets you want accessible in hooks should come from your own env config (Octopus sensitive variables, GitHub Actions secrets, etc.) under a non-reserved name.

## Failure behaviour

A hook that exits non-zero logs a warning and **does not abort** the main deploy (except `onFailure` hooks, which fire *after* a failure anyway). This is deliberate: a failing `postDeploy` notification shouldn't roll back a successful TMSL alter.

If you want "block on hook failure" semantics, script the hook to `exit 0` on warnings and use `onFailure` for real aborts — or write a custom pre-deploy hook that fails the deploy via stdout+nonzero: Weft will log but continue, so the main deploy still proceeds. (Blocking hooks are a feature tracked for a future release.)

## Command parsing — WHITESPACE SPLIT, NOT SHELL

The `command:` string is split on whitespace. Quoted arguments **are not supported**:

```yaml
hooks:
  preDeploy: ./hooks/notify.sh "arg with spaces"   # BREAKS: 3 args, not 1
```

Wrap complex args in a shell script:

```yaml
hooks:
  preDeploy: ./hooks/notify.sh
```

```bash
#!/bin/sh
ARG="arg with spaces"
./actual-command.sh "$ARG"
```

## Example hooks

See `samples/04-full-pipeline/hooks/` for a cross-platform pair:

- `notify.sh` — Unix shell reading JSON from stdin, printing a summary.
- `notify.ps1` — PowerShell equivalent.
````

- [ ] **Step 3: troubleshooting.md**

`docs/troubleshooting.md`:
````markdown
# Troubleshooting

## Exit codes

| Code | Meaning | Next step |
|---|---|---|
| 0 | Success | — |
| 2 | Config error (malformed `weft.yaml`, missing env var) | Check `weft.yaml` syntax; `echo $WEFT_*` |
| 3 | Auth error | See [authentication.md](authentication.md) |
| 4 | Source load error | `weft validate --source ...` to isolate |
| 5 | Target read error (XMLA connection) | Check workspace URL, SP permissions |
| 6 | Diff validation error (drop without `--allow-drops`, history-loss without `allowHistoryLoss`) | Read the error, decide if opting in is safe |
| 7 | TMSL execution error | Check the returned XMLA message; check `artifacts/*-plan.tmsl` |
| 8 | Refresh error | Model deployed, data is stale — re-run `weft refresh` |
| 9 | Partition integrity violation | STOP. Do not retry. Investigate with `artifacts/*-pre-partitions.json` vs `*-post-partitions.json` |
| 10 | Parameter error (required parameter missing, type mismatch) | Check `weft.yaml` parameter declarations and profile values |

## Common issues

### `Source not found: /path/to/model.bim`

`weft deploy` can't find your source. Check:

- Path is relative to the **CWD where you run `weft`**, not to `weft.yaml`.
- If using `--config`, omit `--source` and let the config's `source.path` apply.

### `Environment variable 'WEFT_TENANT_ID' referenced in config is not set.`

`${VAR}` expansion couldn't find the variable. Either `export` it, or set it in Octopus / TeamCity / GitHub Actions secrets.

### `Partition integrity violation: table 'FactSales' missing post-deploy.`

Something outside Weft deleted the table between pre-manifest and post-manifest. Causes:

- Another process ran `createOrReplace` during your deploy.
- The workspace was migrated / the dataset was swapped.

Action: STOP. Investigate before re-running. Weft's receipt (`artifacts/*-receipt.json`) + manifests are the forensic record.

### `Partition integrity violation on 'FactSales': missing post-deploy: Year2021, Year2022`

The post-deploy manifest is missing partitions that existed pre-deploy on a **preserved** table. This is the §5.4 invariant violation. Weft refused to emit the bad TMSL, so you only see this if:

- Another TMSL ran out-of-band and clobbered the table during your deploy.
- There's a bug in `TmslBuilder` — please file an issue with the `artifacts/` contents.

### `History-loss violation on FactSales: would remove Year2021, Year2022, Year2023`

You're shrinking an incremental-refresh `rollingWindowPeriods`. See [incremental-refresh.md](incremental-refresh.md) for the `allowHistoryLoss: true` opt-in and `weft restore-history` recovery.

### `Refusing to drop tables without allowDrops: LegacyDim`

Source removed a table that exists on target. Either:

- Genuinely want to drop it: `weft deploy ... --allow-drops` (and set `allowDrops: true` in the profile).
- Or restore the table in your source (uncommit the deletion).

### `Hook 'PreDeploy' exited 1 (non-fatal).`

Your `preDeploy` hook returned non-zero. Weft logged the failure and continued. Check the hook's stderr in the deploy log. If you want hook failures to stop the deploy, exit with code 0 on warnings and emit diagnostics to stderr.

### MSAL `InteractionRequired` on service-principal mode

The SP's client secret expired or the cert is wrong. In the Azure portal, re-issue the cert or the secret, and update your env vars.

## Getting help

File an issue using `.github/ISSUE_TEMPLATE/bug_report.md`. Include:

- Exit code.
- `weft plan` output (if the issue is during deploy).
- The `artifacts/*-plan.tmsl` file (redact any secrets manually).
- .NET SDK version + OS.
````

- [ ] **Step 4: Commit**

```bash
git add docs/restore-history.md docs/hooks.md docs/troubleshooting.md
git commit -m "docs: restore-history + hooks + troubleshooting guides"
```

---

### Task 22: TeamCity Kotlin DSL

**Files:**
- Create: `build/teamcity/settings.kts`

- [ ] **Step 1: Author settings.kts**

`build/teamcity/settings.kts`:
```kotlin
import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetBuild
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetTest
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetPublish
import jetbrains.buildServer.configs.kotlin.triggers.vcs

version = "2024.03"

project {
    buildType(BuildAndTest)
    buildType(PublishArtifacts)
}

object BuildAndTest : BuildType({
    name = "Build + Test"

    vcs { root(DslContext.settingsRoot) }

    steps {
        dotnetBuild {
            name = "Build"
            projects = "weft.sln"
            configuration = "Release"
            args = "-warnaserror"
        }
        dotnetTest {
            name = "Test"
            projects = "weft.sln"
            configuration = "Release"
            skipBuild = true
        }
    }

    triggers { vcs {} }

    artifactRules = """
        test/**/TestResults/** => test-results
        artifacts/**/* => artifacts
    """.trimIndent()
})

object PublishArtifacts : BuildType({
    name = "Publish artifacts (win-x64 + osx-arm64 + linux-x64)"

    vcs { root(DslContext.settingsRoot) }

    params {
        param("env.WEFT_VERSION", "1.0.0-local")
    }

    steps {
        dotnetPublish {
            name = "Publish win-x64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "win-x64"
            outputDir = "publish/win-x64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
        dotnetPublish {
            name = "Publish linux-x64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "linux-x64"
            outputDir = "publish/linux-x64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
        dotnetPublish {
            name = "Publish osx-arm64"
            projects = "src/Weft.Cli/Weft.Cli.csproj"
            configuration = "Release"
            runtime = "osx-arm64"
            outputDir = "publish/osx-arm64"
            args = "--self-contained false /p:Version=%env.WEFT_VERSION%"
        }
    }

    artifactRules = """
        publish/** => weft-%env.WEFT_VERSION%.zip!/
    """.trimIndent()

    dependencies {
        snapshot(BuildAndTest) {
            onDependencyFailure = FailureAction.FAIL_TO_START
        }
    }
})
```

- [ ] **Step 2: Commit**

```bash
git add build/teamcity/
git commit -m "ci(teamcity): Kotlin DSL settings for build+test and 3-RID publish"
```

---

### Task 23: Octopus step templates

**Files:**
- Create: `build/octopus/README.md`
- Create: `build/octopus/step-templates/weft-deploy.json`
- Create: `build/octopus/step-templates/weft-refresh.json`

- [ ] **Step 1: README**

`build/octopus/README.md`:
````markdown
# Octopus step templates

Two templates ship here. Import each once into Octopus Deploy (Library → Step templates → Import).

| File | Step template name | Use |
|---|---|---|
| `weft-deploy.json` | `Weft — Deploy Power BI Model` | Full diff-based deploy |
| `weft-refresh.json` | `Weft — Refresh Power BI Model` | Targeted refresh of listed tables |

## Typical Octopus project structure

```
Project: PowerBI-Sales-Model
  Variables:
    TenantId                (project, sensitive)
    SpClientId              (project, sensitive, scope: dev/uat/prod)
    CertThumbprint          (project, sensitive, scope: dev/uat/prod)
    WorkspaceUrl            (scoped per environment)
    DatabaseName            (scoped per environment)
    WeftParam.DatabaseName  (scoped per environment → WEFT_PARAM_DatabaseName)
    WeftParam.ServerName    (scoped per environment)

  Process:
    Step 1: Deploy a package (the weft-*.zip artifact + your model)
    Step 2: Weft — Deploy Power BI Model
      - Config File: weft.yaml
      - Target Profile: #{Octopus.Environment.Name}
      - SP Client Id: #{SpClientId}
      - Tenant Id: #{TenantId}
      - Cert Thumbprint: #{CertThumbprint}
```

## Variable naming convention

Octopus project variables named `WeftParam.<ParamName>` get mapped to `WEFT_PARAM_<ParamName>` env vars by the step template, so your `weft.yaml` parameter values can flow from Octopus scopes.
````

- [ ] **Step 2: weft-deploy.json**

`build/octopus/step-templates/weft-deploy.json`:
```json
{
  "Id": "weft-deploy-step-template",
  "Name": "Weft — Deploy Power BI Model",
  "Description": "Runs `weft deploy` with the given config and target profile. Binds project variables named WeftParam.* to WEFT_PARAM_* env vars before spawning.",
  "ActionType": "Octopus.Script",
  "Version": 1,
  "Properties": {
    "Octopus.Action.Script.Syntax": "Bash",
    "Octopus.Action.Script.ScriptBody": "#!/usr/bin/env bash\nset -euo pipefail\n\nexport WEFT_TENANT_ID=\"#{WeftTenantId}\"\nexport WEFT_SP_CLIENT_ID=\"#{WeftSpClientId}\"\nexport WEFT_CERT_THUMBPRINT=\"#{WeftCertThumbprint}\"\n\n# Map every project variable named WeftParam.* → WEFT_PARAM_*\nfor VAR in $(compgen -v | grep '^WeftParam_'); do\n  PARAM_NAME=\"${VAR#WeftParam_}\"\n  PARAM_VALUE=\"${!VAR}\"\n  export \"WEFT_PARAM_${PARAM_NAME}=${PARAM_VALUE}\"\ndone\n\nEXTRA_FLAGS=\"\"\nif [ \"#{WeftAllowDrops}\" = \"True\" ]; then EXTRA_FLAGS=\"$EXTRA_FLAGS --allow-drops\"; fi\nif [ \"#{WeftNoRefresh}\" = \"True\" ]; then EXTRA_FLAGS=\"$EXTRA_FLAGS --no-refresh\"; fi\nif [ \"#{WeftResetBookmarks}\" = \"True\" ]; then EXTRA_FLAGS=\"$EXTRA_FLAGS --reset-bookmarks\"; fi\n\nweft deploy \\\n  --config \"#{WeftConfigFile}\" \\\n  --target \"#{WeftTargetProfile}\" \\\n  --artifacts \"#{WeftArtifactsDir}\" \\\n  --log-format json \\\n  $EXTRA_FLAGS\n"
  },
  "Parameters": [
    {
      "Id": "weft-tenant-id",
      "Name": "WeftTenantId",
      "Label": "Tenant ID",
      "HelpText": "AAD tenant GUID.",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-sp-client-id",
      "Name": "WeftSpClientId",
      "Label": "Service Principal Client ID",
      "HelpText": "AAD app registration (client) ID.",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-cert-thumbprint",
      "Name": "WeftCertThumbprint",
      "Label": "Certificate Thumbprint",
      "HelpText": "Thumbprint of the .pfx installed in the Octopus Tentacle's LocalMachine\\My store.",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "Sensitive" }
    },
    {
      "Id": "weft-config-file",
      "Name": "WeftConfigFile",
      "Label": "Config File",
      "HelpText": "Path to weft.yaml (relative to the deployment working dir).",
      "DefaultValue": "weft.yaml",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-target-profile",
      "Name": "WeftTargetProfile",
      "Label": "Target Profile",
      "HelpText": "Profile name from weft.yaml (e.g. dev, uat, prod). Defaults to the Octopus environment name.",
      "DefaultValue": "#{Octopus.Environment.Name}",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-artifacts-dir",
      "Name": "WeftArtifactsDir",
      "Label": "Artifacts Directory",
      "HelpText": "Where Weft writes plan / pre-manifest / post-manifest / receipt JSON.",
      "DefaultValue": "./artifacts",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-allow-drops",
      "Name": "WeftAllowDrops",
      "Label": "Allow Drops",
      "HelpText": "Pass --allow-drops. Requires matching profile.allowDrops=true in weft.yaml.",
      "DefaultValue": "False",
      "DisplaySettings": { "Octopus.ControlType": "Checkbox" }
    },
    {
      "Id": "weft-no-refresh",
      "Name": "WeftNoRefresh",
      "Label": "Skip Refresh",
      "HelpText": "Pass --no-refresh. Deploy schema but skip refresh.",
      "DefaultValue": "False",
      "DisplaySettings": { "Octopus.ControlType": "Checkbox" }
    },
    {
      "Id": "weft-reset-bookmarks",
      "Name": "WeftResetBookmarks",
      "Label": "Reset Bookmarks",
      "HelpText": "Pass --reset-bookmarks. Forces full re-check against source on next refresh.",
      "DefaultValue": "False",
      "DisplaySettings": { "Octopus.ControlType": "Checkbox" }
    }
  ]
}
```

- [ ] **Step 3: weft-refresh.json**

`build/octopus/step-templates/weft-refresh.json`:
```json
{
  "Id": "weft-refresh-step-template",
  "Name": "Weft — Refresh Power BI Model",
  "Description": "Runs `weft refresh` on a comma-separated list of tables.",
  "ActionType": "Octopus.Script",
  "Version": 1,
  "Properties": {
    "Octopus.Action.Script.Syntax": "Bash",
    "Octopus.Action.Script.ScriptBody": "#!/usr/bin/env bash\nset -euo pipefail\n\nexport WEFT_TENANT_ID=\"#{WeftTenantId}\"\nexport WEFT_SP_CLIENT_ID=\"#{WeftSpClientId}\"\nexport WEFT_CERT_THUMBPRINT=\"#{WeftCertThumbprint}\"\n\nweft refresh \\\n  --config \"#{WeftConfigFile}\" \\\n  --target \"#{WeftTargetProfile}\" \\\n  --tables \"#{WeftTables}\" \\\n  --log-format json\n"
  },
  "Parameters": [
    {
      "Id": "weft-tenant-id",
      "Name": "WeftTenantId",
      "Label": "Tenant ID",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-sp-client-id",
      "Name": "WeftSpClientId",
      "Label": "Service Principal Client ID",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-cert-thumbprint",
      "Name": "WeftCertThumbprint",
      "Label": "Certificate Thumbprint",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "Sensitive" }
    },
    {
      "Id": "weft-config-file",
      "Name": "WeftConfigFile",
      "Label": "Config File",
      "DefaultValue": "weft.yaml",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-target-profile",
      "Name": "WeftTargetProfile",
      "Label": "Target Profile",
      "DefaultValue": "#{Octopus.Environment.Name}",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    },
    {
      "Id": "weft-tables",
      "Name": "WeftTables",
      "Label": "Tables",
      "HelpText": "Comma-separated list of table names to refresh.",
      "DefaultValue": "",
      "DisplaySettings": { "Octopus.ControlType": "SingleLineText" }
    }
  ]
}
```

- [ ] **Step 4: Commit**

```bash
git add build/octopus/
git commit -m "ci(octopus): step templates for weft deploy + weft refresh"
```

---

### Task 24: Final verification + merge to master + tag v1.0.0

- [ ] **Step 1: Full clean build + test**

```bash
cd /Users/marcosmagri/Documents/MUFG/weft
dotnet clean
dotnet build -warnaserror
dotnet test
```
Expected: 0 warnings, 0 errors, 109+ tests pass (Plan 3 had 107; Plan 4 Tasks 2-4 add 2+).

- [ ] **Step 2: Merge to master**

```bash
git checkout master
git merge --no-ff feature/plan-1-core-mvp -m "Merge Weft v1 (Plans 1-4) into master"
git log --oneline | head -20
```

- [ ] **Step 3: Tag v1.0.0**

```bash
git tag -a v1.0.0 -m "Weft v1.0.0 — first stable release"
git tag -l
```

Expected tags:
- `plan-1-core-mvp-complete`
- `plan-2-auth-xmla-cli-complete`
- `plan-3-config-parameters-hooks-complete`
- `plan-4-packaging-ci-docs-complete` (add in step 4)
- `v1.0.0`

- [ ] **Step 4: Tag Plan 4 milestone**

```bash
git tag -a plan-4-packaging-ci-docs-complete -m "Weft Plan 4: Packaging + CI/CD + Docs complete"
```

- [ ] **Step 5: Final git log snapshot for the record**

```bash
git log --oneline master | head -60 > /tmp/weft-v1-history.txt
wc -l /tmp/weft-v1-history.txt
head -20 /tmp/weft-v1-history.txt
rm /tmp/weft-v1-history.txt
```

No commit at this step — just a sanity check.

---

## Spec coverage check (run after Task 24)

| Spec section | Plan-4 task(s) |
|---|---|
| §11 Repo layout (final) | All Plan-4 tasks collectively |
| §12.1 TeamCity CI pipeline | Task 22 |
| §12.2 Octopus step templates | Task 23 |
| §12.4 GitHub Actions | Tasks 8, 9, 10 |
| §12.5 Release / versioning | Tasks 6, 7, 9 (release-please) |
| §11 docs list (getting-started, authentication, parameters, partition-preservation, incremental-refresh, restore-history, hooks, troubleshooting) | Tasks 19, 20, 21 |
| §11 samples list (01-04) | Tasks 15, 16, 17, 18 |
| Plan-3 review deferrals | Tasks 1, 2, 3, 4, 5 |
| NuGet pack metadata (implicit in §12.5) | Task 11 |
| README + CONTRIBUTING (implicit in §11) | Tasks 12, 13 |

Nothing material from the spec is left unaddressed.

---

## Done criteria for Plan 4

- [ ] All 24 tasks committed.
- [ ] `dotnet build -warnaserror` and `dotnet test` clean.
- [ ] `git log master | head` shows the merge commit for Plans 1-4.
- [ ] `git tag -l` includes all five tags (`plan-1`, `plan-2`, `plan-3`, `plan-4`, `v1.0.0`).
- [ ] `.github/workflows/{ci,release-please,release-artifacts}.yml` exist.
- [ ] `samples/01-04-*` directories exist with `model.bim` / `weft.yaml` / `README.md`.
- [ ] `docs/{getting-started,authentication,parameters,partition-preservation,incremental-refresh,restore-history,hooks,troubleshooting}.md` exist.
- [ ] `README.md` + `CONTRIBUTING.md` + `CHANGELOG.md` (the last one empty or seeded) exist at repo root.
- [ ] `build/teamcity/settings.kts` + `build/octopus/step-templates/{weft-deploy,weft-refresh}.json` exist.

When everything above is checked, **Weft v1.0.0 is ready to ship**. Push the branch + tags, let `release-please` open its PR, merge it, and the tagged GitHub release fires the artifact workflow.
