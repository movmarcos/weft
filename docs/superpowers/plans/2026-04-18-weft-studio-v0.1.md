# Weft Studio v0.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship **Weft Studio v0.1** — a cross-platform Avalonia desktop app that opens a `.bim` semantic model, shows the Explorer tree with Inspector + DAX editor, supports rename-measure and update-DAX edits with undo/redo, and saves back to the original `.bim` file. No diagram, no diff, no live workspace yet — those land in v0.2 / v0.3.

**Architecture:** Three-layer app on top of `Weft.Core` (consumed as a local NuGet until it ships to nuget.org). UI layer is Avalonia + ReactiveUI; App layer owns `ModelSession` (wraps TOM `Database`, exposes `ModelCommand` for writes, drives undo/redo). Views never touch TOM directly — every mutation is a command. ViewModels tested headlessly with `Avalonia.Headless.XUnit`; commands tested as plain xUnit against TOM fixtures copied from `weft/test/Weft.Core.Tests/fixtures/`.

**Tech Stack:** .NET 10, Avalonia 11.2 UI + ReactiveUI 20, AvaloniaEdit 11.x, Weft.Core (local NuGet), xUnit + FluentAssertions + Avalonia.Headless.XUnit, Microsoft.AnalysisServices.Tabular (TOM SDK, already a Weft.Core transitive dep).

**Spec reference:** `docs/superpowers/specs/2026-04-18-weft-studio-design.md`. v0.1 implements Sections 3–6 subset — everything except Diagram, Diff, and live-workspace flows.

---

## Repo strategy (revised 2026-04-18)

Monorepo: Studio lives inside the existing `movmarcos/weft` repo under a top-level `studio/` folder. The existing `weft.sln` stays untouched; CLI CI stays untouched. Studio has its own `studio/weft-studio.sln` and its own CI workflow triggered only on changes under `studio/**`. Projects in `studio/` reference `Weft.Core` via `<ProjectReference>` — no local NuGet feed, no pack step during development. The repo-level `Directory.Build.props` applies to Studio projects as-is (net10, nullable on, warnings-as-errors).

All paths below are relative to the weft repo root (`/Users/marcosmagri/Documents/MUFG/weft/`).

## File structure at end of v0.1 (new and modified files only)

```
.github/workflows/
└── studio.yml                           # NEW — builds studio/weft-studio.sln

studio/                                  # NEW top-level folder
├── weft-studio.sln
└── src/
│   ├── WeftStudio.App/                  # Application layer (no Avalonia deps)
│   │   ├── WeftStudio.App.csproj
│   │   ├── ModelSession.cs
│   │   ├── Commands/
│   │   │   ├── ModelCommand.cs
│   │   │   ├── RenameMeasureCommand.cs
│   │   │   └── UpdateDaxCommand.cs
│   │   ├── ChangeTracker.cs
│   │   └── Persistence/
│   │       └── BimSaver.cs
│   └── WeftStudio.Ui/                   # Avalonia UI + ReactiveUI
│       ├── WeftStudio.Ui.csproj
│       ├── App.axaml(.cs)
│       ├── Program.cs
│       ├── Shell/
│       │   ├── ShellWindow.axaml(.cs)
│       │   ├── ShellViewModel.cs
│       │   └── ActivityBar.axaml(.cs)
│       ├── Explorer/
│       │   ├── ExplorerView.axaml(.cs)
│       │   ├── ExplorerViewModel.cs
│       │   └── TreeNode.cs
│       ├── Inspector/
│       │   ├── InspectorView.axaml(.cs)
│       │   └── InspectorViewModel.cs
│       ├── DaxEditor/
│       │   ├── DaxEditorView.axaml(.cs)
│       │   ├── DaxEditorViewModel.cs
│       │   └── DaxSyntaxHighlighting.xshd
│       └── Settings/
│           └── SettingsStore.cs
└── test/
    ├── WeftStudio.App.Tests/
    │   ├── WeftStudio.App.Tests.csproj
    │   ├── fixtures/                    # linked from weft/test/.../fixtures
    │   │   └── simple.bim               # via <None Link="…"> in csproj
    │   ├── ModelSessionTests.cs
    │   ├── RenameMeasureCommandTests.cs
    │   ├── UpdateDaxCommandTests.cs
    │   ├── ChangeTrackerTests.cs
    │   └── BimSaverTests.cs
    └── WeftStudio.Ui.Tests/
        ├── WeftStudio.Ui.Tests.csproj
        ├── ExplorerViewModelTests.cs
        ├── InspectorViewModelTests.cs
        └── ShellViewModelTests.cs
```

**Responsibilities:**
- `WeftStudio.App` is pure logic, no UI framework reference. Fully unit-testable without Avalonia.
- `WeftStudio.Ui` owns views + viewmodels. Depends on `WeftStudio.App`.
- Tests separated by target: `App.Tests` is plain xUnit; `Ui.Tests` uses `Avalonia.Headless.XUnit`.
- Fixtures come from the existing `test/Weft.Core.Tests/fixtures/` via `<None Link>` — no duplication.

---

## Phase 1 — Repo + solution + CI scaffold

### Task 1: Create studio/ subfolder and empty solution

**Files:**
- Create: `studio/weft-studio.sln`

All other infrastructure (LICENSE, `.gitignore`, `Directory.Build.props`, `global.json`) already exists at the weft repo root and applies automatically to anything under `studio/`.

- [ ] **Step 1: Create the folder and empty solution**

```bash
mkdir -p studio
cd studio
dotnet new sln -n weft-studio
cd ..
```

- [ ] **Step 2: Verify the solution builds clean (no projects yet)**

Run: `dotnet build studio/weft-studio.sln`
Expected: "Build succeeded. 0 Warning(s) 0 Error(s)" (with no projects to build).

- [ ] **Step 3: Verify shared build props are picked up**

The repo-level `Directory.Build.props` sets `TargetFramework=net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, etc. Projects added under `studio/` in later tasks will inherit these. No file needs to be created in `studio/` to enable inheritance.

- [ ] **Step 4: Commit**

```bash
git add studio/
git commit -m "chore(studio): scaffold studio/ subfolder with empty weft-studio.sln"
```

---

### Task 2: Add studio/README.md

**Files:**
- Create: `studio/README.md`

- [ ] **Step 1: Write minimal README for the studio subfolder**

```markdown
# Weft Studio

Cross-platform desktop app for editing, visualizing, diffing, and deploying Power BI / Fabric semantic models. Companion to the [`weft`](../README.md) CLI in this repo; shares `Weft.Core` as the engine.

> **Status:** v0.1 in development. Not yet usable.

## Developing

Studio lives in its own solution within the weft monorepo — the existing CLI solution (`weft.sln` at the repo root) is unaffected by work here.

### Build and test

```bash
# From the repo root
dotnet build studio/weft-studio.sln
dotnet test  studio/weft-studio.sln
```

### Running

```bash
dotnet run --project studio/src/WeftStudio.Ui
```

## License

MIT — see the repo-level [LICENSE](../LICENSE).
```

- [ ] **Step 2: Commit**

```bash
git add studio/README.md
git commit -m "docs(studio): add README for the studio/ subfolder"
```

---

### Task 3: Scaffold Studio CI workflow

**Files:**
- Create: `.github/workflows/studio.yml`

This workflow is path-scoped — it only runs when changes touch `studio/**` or the workflow file itself, so CLI-only PRs don't wait on Studio builds.

- [ ] **Step 1: Write the workflow**

```yaml
name: Studio CI

on:
  push:
    branches: [main, master]
    paths:
      - 'studio/**'
      - 'src/Weft.Core/**'
      - 'src/Weft.Auth/**'
      - 'src/Weft.Xmla/**'
      - 'src/Weft.Config/**'
      - '.github/workflows/studio.yml'
      - 'Directory.Build.props'
  pull_request:
    branches: [main, master]
    paths:
      - 'studio/**'
      - 'src/Weft.Core/**'
      - 'src/Weft.Auth/**'
      - 'src/Weft.Xmla/**'
      - 'src/Weft.Config/**'
      - '.github/workflows/studio.yml'
      - 'Directory.Build.props'

jobs:
  build-test:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore studio/weft-studio.sln
      - name: Build
        run: dotnet build studio/weft-studio.sln --no-restore --configuration Release -warnaserror
      - name: Test
        run: dotnet test studio/weft-studio.sln --no-build --configuration Release --logger "trx"
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: trx-${{ matrix.os }}
          path: 'studio/**/TestResults/*.trx'
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/studio.yml
git commit -m "ci(studio): path-scoped matrix build+test on ubuntu/windows/macos"
```

---

## Phase 2 — Application-layer core (WeftStudio.App)

### Task 4: Create WeftStudio.App project

**Files:**
- Create: `studio/src/WeftStudio.App/WeftStudio.App.csproj`
- Modify: `studio/weft-studio.sln`

- [ ] **Step 1: Create class library**

```bash
cd studio
dotnet new classlib -o src/WeftStudio.App -n WeftStudio.App
rm src/WeftStudio.App/Class1.cs
cd ..
```

- [ ] **Step 2: Edit csproj to ProjectReference Weft.Core**

Replace the generated csproj with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\src\Weft.Core\Weft.Core.csproj" />
  </ItemGroup>
</Project>
```

(`TargetFramework`, `Nullable`, `TreatWarningsAsErrors` etc. come from the repo-level `Directory.Build.props`. Path to Weft.Core is three levels up: `studio/src/WeftStudio.App/` → `../../..` lands at the repo root.)

- [ ] **Step 3: Add to Studio solution**

```bash
dotnet sln studio/weft-studio.sln add studio/src/WeftStudio.App/WeftStudio.App.csproj
```

- [ ] **Step 4: Verify build**

```bash
dotnet build studio/weft-studio.sln
```
Expected: success — `Weft.Core` resolved via ProjectReference, no NuGet fetch needed.

- [ ] **Step 5: Commit**

```bash
git add studio/src/WeftStudio.App studio/weft-studio.sln
git commit -m "feat(studio/app): scaffold WeftStudio.App class library referencing Weft.Core"
```

---

### Task 5: Create test project for WeftStudio.App

**Files:**
- Create: `studio/test/WeftStudio.App.Tests/WeftStudio.App.Tests.csproj`
- Modify: `studio/weft-studio.sln`

- [ ] **Step 1: Create xunit project**

```bash
cd studio
dotnet new xunit -o test/WeftStudio.App.Tests -n WeftStudio.App.Tests
rm test/WeftStudio.App.Tests/UnitTest1.cs
cd ..
```

- [ ] **Step 2: Replace csproj with FluentAssertions + linked fixture**

The existing weft repo has a minimal fixture at `test/Weft.Core.Tests/fixtures/models/tiny-static.bim` (one table `DimDate`, one table `FactSales` with a measure `Total Sales`). Studio's tests link to it but expose it as `simple.bim` in the output so test code can stay neutral about which upstream fixture it comes from.

Replace the generated csproj with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="7.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\WeftStudio.App\WeftStudio.App.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- Link the existing Weft.Core fixture into this test project's output as simple.bim -->
    <None Include="..\..\..\test\Weft.Core.Tests\fixtures\models\tiny-static.bim"
          Link="fixtures\simple.bim">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add to solution and verify**

```bash
dotnet sln studio/weft-studio.sln add studio/test/WeftStudio.App.Tests/WeftStudio.App.Tests.csproj
dotnet test studio/weft-studio.sln
```
Expected: "0 total" (no tests yet).

- [ ] **Step 4: Verify fixture lands in test output**

```bash
ls studio/test/WeftStudio.App.Tests/bin/Debug/net10.0/fixtures/
```
Expected: `simple.bim` present.

- [ ] **Step 5: Commit**

```bash
git add studio/test studio/weft-studio.sln
git commit -m "test(studio/app): scaffold WeftStudio.App.Tests with linked fixture"
```

---

### Task 6: ModelSession loads a .bim and exposes the Database

**Files:**
- Create: `studio/src/WeftStudio.App/ModelSession.cs`
- Create: `studio/test/WeftStudio.App.Tests/ModelSessionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Weft.Core.Loading;
using WeftStudio.App;

namespace WeftStudio.App.Tests;

public class ModelSessionTests
{
    [Fact]
    public void OpenBim_loads_database_and_exposes_model_name()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

        var session = ModelSession.OpenBim(path);

        session.Database.Should().NotBeNull();
        session.Database.Model.Should().NotBeNull();
        session.SourcePath.Should().Be(path);
        session.IsDirty.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter ModelSessionTests`
Expected: FAIL — `ModelSession` type does not exist.

- [ ] **Step 3: Implement ModelSession**

```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Loading;

namespace WeftStudio.App;

public sealed class ModelSession
{
    public Database Database { get; }
    public string? SourcePath { get; }
    public bool IsDirty => ChangeTracker.HasUncommittedCommands;
    public ChangeTracker ChangeTracker { get; }

    private ModelSession(Database db, string? sourcePath)
    {
        Database = db;
        SourcePath = sourcePath;
        ChangeTracker = new ChangeTracker();
    }

    public static ModelSession OpenBim(string path)
    {
        var loader = new BimFileLoader();
        var database = loader.Load(path);
        return new ModelSession(database, path);
    }
}
```

Create stub `ChangeTracker` (full impl in Task 9):

```csharp
// src/WeftStudio.App/ChangeTracker.cs
namespace WeftStudio.App;

public sealed class ChangeTracker
{
    public bool HasUncommittedCommands => false;
}
```

- [ ] **Step 4: Run tests and verify pass**

Run: `dotnet test`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.App test/WeftStudio.App.Tests
git commit -m "feat(app): ModelSession.OpenBim wraps Weft.Core loader + TOM Database"
```

---

### Task 7: ModelCommand abstraction

**Files:**
- Create: `studio/src/WeftStudio.App/Commands/ModelCommand.cs`

This task introduces types used by Tasks 8 and 9. No test of its own — tests exist via the concrete commands.

- [ ] **Step 1: Write the abstract command type**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public abstract class ModelCommand
{
    /// <summary>Human-readable, shows in Undo menu.</summary>
    public abstract string Description { get; }

    /// <summary>Apply the change to the model. Throws on invalid transition.</summary>
    public abstract void Apply(Database db);

    /// <summary>Undo the change. Must leave the model identical to pre-Apply state.</summary>
    public abstract void Revert(Database db);
}
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build
git add src/WeftStudio.App/Commands/ModelCommand.cs
git commit -m "feat(app): ModelCommand abstraction (Apply / Revert / Description)"
```

---

### Task 8: RenameMeasureCommand with round-trip tests

**Files:**
- Create: `studio/src/WeftStudio.App/Commands/RenameMeasureCommand.cs`
- Create: `studio/test/WeftStudio.App.Tests/RenameMeasureCommandTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class RenameMeasureCommandTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Apply_renames_measure_on_table()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        cmd.Apply(session.Database);

        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeFalse();
        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Revenue").Should().BeTrue();
    }

    [Fact]
    public void Revert_restores_original_name()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        cmd.Apply(session.Database);
        cmd.Revert(session.Database);

        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeTrue();
    }

    [Fact]
    public void Apply_throws_when_target_name_already_exists()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var existing = session.Database.Model.Tables["FactSales"].Measures[0].Name;
        var otherExisting = session.Database.Model.Tables["FactSales"].Measures.Count > 1
            ? session.Database.Model.Tables["FactSales"].Measures[1].Name
            : existing;

        if (existing == otherExisting) return; // skip if fixture only has one measure

        var cmd = new RenameMeasureCommand("FactSales", existing, otherExisting);

        Action act = () => cmd.Apply(session.Database);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{otherExisting}*");
    }

    [Fact]
    public void Description_is_human_readable()
    {
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");
        cmd.Description.Should().Be("Rename measure Sales[Total Sales] → Revenue");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter RenameMeasureCommandTests`
Expected: compilation FAIL — type missing.

- [ ] **Step 3: Implement RenameMeasureCommand**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public sealed class RenameMeasureCommand : ModelCommand
{
    private readonly string _tableName;
    private readonly string _originalName;
    private readonly string _newName;

    public RenameMeasureCommand(string tableName, string originalName, string newName)
    {
        _tableName = tableName;
        _originalName = originalName;
        _newName = newName;
    }

    public override string Description =>
        $"Rename measure {_tableName}[{_originalName}] → {_newName}";

    public override void Apply(Database db)
    {
        var table = db.Model.Tables[_tableName];
        if (table.Measures.Contains(_newName))
            throw new InvalidOperationException(
                $"Measure '{_newName}' already exists on table '{_tableName}'.");
        table.Measures[_originalName].Name = _newName;
    }

    public override void Revert(Database db)
    {
        var table = db.Model.Tables[_tableName];
        table.Measures[_newName].Name = _originalName;
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run: `dotnet test --filter RenameMeasureCommandTests`
Expected: 4 passed (or 3 passed + 1 skipped, depending on fixture).

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.App/Commands/RenameMeasureCommand.cs test
git commit -m "feat(app): RenameMeasureCommand with duplicate-name guard and revert"
```

---

### Task 9: ChangeTracker with undo/redo

**Files:**
- Modify: `studio/src/WeftStudio.App/ChangeTracker.cs`
- Create: `studio/test/WeftStudio.App.Tests/ChangeTrackerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class ChangeTrackerTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Execute_applies_command_and_marks_dirty()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        s.ChangeTracker.Execute(s.Database, cmd);

        s.IsDirty.Should().BeTrue();
        s.ChangeTracker.UndoHistory.Should().ContainSingle()
            .Which.Description.Should().Contain("Revenue");
    }

    [Fact]
    public void Undo_reverts_the_last_command()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));

        s.ChangeTracker.Undo(s.Database);

        s.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeTrue();
        s.ChangeTracker.UndoHistory.Should().BeEmpty();
        s.ChangeTracker.RedoHistory.Should().ContainSingle();
    }

    [Fact]
    public void Redo_reapplies_an_undone_command()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
        s.ChangeTracker.Undo(s.Database);

        s.ChangeTracker.Redo(s.Database);

        s.Database.Model.Tables["FactSales"]
            .Measures.Contains("Revenue").Should().BeTrue();
    }

    [Fact]
    public void New_Execute_after_Undo_clears_redo_stack()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
        s.ChangeTracker.Undo(s.Database);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue2"));

        s.ChangeTracker.RedoHistory.Should().BeEmpty();
    }

    [Fact]
    public void MarkClean_clears_dirty_and_preserves_history()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));

        s.ChangeTracker.MarkClean();

        s.IsDirty.Should().BeFalse();
        s.ChangeTracker.UndoHistory.Should().ContainSingle();
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter ChangeTrackerTests`
Expected: multiple FAIL — `Execute`/`Undo`/`Redo`/etc. not defined.

- [ ] **Step 3: Implement ChangeTracker**

```csharp
using Microsoft.AnalysisServices.Tabular;
using WeftStudio.App.Commands;

namespace WeftStudio.App;

public sealed class ChangeTracker
{
    private readonly Stack<ModelCommand> _undo = new();
    private readonly Stack<ModelCommand> _redo = new();
    private int _cleanBoundary = 0;

    public IReadOnlyList<ModelCommand> UndoHistory => _undo.Reverse().ToList();
    public IReadOnlyList<ModelCommand> RedoHistory => _redo.Reverse().ToList();
    public bool HasUncommittedCommands => _undo.Count != _cleanBoundary;

    public void Execute(Database db, ModelCommand command)
    {
        command.Apply(db);
        _undo.Push(command);
        _redo.Clear();
    }

    public void Undo(Database db)
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Revert(db);
        _redo.Push(cmd);
    }

    public void Redo(Database db)
    {
        if (_redo.Count == 0) return;
        var cmd = _redo.Pop();
        cmd.Apply(db);
        _undo.Push(cmd);
    }

    public void MarkClean() => _cleanBoundary = _undo.Count;
}
```

- [ ] **Step 4: Run tests and verify pass**

Run: `dotnet test --filter ChangeTrackerTests`
Expected: 5 passed. Full suite still green.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.App test
git commit -m "feat(app): ChangeTracker with undo/redo/dirty-tracking + 5 tests"
```

---

### Task 10: UpdateDaxCommand

**Files:**
- Create: `studio/src/WeftStudio.App/Commands/UpdateDaxCommand.cs`
- Create: `studio/test/WeftStudio.App.Tests/UpdateDaxCommandTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class UpdateDaxCommandTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Apply_updates_measure_expression()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var measure = s.Database.Model.Tables["FactSales"].Measures[0];
        var original = measure.Expression;
        var cmd = new UpdateDaxCommand("FactSales", measure.Name, original, "SUM(FactSales[Amount])*2");

        cmd.Apply(s.Database);

        s.Database.Model.Tables["FactSales"].Measures[measure.Name].Expression
            .Should().Be("SUM(FactSales[Amount])*2");
    }

    [Fact]
    public void Revert_restores_original_expression()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var measure = s.Database.Model.Tables["FactSales"].Measures[0];
        var original = measure.Expression;
        var cmd = new UpdateDaxCommand("FactSales", measure.Name, original, "SUM(FactSales[Amount])*2");

        cmd.Apply(s.Database);
        cmd.Revert(s.Database);

        s.Database.Model.Tables["FactSales"].Measures[measure.Name].Expression
            .Should().Be(original);
    }

    [Fact]
    public void Description_is_human_readable()
    {
        var cmd = new UpdateDaxCommand("FactSales", "Total Sales", "OLD", "NEW");
        cmd.Description.Should().Be("Update DAX for Sales[Total Sales]");
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter UpdateDaxCommandTests`
Expected: compilation FAIL.

- [ ] **Step 3: Implement UpdateDaxCommand**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public sealed class UpdateDaxCommand : ModelCommand
{
    private readonly string _tableName;
    private readonly string _measureName;
    private readonly string _originalExpression;
    private readonly string _newExpression;

    public UpdateDaxCommand(string tableName, string measureName,
        string originalExpression, string newExpression)
    {
        _tableName = tableName;
        _measureName = measureName;
        _originalExpression = originalExpression;
        _newExpression = newExpression;
    }

    public override string Description =>
        $"Update DAX for {_tableName}[{_measureName}]";

    public override void Apply(Database db) =>
        db.Model.Tables[_tableName].Measures[_measureName].Expression = _newExpression;

    public override void Revert(Database db) =>
        db.Model.Tables[_tableName].Measures[_measureName].Expression = _originalExpression;
}
```

- [ ] **Step 4: Run and verify pass**

Run: `dotnet test --filter UpdateDaxCommandTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.App test
git commit -m "feat(app): UpdateDaxCommand with round-trip + 3 tests"
```

---

### Task 11: BimSaver writes model back to disk

**Files:**
- Create: `studio/src/WeftStudio.App/Persistence/BimSaver.cs`
- Create: `studio/test/WeftStudio.App.Tests/BimSaverTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.App.Commands;
using WeftStudio.App.Persistence;

namespace WeftStudio.App.Tests;

public class BimSaverTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Save_writes_current_database_state_to_disk()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(FixturePath, tmp, overwrite: true);

        try
        {
            var s = ModelSession.OpenBim(tmp);
            s.ChangeTracker.Execute(s.Database,
                new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
            BimSaver.Save(s);

            var reloaded = ModelSession.OpenBim(tmp);
            reloaded.Database.Model.Tables["FactSales"].Measures.Contains("Revenue")
                .Should().BeTrue();
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Save_marks_session_clean()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(FixturePath, tmp, overwrite: true);

        try
        {
            var s = ModelSession.OpenBim(tmp);
            s.ChangeTracker.Execute(s.Database,
                new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
            s.IsDirty.Should().BeTrue();

            BimSaver.Save(s);

            s.IsDirty.Should().BeFalse();
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Save_throws_when_session_has_no_source_path()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var sessionWithoutPath = new ModelSession(s.Database, sourcePath: null);

        Action act = () => BimSaver.Save(sessionWithoutPath);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no source path*");
    }
}
```

The third test requires ModelSession to expose a non-public constructor for tests. Two changes needed (applied in Step 3):

1. In `studio/src/WeftStudio.App/WeftStudio.App.csproj`, expose internals to the test project:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="WeftStudio.App.Tests" />
</ItemGroup>
```

2. In `studio/src/WeftStudio.App/ModelSession.cs`, change the constructor from `private` to `internal`:

```csharp
internal ModelSession(Database db, string? sourcePath)
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter BimSaverTests`
Expected: compilation FAIL — `BimSaver` missing.

- [ ] **Step 3: Implement BimSaver**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Persistence;

public static class BimSaver
{
    public static void Save(ModelSession session)
    {
        if (string.IsNullOrEmpty(session.SourcePath))
            throw new InvalidOperationException(
                "Cannot save: session has no source path. Use Save-As instead.");

        var json = JsonSerializer.SerializeDatabase(session.Database,
            new SerializeOptions { IgnoreInferredObjects = true,
                                   IgnoreInferredProperties = true,
                                   IgnoreTimestamps = true });

        File.WriteAllText(session.SourcePath!, json);
        session.ChangeTracker.MarkClean();
    }
}
```

Also update `ModelSession` constructor accessibility and add `InternalsVisibleTo` as shown in Step 1.

- [ ] **Step 4: Run and verify pass**

Run: `dotnet test`
Expected: all tests pass, including the 3 new `BimSaver` tests.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.App test
git commit -m "feat(app): BimSaver serializes TOM Database back to .bim and marks clean"
```

---

## Phase 3 — UI layer scaffolding (WeftStudio.Ui)

### Task 12: Create WeftStudio.Ui project with Avalonia + ReactiveUI

**Files:**
- Create: `studio/src/WeftStudio.Ui/WeftStudio.Ui.csproj`
- Create: `studio/src/WeftStudio.Ui/Program.cs`
- Create: `studio/src/WeftStudio.Ui/App.axaml`
- Create: `studio/src/WeftStudio.Ui/App.axaml.cs`
- Modify: `weft-studio.sln`

- [ ] **Step 1: Create Avalonia MVVM template**

```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.mvvm -o src/WeftStudio.Ui -n WeftStudio.Ui
```

- [ ] **Step 2: Trim csproj to essentials and add refs**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.*" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.*" />
    <PackageReference Include="Avalonia.ReactiveUI" Version="11.2.*" />
    <PackageReference Include="AvaloniaEdit" Version="11.*" />
    <PackageReference Include="AvaloniaEdit.TextMate" Version="11.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WeftStudio.App\WeftStudio.App.csproj" />
  </ItemGroup>
</Project>
```

Remove the template's `ViewModels/`, `Views/` generated samples; keep only `App.axaml(.cs)` and `Program.cs`.

- [ ] **Step 3: Add to solution and verify build**

```bash
dotnet sln add src/WeftStudio.Ui/WeftStudio.Ui.csproj
dotnet build src/WeftStudio.Ui
```
Expected: success.

- [ ] **Step 4: Verify runs (sanity check)**

Run: `dotnet run --project src/WeftStudio.Ui`
Expected: blank Avalonia window opens. Close.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.Ui weft-studio.sln
git commit -m "feat(ui): scaffold WeftStudio.Ui with Avalonia 11 + ReactiveUI + AvaloniaEdit"
```

---

### Task 13: Create WeftStudio.Ui.Tests with Avalonia.Headless

**Files:**
- Create: `studio/test/WeftStudio.Ui.Tests/WeftStudio.Ui.Tests.csproj`
- Create: `studio/test/WeftStudio.Ui.Tests/HeadlessAppBuilder.cs`

- [ ] **Step 1: Create xunit project**

```bash
dotnet new xunit -o test/WeftStudio.Ui.Tests -n WeftStudio.Ui.Tests
rm test/WeftStudio.Ui.Tests/UnitTest1.cs
```

- [ ] **Step 2: Edit csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="7.*" />
    <PackageReference Include="Avalonia.Headless.XUnit" Version="11.2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\WeftStudio.Ui\WeftStudio.Ui.csproj" />
    <ProjectReference Include="..\..\src\WeftStudio.App\WeftStudio.App.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add headless test harness**

```csharp
// test/WeftStudio.Ui.Tests/HeadlessAppBuilder.cs
using Avalonia;
using Avalonia.Headless;
using WeftStudio.Ui;

[assembly: AvaloniaTestApplication(typeof(WeftStudio.Ui.Tests.TestAppBuilder))]

namespace WeftStudio.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

- [ ] **Step 4: Add to solution and run**

```bash
dotnet sln add test/WeftStudio.Ui.Tests
dotnet test test/WeftStudio.Ui.Tests
```
Expected: "0 total".

- [ ] **Step 5: Commit**

```bash
git add test/WeftStudio.Ui.Tests weft-studio.sln
git commit -m "test(ui): scaffold UI tests with Avalonia.Headless.XUnit"
```

---

### Task 14: ShellViewModel exposes active-mode state

**Files:**
- Create: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/ShellViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class ShellViewModelTests
{
    [Fact]
    public void Defaults_to_explorer_mode()
    {
        var vm = new ShellViewModel();
        vm.ActiveMode.Should().Be(ActivityMode.Explorer);
    }

    [Fact]
    public void SwitchTo_changes_active_mode_and_raises_property_changed()
    {
        var vm = new ShellViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
            { if (e.PropertyName == nameof(vm.ActiveMode)) raised = true; };

        vm.ActiveMode = ActivityMode.Diagram;

        vm.ActiveMode.Should().Be(ActivityMode.Diagram);
        raised.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter ShellViewModelTests`
Expected: compilation FAIL.

- [ ] **Step 3: Implement ShellViewModel**

```csharp
// src/WeftStudio.Ui/Shell/ShellViewModel.cs
using ReactiveUI;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private ActivityMode _activeMode = ActivityMode.Explorer;

    public ActivityMode ActiveMode
    {
        get => _activeMode;
        set => this.RaiseAndSetIfChanged(ref _activeMode, value);
    }
}
```

- [ ] **Step 4: Run and verify pass**

Run: `dotnet test --filter ShellViewModelTests`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.Ui/Shell test
git commit -m "feat(ui): ShellViewModel with ActivityMode state + ReactiveUI"
```

---

### Task 15: ShellWindow with activity bar (visual only, Explorer placeholder)

**Files:**
- Create: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml`
- Create: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml.cs`
- Create: `studio/src/WeftStudio.Ui/Shell/ActivityBar.axaml`
- Create: `studio/src/WeftStudio.Ui/Shell/ActivityBar.axaml.cs`
- Modify: `studio/src/WeftStudio.Ui/App.axaml.cs`

This task is a visual-only scaffold. Verification is "it runs and looks right"; no VM tests.

- [ ] **Step 1: Write ShellWindow XAML**

```xml
<!-- src/WeftStudio.Ui/Shell/ShellWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="using:WeftStudio.Ui.Shell"
        x:Class="WeftStudio.Ui.Shell.ShellWindow"
        x:DataType="shell:ShellViewModel"
        Title="Weft Studio"
        Width="1200" Height="800">
  <Grid ColumnDefinitions="48,250,*,300" RowDefinitions="*,24">
    <shell:ActivityBar Grid.Column="0" Grid.Row="0"/>
    <Border Grid.Column="1" Grid.Row="0" Background="#F4F4F4"
            BorderBrush="#DDD" BorderThickness="0,0,1,0">
      <TextBlock Text="Explorer (placeholder)" Margin="12"/>
    </Border>
    <Border Grid.Column="2" Grid.Row="0">
      <TextBlock Text="Editor tabs go here" Margin="12" VerticalAlignment="Center"
                 HorizontalAlignment="Center"/>
    </Border>
    <Border Grid.Column="3" Grid.Row="0" Background="#FAFAFA"
            BorderBrush="#DDD" BorderThickness="1,0,0,0">
      <TextBlock Text="Inspector" Margin="12"/>
    </Border>
    <Border Grid.ColumnSpan="4" Grid.Row="1" Background="#2A2D31">
      <TextBlock Text="Ready" Foreground="#DDD" Margin="12,0" VerticalAlignment="Center"/>
    </Border>
  </Grid>
</Window>
```

- [ ] **Step 2: Write ActivityBar XAML**

```xml
<!-- src/WeftStudio.Ui/Shell/ActivityBar.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="WeftStudio.Ui.Shell.ActivityBar">
  <StackPanel Background="#333" Spacing="8" Margin="0,8">
    <Button Content="☰" Classes="activity" ToolTip.Tip="Explorer"/>
    <Button Content="◈" Classes="activity" ToolTip.Tip="Diagram (v0.2)" IsEnabled="False"/>
    <Button Content="⇄" Classes="activity" ToolTip.Tip="Diff (v0.3)" IsEnabled="False"/>
    <Button Content="🔎" Classes="activity" ToolTip.Tip="Search (v1.0)" IsEnabled="False"/>
  </StackPanel>
  <UserControl.Styles>
    <Style Selector="Button.activity">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="#CCC"/>
      <Setter Property="FontSize" Value="18"/>
      <Setter Property="HorizontalAlignment" Value="Center"/>
      <Setter Property="Padding" Value="10,6"/>
    </Style>
  </UserControl.Styles>
</UserControl>
```

- [ ] **Step 3: Wire ShellWindow as main window in App.axaml.cs**

```csharp
// src/WeftStudio.Ui/App.axaml.cs
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShellWindow { DataContext = new ShellViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: Add code-behinds**

```csharp
// ShellWindow.axaml.cs
using Avalonia.Controls;
namespace WeftStudio.Ui.Shell;
public partial class ShellWindow : Window { public ShellWindow() => InitializeComponent(); }

// ActivityBar.axaml.cs
using Avalonia.Controls;
namespace WeftStudio.Ui.Shell;
public partial class ActivityBar : UserControl { public ActivityBar() => InitializeComponent(); }
```

- [ ] **Step 5: Run and eyeball**

Run: `dotnet run --project src/WeftStudio.Ui`
Expected: window with dark activity bar, light-gray Explorer column, center area, right Inspector column, dark status bar at bottom.

- [ ] **Step 6: Commit**

```bash
git add src/WeftStudio.Ui
git commit -m "feat(ui): ShellWindow + ActivityBar scaffolded with 4-column layout"
```

---

## Phase 4 — Explorer tree wired to ModelSession

### Task 16: ExplorerViewModel builds tree from a ModelSession

**Files:**
- Create: `studio/src/WeftStudio.Ui/Explorer/ExplorerViewModel.cs`
- Create: `studio/src/WeftStudio.Ui/Explorer/TreeNode.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/ExplorerViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Tests;

public class ExplorerViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                     "WeftStudio.App.Tests", "fixtures", "simple.bim");

    [Fact]
    public void Root_shows_Tables_and_Measures_categories()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        vm.Roots.Select(r => r.DisplayName)
            .Should().Contain(new[] { "Tables", "Measures", "Relationships" });
    }

    [Fact]
    public void Tables_node_lists_each_table()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        var tablesNode = vm.Roots.Single(r => r.DisplayName == "Tables");
        tablesNode.Children.Should().NotBeEmpty();
        tablesNode.Children.Select(c => c.DisplayName).Should().Contain("FactSales");
    }

    [Fact]
    public void Measures_node_flattens_all_measures_across_tables()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        var measuresNode = vm.Roots.Single(r => r.DisplayName == "Measures");
        measuresNode.Children.Should().NotBeEmpty();
    }
}
```

Also copy the fixture to `WeftStudio.Ui.Tests/fixtures/` or share via `MSBuild` `<Link>` element in the csproj — for consistency across projects, add to Ui.Tests csproj:

```xml
<ItemGroup>
  <None Include="..\WeftStudio.App.Tests\fixtures\simple.bim" Link="fixtures\simple.bim">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

And update the test to:
```csharp
private static string FixturePath =>
    Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter ExplorerViewModelTests`
Expected: compilation FAIL.

- [ ] **Step 3: Implement TreeNode and ExplorerViewModel**

```csharp
// src/WeftStudio.Ui/Explorer/TreeNode.cs
using System.Collections.ObjectModel;
using ReactiveUI;

namespace WeftStudio.Ui.Explorer;

public sealed class TreeNode : ReactiveObject
{
    public TreeNode(string displayName, object? payload = null)
    {
        DisplayName = displayName;
        Payload = payload;
    }
    public string DisplayName { get; }
    public object? Payload { get; }
    public ObservableCollection<TreeNode> Children { get; } = new();
}
```

```csharp
// src/WeftStudio.Ui/Explorer/ExplorerViewModel.cs
using System.Collections.ObjectModel;
using ReactiveUI;
using WeftStudio.App;

namespace WeftStudio.Ui.Explorer;

public sealed class ExplorerViewModel : ReactiveObject
{
    public ExplorerViewModel(ModelSession session)
    {
        Session = session;
        Roots = BuildRoots(session);
    }

    public ModelSession Session { get; }
    public ObservableCollection<TreeNode> Roots { get; }

    private static ObservableCollection<TreeNode> BuildRoots(ModelSession s)
    {
        var tables = new TreeNode("Tables");
        foreach (var t in s.Database.Model.Tables)
        {
            var node = new TreeNode(t.Name, t);
            foreach (var c in t.Columns)  node.Children.Add(new TreeNode(c.Name, c));
            foreach (var m in t.Measures) node.Children.Add(new TreeNode(m.Name, m));
            tables.Children.Add(node);
        }

        var measures = new TreeNode("Measures");
        foreach (var t in s.Database.Model.Tables)
            foreach (var m in t.Measures)
                measures.Children.Add(new TreeNode($"{t.Name}[{m.Name}]", m));

        var rels = new TreeNode("Relationships");
        foreach (var r in s.Database.Model.Relationships)
            rels.Children.Add(new TreeNode(r.Name, r));

        return new ObservableCollection<TreeNode> { tables, measures, rels };
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run: `dotnet test --filter ExplorerViewModelTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.Ui/Explorer test/WeftStudio.Ui.Tests
git commit -m "feat(ui): ExplorerViewModel builds tree (Tables / Measures / Relationships)"
```

---

### Task 17: ExplorerView renders the tree; wire into Shell

**Files:**
- Create: `studio/src/WeftStudio.Ui/Explorer/ExplorerView.axaml`
- Create: `studio/src/WeftStudio.Ui/Explorer/ExplorerView.axaml.cs`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` (replace placeholder)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs` (expose Explorer VM)
- Modify: `studio/src/WeftStudio.Ui/App.axaml.cs` (open a default file at startup for demoable v0.1)

Shell startup flow for v0.1: accept an optional `.bim` path as command-line arg, otherwise show a "No file open" placeholder. Full File-menu lands in Task 19.

- [ ] **Step 1: Write ExplorerView XAML**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:explorer="using:WeftStudio.Ui.Explorer"
             x:Class="WeftStudio.Ui.Explorer.ExplorerView"
             x:DataType="explorer:ExplorerViewModel">
  <TreeView ItemsSource="{Binding Roots}">
    <TreeView.ItemTemplate>
      <TreeDataTemplate ItemsSource="{Binding Children}">
        <TextBlock Text="{Binding DisplayName}"/>
      </TreeDataTemplate>
    </TreeView.ItemTemplate>
  </TreeView>
</UserControl>
```

Code-behind:
```csharp
using Avalonia.Controls;
namespace WeftStudio.Ui.Explorer;
public partial class ExplorerView : UserControl { public ExplorerView() => InitializeComponent(); }
```

- [ ] **Step 2: Update ShellViewModel to expose Explorer VM**

```csharp
using ReactiveUI;
using WeftStudio.App;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private ActivityMode _activeMode = ActivityMode.Explorer;
    private ExplorerViewModel? _explorer;

    public ActivityMode ActiveMode
    {
        get => _activeMode;
        set => this.RaiseAndSetIfChanged(ref _activeMode, value);
    }

    public ExplorerViewModel? Explorer
    {
        get => _explorer;
        set => this.RaiseAndSetIfChanged(ref _explorer, value);
    }

    public void OpenModel(string bimPath)
    {
        var session = ModelSession.OpenBim(bimPath);
        Explorer = new ExplorerViewModel(session);
    }
}
```

- [ ] **Step 3: Replace placeholder in ShellWindow.axaml**

```xml
<!-- In the second Grid.Column="1" Border -->
<Border Grid.Column="1" Grid.Row="0" Background="#F4F4F4"
        BorderBrush="#DDD" BorderThickness="0,0,1,0">
  <ContentControl Content="{Binding Explorer}">
    <ContentControl.DataTemplates>
      <DataTemplate DataType="explorer:ExplorerViewModel">
        <explorer:ExplorerView/>
      </DataTemplate>
    </ContentControl.DataTemplates>
  </ContentControl>
</Border>
```

Add `xmlns:explorer="using:WeftStudio.Ui.Explorer"` to ShellWindow's root.

- [ ] **Step 4: Wire command-line arg → OpenModel in App.axaml.cs**

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = new ShellViewModel();
        var args = desktop.Args ?? Array.Empty<string>();
        if (args.Length > 0 && File.Exists(args[0]))
            vm.OpenModel(args[0]);
        desktop.MainWindow = new ShellWindow { DataContext = vm };
    }
    base.OnFrameworkInitializationCompleted();
}
```

- [ ] **Step 5: Run and verify**

```bash
dotnet run --project src/WeftStudio.Ui -- test/WeftStudio.App.Tests/fixtures/simple.bim
```

Expected: window opens, Explorer tree on the left shows Tables / Measures / Relationships roots with children.

- [ ] **Step 6: Commit**

```bash
git add src/WeftStudio.Ui
git commit -m "feat(ui): ExplorerView wired to ShellViewModel.Explorer; CLI-arg opens a bim"
```

---

## Phase 5 — DAX editor tab

### Task 18: DaxEditorViewModel and DAX syntax highlighting resource

**Files:**
- Create: `studio/src/WeftStudio.Ui/DaxEditor/DaxEditorViewModel.cs`
- Create: `studio/src/WeftStudio.Ui/DaxEditor/DaxSyntaxHighlighting.xshd`
- Create: `studio/src/WeftStudio.Ui/DaxEditor/DaxEditorView.axaml` + `.cs`
- Modify: `studio/src/WeftStudio.Ui/WeftStudio.Ui.csproj` (embed the xshd)

The DAX grammar covers a minimal set for v0.1: keywords (`TRUE`, `FALSE`, `NOT`, `AND`, `OR`, `IF`, ...), top-level functions (`SUM`, `CALCULATE`, `FILTER`, ...), string literals, numeric literals, comments (`--`, `/* */`), brackets, operators. Extending the function list later is a pure-data change.

- [ ] **Step 1: Write the grammar (minimal v0.1 set)**

```xml
<!-- src/WeftStudio.Ui/DaxEditor/DaxSyntaxHighlighting.xshd -->
<SyntaxDefinition name="DAX" extensions=".dax"
                  xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment"      foreground="#6A9955"/>
  <Color name="String"       foreground="#CE9178"/>
  <Color name="Number"       foreground="#B5CEA8"/>
  <Color name="Keyword"      foreground="#569CD6" fontWeight="bold"/>
  <Color name="Function"     foreground="#DCDCAA"/>
  <Color name="TableColumn"  foreground="#9CDCFE"/>

  <RuleSet ignoreCase="true">
    <Span color="Comment" begin="--" />
    <Span color="Comment" multiline="true" begin="/\*" end="\*/" />
    <Span color="String"  begin="&quot;" end="&quot;" escapeCharacter="\"/>
    <Span color="TableColumn" begin="\[" end="\]"/>

    <Keywords color="Keyword">
      <Word>TRUE</Word><Word>FALSE</Word><Word>NOT</Word><Word>AND</Word>
      <Word>OR</Word><Word>IN</Word><Word>IF</Word><Word>SWITCH</Word>
      <Word>VAR</Word><Word>RETURN</Word><Word>BLANK</Word>
    </Keywords>

    <Keywords color="Function">
      <Word>SUM</Word><Word>SUMX</Word><Word>AVERAGE</Word><Word>COUNT</Word>
      <Word>COUNTROWS</Word><Word>DISTINCTCOUNT</Word><Word>CALCULATE</Word>
      <Word>CALCULATETABLE</Word><Word>FILTER</Word><Word>ALL</Word>
      <Word>ALLEXCEPT</Word><Word>ALLSELECTED</Word><Word>RELATED</Word>
      <Word>RELATEDTABLE</Word><Word>USERELATIONSHIP</Word><Word>DIVIDE</Word>
      <Word>IFERROR</Word><Word>ISBLANK</Word><Word>LOOKUPVALUE</Word>
      <Word>SELECTEDVALUE</Word><Word>HASONEVALUE</Word>
      <Word>DATEADD</Word><Word>DATESYTD</Word><Word>DATESMTD</Word>
      <Word>DATESQTD</Word><Word>TOTALYTD</Word><Word>SAMEPERIODLASTYEAR</Word>
      <Word>FORMAT</Word><Word>CONCATENATE</Word><Word>CONCATENATEX</Word>
    </Keywords>

    <Rule color="Number">\b\d+(\.\d+)?\b</Rule>
  </RuleSet>
</SyntaxDefinition>
```

Embed it in the csproj:

```xml
<ItemGroup>
  <EmbeddedResource Include="DaxEditor\DaxSyntaxHighlighting.xshd"/>
</ItemGroup>
```

- [ ] **Step 2: Write DaxEditorViewModel**

```csharp
// src/WeftStudio.Ui/DaxEditor/DaxEditorViewModel.cs
using ReactiveUI;
using WeftStudio.App;
using WeftStudio.App.Commands;

namespace WeftStudio.Ui.DaxEditor;

public sealed class DaxEditorViewModel : ReactiveObject
{
    private string _text = "";
    private readonly ModelSession _session;
    private readonly string _tableName;
    private readonly string _measureName;
    private string _originalText;

    public DaxEditorViewModel(ModelSession session, string tableName, string measureName)
    {
        _session = session;
        _tableName = tableName;
        _measureName = measureName;
        _originalText = session.Database.Model.Tables[tableName]
            .Measures[measureName].Expression ?? "";
        _text = _originalText;
    }

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public string MeasureName => _measureName;
    public string TableName   => _tableName;
    public bool IsDirty       => _text != _originalText;

    public void Commit()
    {
        if (!IsDirty) return;
        var cmd = new UpdateDaxCommand(_tableName, _measureName, _originalText, _text);
        _session.ChangeTracker.Execute(_session.Database, cmd);
        _originalText = _text;
        this.RaisePropertyChanged(nameof(IsDirty));
    }
}
```

- [ ] **Step 3: Write DaxEditorView with AvaloniaEdit**

```xml
<!-- src/WeftStudio.Ui/DaxEditor/DaxEditorView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:avaloniaEdit="using:AvaloniaEdit"
             xmlns:dax="using:WeftStudio.Ui.DaxEditor"
             x:Class="WeftStudio.Ui.DaxEditor.DaxEditorView"
             x:DataType="dax:DaxEditorViewModel">
  <Grid RowDefinitions="Auto,*">
    <Border Grid.Row="0" Background="#EEE" Padding="8,4">
      <TextBlock>
        <Run Text="{Binding TableName}"/>
        <Run Text="["/>
        <Run Text="{Binding MeasureName}"/>
        <Run Text="]"/>
      </TextBlock>
    </Border>
    <avaloniaEdit:TextEditor Name="Editor" Grid.Row="1"
                              FontFamily="Cascadia Mono,Menlo,monospace"
                              ShowLineNumbers="True"/>
  </Grid>
</UserControl>
```

Code-behind wires AvaloniaEdit manually (AvaloniaEdit's document binding is imperative in the control):

```csharp
// src/WeftStudio.Ui/DaxEditor/DaxEditorView.axaml.cs
using System.Reflection;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace WeftStudio.Ui.DaxEditor;

public partial class DaxEditorView : UserControl
{
    private static IHighlightingDefinition? _daxDef;
    private TextEditor? _editor;

    public DaxEditorView() => InitializeComponent();

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = this.FindControl<TextEditor>("Editor");
        _editor!.SyntaxHighlighting = LoadDaxHighlighting();

        DataContextChanged += (_, _) => Sync();
        _editor.TextChanged += (_, _) =>
        {
            if (DataContext is DaxEditorViewModel vm) vm.Text = _editor!.Text;
        };
        Sync();
    }

    private void Sync()
    {
        if (_editor is null) return;
        if (DataContext is DaxEditorViewModel vm && _editor.Text != vm.Text)
            _editor.Text = vm.Text;
    }

    private static IHighlightingDefinition LoadDaxHighlighting()
    {
        if (_daxDef is not null) return _daxDef;
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("WeftStudio.Ui.DaxEditor.DaxSyntaxHighlighting.xshd")
            ?? throw new InvalidOperationException("DAX xshd not embedded");
        using var reader = XmlReader.Create(stream);
        _daxDef = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return _daxDef;
    }
}
```

- [ ] **Step 4: Add a VM test for Commit behaviour**

```csharp
// test/WeftStudio.Ui.Tests/DaxEditorViewModelTests.cs
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.DaxEditor;

namespace WeftStudio.Ui.Tests;

public class DaxEditorViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Commit_applies_UpdateDaxCommand_to_session()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new DaxEditorViewModel(s, "FactSales", m.Name);

        vm.Text = "SUM(FactSales[Amount])*2";
        vm.Commit();

        s.Database.Model.Tables["FactSales"].Measures[m.Name].Expression
            .Should().Be("SUM(FactSales[Amount])*2");
        s.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Commit_no_op_when_text_unchanged()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new DaxEditorViewModel(s, "FactSales", m.Name);

        vm.Commit();

        s.IsDirty.Should().BeFalse();
    }
}
```

Run: `dotnet test`
Expected: all tests pass (including 2 new).

- [ ] **Step 5: Commit**

```bash
git add src/WeftStudio.Ui/DaxEditor test/WeftStudio.Ui.Tests
git commit -m "feat(ui): DaxEditor (AvaloniaEdit + xshd DAX grammar) + VM tests"
```

---

### Task 19: Click a measure in Explorer → open DAX editor tab

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs` (add OpenTabs collection + OpenMeasure method)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` (replace "Editor tabs" placeholder with TabControl)
- Modify: `studio/src/WeftStudio.Ui/Explorer/ExplorerView.axaml` (double-click binds to open)
- Modify: `studio/src/WeftStudio.Ui/Explorer/ExplorerView.axaml.cs` (raise open event to Shell)

- [ ] **Step 1: Write a failing VM test for OpenMeasure**

```csharp
// test/WeftStudio.Ui.Tests/ShellViewModelTests.cs — append
[Fact]
public void OpenMeasure_adds_tab_and_activates_it()
{
    var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
    var vm = new ShellViewModel();
    vm.OpenModel(fixture);

    var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];
    vm.OpenMeasure("FactSales", measure.Name);

    vm.OpenTabs.Should().ContainSingle()
        .Which.MeasureName.Should().Be(measure.Name);
    vm.ActiveTab.Should().Be(vm.OpenTabs[0]);
}

[Fact]
public void OpenMeasure_focuses_existing_tab_if_already_open()
{
    var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
    var vm = new ShellViewModel();
    vm.OpenModel(fixture);
    var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];
    vm.OpenMeasure("FactSales", measure.Name);
    vm.OpenMeasure("FactSales", measure.Name);

    vm.OpenTabs.Should().ContainSingle();
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test --filter ShellViewModelTests`
Expected: compilation FAIL.

- [ ] **Step 3: Implement OpenTabs / OpenMeasure on ShellViewModel**

```csharp
// Append to ShellViewModel
using System.Collections.ObjectModel;
using WeftStudio.Ui.DaxEditor;

// ... inside ShellViewModel:
public ObservableCollection<DaxEditorViewModel> OpenTabs { get; } = new();

private DaxEditorViewModel? _activeTab;
public DaxEditorViewModel? ActiveTab
{
    get => _activeTab;
    set => this.RaiseAndSetIfChanged(ref _activeTab, value);
}

public void OpenMeasure(string tableName, string measureName)
{
    if (Explorer is null) return;
    var existing = OpenTabs.FirstOrDefault(
        t => t.TableName == tableName && t.MeasureName == measureName);
    if (existing is not null)
    {
        ActiveTab = existing;
        return;
    }
    var tab = new DaxEditorViewModel(Explorer.Session, tableName, measureName);
    OpenTabs.Add(tab);
    ActiveTab = tab;
}
```

- [ ] **Step 4: Update ShellWindow XAML with TabControl in center column**

```xml
<!-- Replace the center Border (Grid.Column="2") with: -->
<TabControl Grid.Column="2" Grid.Row="0"
            ItemsSource="{Binding OpenTabs}"
            SelectedItem="{Binding ActiveTab}">
  <TabControl.ItemTemplate>
    <DataTemplate>
      <TextBlock>
        <Run Text="{Binding TableName}"/><Run Text="["/>
        <Run Text="{Binding MeasureName}"/><Run Text="]"/>
      </TextBlock>
    </DataTemplate>
  </TabControl.ItemTemplate>
  <TabControl.ContentTemplate>
    <DataTemplate>
      <dax:DaxEditorView/>
    </DataTemplate>
  </TabControl.ContentTemplate>
</TabControl>
```

Add `xmlns:dax="using:WeftStudio.Ui.DaxEditor"` to ShellWindow root.

- [ ] **Step 5: Wire Explorer double-click → Shell.OpenMeasure**

In `ExplorerView.axaml`, give the TreeView a name so code-behind can subscribe:

```xml
<TreeView Name="Tree" ItemsSource="{Binding Roots}">
  <!-- (ItemTemplate unchanged from Task 17) -->
</TreeView>
```

Subscribe to `DoubleTapped` in the code-behind and expose a typed event the Shell can listen to:

```csharp
// ExplorerView.axaml.cs
using Avalonia.Input;
using Microsoft.AnalysisServices.Tabular;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
        this.FindControl<TreeView>("Tree")!.DoubleTapped += OnDouble;
    }

    public event Action<string, string>? MeasureDoubleClicked;

    private void OnDouble(object? sender, TappedEventArgs e)
    {
        if (sender is not TreeView tv) return;
        if (tv.SelectedItem is not TreeNode n || n.Payload is not Measure m) return;
        MeasureDoubleClicked?.Invoke(m.Table.Name, m.Name);
    }
}
```

In `ShellWindow.axaml.cs`, wire the explorer view's event to ShellViewModel:

```csharp
public ShellWindow()
{
    InitializeComponent();
    Loaded += (_, _) =>
    {
        var explorerView = this.FindDescendantOfType<ExplorerView>();
        if (explorerView is null) return;
        explorerView.MeasureDoubleClicked += (table, measure) =>
        {
            if (DataContext is ShellViewModel vm) vm.OpenMeasure(table, measure);
        };
    };
}
```

(`FindDescendantOfType` from `Avalonia.VisualTree.VisualExtensions`.)

- [ ] **Step 6: Run VM tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 7: Run the app and verify manually**

```bash
dotnet run --project src/WeftStudio.Ui -- test/WeftStudio.App.Tests/fixtures/simple.bim
```

Expected: double-click a measure under Measures → a tab opens with the DAX shown and highlighted.

- [ ] **Step 8: Commit**

```bash
git add src/WeftStudio.Ui test/WeftStudio.Ui.Tests
git commit -m "feat(ui): Explorer double-click opens DAX editor tab via ShellViewModel.OpenMeasure"
```

---

## Phase 6 — Inspector + rename + save

### Task 20: InspectorViewModel shows selected-measure properties

**Files:**
- Create: `studio/src/WeftStudio.Ui/Inspector/InspectorViewModel.cs`
- Create: `studio/src/WeftStudio.Ui/Inspector/InspectorView.axaml` + `.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/InspectorViewModelTests.cs`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs` (expose Inspector property, sync to ActiveTab)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` (right-column placeholder → InspectorView)

- [ ] **Step 1: Write failing InspectorViewModel tests**

```csharp
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.App.Commands;
using WeftStudio.Ui.Inspector;

namespace WeftStudio.Ui.Tests;

public class InspectorViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Reflects_measure_name_and_format()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new InspectorViewModel(s, "FactSales", m.Name);

        vm.Name.Should().Be(m.Name);
    }

    [Fact]
    public void Renaming_via_inspector_applies_RenameMeasureCommand()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var originalName = m.Name;
        var vm = new InspectorViewModel(s, "FactSales", originalName);

        vm.Name = "Renamed";
        vm.CommitRename();

        s.Database.Model.Tables["FactSales"].Measures.Contains("Renamed")
            .Should().BeTrue();
        s.IsDirty.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run and verify failure**

Expected: compilation FAIL.

- [ ] **Step 3: Implement InspectorViewModel**

```csharp
// src/WeftStudio.Ui/Inspector/InspectorViewModel.cs
using ReactiveUI;
using WeftStudio.App;
using WeftStudio.App.Commands;

namespace WeftStudio.Ui.Inspector;

public sealed class InspectorViewModel : ReactiveObject
{
    private readonly ModelSession _session;
    private readonly string _tableName;
    private string _originalName;
    private string _name;

    public InspectorViewModel(ModelSession session, string tableName, string measureName)
    {
        _session = session;
        _tableName = tableName;
        _originalName = measureName;
        _name = measureName;
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public void CommitRename()
    {
        if (_name == _originalName) return;
        var cmd = new RenameMeasureCommand(_tableName, _originalName, _name);
        _session.ChangeTracker.Execute(_session.Database, cmd);
        _originalName = _name;
    }
}
```

- [ ] **Step 4: Write InspectorView XAML**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:inspector="using:WeftStudio.Ui.Inspector"
             x:Class="WeftStudio.Ui.Inspector.InspectorView"
             x:DataType="inspector:InspectorViewModel">
  <StackPanel Margin="12" Spacing="8">
    <TextBlock Text="MEASURE" Foreground="#888" FontSize="10"/>
    <TextBlock Text="Name"/>
    <TextBox Text="{Binding Name}" LostFocus="OnNameLostFocus"/>
  </StackPanel>
</UserControl>
```

Code-behind:
```csharp
using Avalonia.Controls;
namespace WeftStudio.Ui.Inspector;
public partial class InspectorView : UserControl
{
    public InspectorView() => InitializeComponent();
    private void OnNameLostFocus(object? sender, Avalonia.Input.RoutedEventArgs e)
    {
        if (DataContext is InspectorViewModel vm) vm.CommitRename();
    }
}
```

- [ ] **Step 5: Expose Inspector on ShellViewModel, sync to ActiveTab**

```csharp
// In ShellViewModel
private InspectorViewModel? _inspector;
public InspectorViewModel? Inspector
{
    get => _inspector;
    set => this.RaiseAndSetIfChanged(ref _inspector, value);
}

public ShellViewModel()
{
    this.WhenAnyValue(x => x.ActiveTab).Subscribe(tab =>
    {
        if (tab is null || Explorer is null) Inspector = null;
        else Inspector = new InspectorViewModel(Explorer.Session, tab.TableName, tab.MeasureName);
    });
}
```

Add `using System.Reactive.Linq;` and `using ReactiveUI;` if not present.

- [ ] **Step 6: Replace right-column placeholder in ShellWindow.axaml**

```xml
<Border Grid.Column="3" Grid.Row="0" Background="#FAFAFA"
        BorderBrush="#DDD" BorderThickness="1,0,0,0">
  <ContentControl Content="{Binding Inspector}">
    <ContentControl.DataTemplates>
      <DataTemplate DataType="inspector:InspectorViewModel">
        <inspector:InspectorView/>
      </DataTemplate>
    </ContentControl.DataTemplates>
  </ContentControl>
</Border>
```

Add `xmlns:inspector="using:WeftStudio.Ui.Inspector"`.

- [ ] **Step 7: Run tests and app**

```bash
dotnet test
dotnet run --project src/WeftStudio.Ui -- test/WeftStudio.App.Tests/fixtures/simple.bim
```

Expected: opening a measure populates the right-side Inspector with its name; editing and tabbing away renames it (status bar of the measure tab title updates next task).

- [ ] **Step 8: Commit**

```bash
git add src/WeftStudio.Ui test/WeftStudio.Ui.Tests
git commit -m "feat(ui): InspectorView for measure rename; ShellVM syncs Inspector to ActiveTab"
```

---

### Task 21: File menu — Open, Save, Save As, Exit

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml` (add Menu at top)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs` (add commands)
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml.cs` (file picker invocation)

- [ ] **Step 1: Add commands on ShellViewModel**

```csharp
// On ShellViewModel
using System.Reactive;
using WeftStudio.App.Persistence;

public ReactiveCommand<Unit, Unit> SaveCommand { get; }
public ReactiveCommand<string, Unit> OpenModelCommand { get; }

public ShellViewModel()
{
    // ...existing constructor body...

    var canSave = this.WhenAnyValue(x => x.Explorer)
        .Select(exp => exp is not null);

    SaveCommand = ReactiveCommand.Create(() =>
    {
        if (Explorer is not null) BimSaver.Save(Explorer.Session);
    }, canSave);

    OpenModelCommand = ReactiveCommand.Create<string>(OpenModel);
}
```

Add using: `using System.Reactive;`, `using System.Reactive.Linq;`.

- [ ] **Step 2: Add a test for SaveCommand**

```csharp
// Append to ShellViewModelTests
[Fact]
public async Task SaveCommand_persists_changes_and_clears_dirty()
{
    var tmp = Path.GetTempFileName() + ".bim";
    File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"),
              tmp, overwrite: true);
    try
    {
        var vm = new ShellViewModel();
        vm.OpenModel(tmp);
        var m = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];
        vm.Explorer.Session.ChangeTracker.Execute(
            vm.Explorer.Session.Database,
            new WeftStudio.App.Commands.RenameMeasureCommand("FactSales", m.Name, "Renamed"));

        vm.Explorer.Session.IsDirty.Should().BeTrue();
        await vm.SaveCommand.Execute();
        vm.Explorer.Session.IsDirty.Should().BeFalse();
    }
    finally { File.Delete(tmp); }
}
```

Run: `dotnet test` — expected PASS.

- [ ] **Step 3: Add menu in ShellWindow.axaml**

Put above the Grid (adjust root to StackPanel or Dock):

```xml
<DockPanel>
  <Menu DockPanel.Dock="Top">
    <MenuItem Header="_File">
      <MenuItem Header="_Open..." Click="OnFileOpen"/>
      <MenuItem Header="_Save" Command="{Binding SaveCommand}"/>
      <Separator/>
      <MenuItem Header="E_xit" Click="OnExit"/>
    </MenuItem>
  </Menu>
  <Grid ColumnDefinitions="48,250,*,300" RowDefinitions="*,24">
    <!-- ...existing columns... -->
  </Grid>
</DockPanel>
```

- [ ] **Step 4: Add OnFileOpen / OnExit code-behind**

```csharp
// ShellWindow.axaml.cs
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;

private async void OnFileOpen(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    var picker = StorageProvider;
    var files = await picker.OpenFilePickerAsync(new FilePickerOpenOptions
    {
        Title = "Open a .bim model",
        AllowMultiple = false,
        FileTypeFilter = new[]
        {
            new FilePickerFileType("Power BI model") { Patterns = new[] { "*.bim" } }
        }
    });
    var f = files.FirstOrDefault()?.TryGetLocalPath();
    if (f is not null && DataContext is ShellViewModel vm)
        await vm.OpenModelCommand.Execute(f);
}

private void OnExit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt)
        dt.Shutdown();
}
```

- [ ] **Step 5: Manual verification**

```bash
dotnet run --project src/WeftStudio.Ui
```

Expected: File → Open picks a `.bim`, tree populates. Edit a measure, File → Save overwrites the file. Reopen confirms persistence.

- [ ] **Step 6: Commit**

```bash
git add src/WeftStudio.Ui test/WeftStudio.Ui.Tests
git commit -m "feat(ui): File menu (Open / Save / Exit) wired to SaveCommand + native picker"
```

---

## Phase 7 — Settings & polish

### Task 22: SettingsStore persists recent files

**Files:**
- Create: `studio/src/WeftStudio.Ui/Settings/SettingsStore.cs`
- Create: `studio/test/WeftStudio.Ui.Tests/SettingsStoreTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using WeftStudio.Ui.Settings;

namespace WeftStudio.Ui.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Save_then_load_round_trips_RecentFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(dir);
            var data = new Settings { RecentFiles = { "a.bim", "b.bim" } };
            store.Save(data);

            var store2 = new SettingsStore(dir);
            var loaded = store2.Load();
            loaded.RecentFiles.Should().Equal("a.bim", "b.bim");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_returns_empty_when_no_file_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        var store = new SettingsStore(dir);
        var loaded = store.Load();
        loaded.RecentFiles.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run and verify failure**

- [ ] **Step 3: Implement SettingsStore + Settings**

```csharp
// src/WeftStudio.Ui/Settings/Settings.cs
namespace WeftStudio.Ui.Settings;
public sealed class Settings
{
    public List<string> RecentFiles { get; set; } = new();
}
```

```csharp
// src/WeftStudio.Ui/Settings/SettingsStore.cs
using System.Text.Json;

namespace WeftStudio.Ui.Settings;

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WeftStudio");

    public Settings Load() =>
        File.Exists(_path)
            ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings()
            : new Settings();

    public void Save(Settings s) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
}
```

- [ ] **Step 4: Run tests and verify pass**

Expected: 2 passed.

- [ ] **Step 5: Wire into ShellViewModel — push path to recents on OpenModel**

```csharp
// On ShellViewModel — add field and update OpenModel
private readonly SettingsStore _store = new(SettingsStore.DefaultDirectory);

public void OpenModel(string bimPath)
{
    var session = ModelSession.OpenBim(bimPath);
    Explorer = new ExplorerViewModel(session);

    var s = _store.Load();
    s.RecentFiles.Remove(bimPath);
    s.RecentFiles.Insert(0, bimPath);
    if (s.RecentFiles.Count > 10) s.RecentFiles.RemoveRange(10, s.RecentFiles.Count - 10);
    _store.Save(s);
}
```

- [ ] **Step 6: Commit**

```bash
git add src/WeftStudio.Ui test/WeftStudio.Ui.Tests
git commit -m "feat(ui): SettingsStore with recent-files; ShellVM appends on open"
```

---

### Task 23: Status bar reflects dirty state + model name

**Files:**
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellViewModel.cs`
- Modify: `studio/src/WeftStudio.Ui/Shell/ShellWindow.axaml`

- [ ] **Step 1: Expose status-bar strings on ShellViewModel**

```csharp
// ShellViewModel
public string StatusText => Explorer is null
    ? "No model open"
    : $"{Path.GetFileName(Explorer.Session.SourcePath)}" +
      (Explorer.Session.IsDirty ? " · unsaved changes" : "");

public ShellViewModel()
{
    // existing body; add:
    this.WhenAnyValue(x => x.Explorer).Subscribe(_ =>
        this.RaisePropertyChanged(nameof(StatusText)));
}
```

Note: `IsDirty` changes don't raise property-changed today — Task 9's `ChangeTracker` doesn't notify. For v0.1 StatusText refreshes on Open and Save only, which matches expectations.

- [ ] **Step 2: Update the status bar binding**

```xml
<Border Grid.ColumnSpan="4" Grid.Row="1" Background="#2A2D31">
  <TextBlock Text="{Binding StatusText}" Foreground="#DDD"
             Margin="12,0" VerticalAlignment="Center"/>
</Border>
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet run --project src/WeftStudio.Ui -- test/WeftStudio.App.Tests/fixtures/simple.bim
```

Expected: status bar shows `simple.bim` on open, `simple.bim · unsaved changes` after an edit, back to `simple.bim` after save.

- [ ] **Step 4: Commit**

```bash
git add src/WeftStudio.Ui
git commit -m "feat(ui): status bar reflects current model name + dirty state"
```

---

### Task 24: End-to-end smoke test

**Files:**
- Create: `studio/test/WeftStudio.Ui.Tests/EndToEndSmokeTests.cs`

This task validates the v0.1 gesture: open → double-click → edit → save → reload preserves change.

- [ ] **Step 1: Write the smoke test**

```csharp
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class EndToEndSmokeTests
{
    [Fact]
    public async Task Open_edit_save_reload_round_trip()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"),
                  tmp, overwrite: true);
        try
        {
            var vm = new ShellViewModel();
            vm.OpenModel(tmp);
            var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];

            vm.OpenMeasure("FactSales", measure.Name);
            vm.ActiveTab!.Text = "SUM(FactSales[Amount]) * 2";
            vm.ActiveTab.Commit();

            await vm.SaveCommand.Execute();

            var vm2 = new ShellViewModel();
            vm2.OpenModel(tmp);
            vm2.Explorer!.Session.Database.Model.Tables["FactSales"]
                .Measures[measure.Name].Expression.Should().Be("SUM(FactSales[Amount]) * 2");
        }
        finally { File.Delete(tmp); }
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test`
Expected: all tests pass including the smoke test.

- [ ] **Step 3: Commit**

```bash
git add test/WeftStudio.Ui.Tests
git commit -m "test(ui): end-to-end smoke — open/edit/save/reload round trip"
```

---

### Task 25: Tag v0.1

**Files:**
- None (git operation)

- [ ] **Step 1: Verify clean tree and full CI passes locally**

```bash
git status    # expect: clean
dotnet build -c Release -warnaserror
dotnet test  -c Release
```

Expected: build + all tests green.

- [ ] **Step 2: Tag and push**

```bash
git tag v0.1.0 -m "Weft Studio v0.1 — open, browse, edit, save .bim models"
git push --tags
```

- [ ] **Step 3: Open an issue for v0.2 scope**

Not part of this plan — manual step to document the next milestone.

---

## Total

25 tasks across 7 phases. Each task is committed independently, so intermediate state is always shippable. Reference commits:

- Phase 1 (Tasks 1-3): repo scaffold
- Phase 2 (Tasks 4-11): application-layer core with full test coverage
- Phase 3 (Tasks 12-15): Avalonia UI scaffold
- Phase 4 (Tasks 16-17): Explorer wired to ModelSession
- Phase 5 (Tasks 18-19): DAX editor + tab opening
- Phase 6 (Tasks 20-21): Inspector + File menu
- Phase 7 (Tasks 22-25): settings, status bar, smoke test, tag

**Explicitly out of scope for v0.1 (documented for v0.2 plan):**
- Diagram canvas + schema ERD rendering
- Dependency tracing
- Diff mode + deploy
- Live workspace connection
- Search mode
- Command-based undo/redo UI (Undo / Redo menu items — ChangeTracker supports it; UI wiring is v0.2)
- Saving Tabular Editor folder format (v0.1 does `.bim` only)
- Installer / auto-update
