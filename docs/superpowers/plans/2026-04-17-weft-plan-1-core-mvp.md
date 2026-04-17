# Weft Plan 1 — Core MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the pure-logic core of Weft — load source models, compare them against target snapshots, classify tables (Static / DynamicallyPartitioned / IncrementalRefreshPolicy), produce a transactional TMSL `sequence` script that preserves partitions and bookmarks, and enforce the partition-integrity invariant. No network calls, no AAD — everything runs against in-memory TOM `Database` fixtures and is fully unit-tested.

**Architecture:** A single .NET 10 class library `Weft.Core` plus a test project `Weft.Core.Tests`. Public surface is small: `IModelLoader`, `ModelDiffer`, `TmslBuilder`, `PartitionManifest`, `RefreshPolicyAnalyzer`, `RestorePartitionSet`. Each module has one responsibility and is unit-testable in isolation. The integration with XMLA, auth, CLI, and config is deferred to Plan 2 and Plan 3.

**Tech Stack:** .NET 10, Microsoft.AnalysisServices.NetCore.retail.amd64 (TOM), xUnit, FluentAssertions, Verify.Xunit, System.Text.Json.

**Reference spec:** `docs/superpowers/specs/2026-04-17-weft-design.md`. Sections this plan implements: §5.1 ModelLoader, §5.3 ModelDiffer, §5.4 TmslBuilder, §7A.1–§7A.2 (classification + policy diffing), §7A.7 bookmark preservation logic, §7A.8 history-loss retention math, §7A.9 restore partition-set computation.

**Out of this plan (deferred):**
- Auth, XMLA execution, refresh runner — Plan 2.
- CLI commands and System.CommandLine wiring — Plan 2.
- YAML config loading, parameter resolver, hooks — Plan 3.
- TeamCity / Octopus / packaging / docs — Plan 4.

---

## File structure (locked in by this plan)

```
weft/
├── global.json                          # pins .NET 10 SDK
├── Directory.Build.props                # shared C# settings
├── .editorconfig
├── .gitignore
├── LICENSE                              # MIT (placeholder header for now)
├── weft.sln
└── src/
│   └── Weft.Core/
│       ├── Weft.Core.csproj
│       ├── Loading/
│       │   ├── IModelLoader.cs
│       │   ├── ModelLoaderFactory.cs
│       │   ├── BimFileLoader.cs
│       │   └── TabularEditorFolderLoader.cs
│       ├── Partitions/
│       │   ├── PartitionManifest.cs
│       │   ├── PartitionRecord.cs
│       │   ├── PartitionManifestReader.cs
│       │   └── PartitionManifestWriter.cs
│       ├── Diffing/
│       │   ├── ChangeSet.cs
│       │   ├── TablePlan.cs
│       │   ├── TableDiff.cs
│       │   ├── TableClassification.cs
│       │   ├── TableClassifier.cs
│       │   ├── ModelDiffer.cs
│       │   ├── Comparers/
│       │   │   ├── ColumnComparer.cs
│       │   │   ├── MeasureComparer.cs
│       │   │   ├── HierarchyComparer.cs
│       │   │   ├── RelationshipComparer.cs
│       │   │   └── RoleComparer.cs
│       │   └── Exceptions/
│       │       └── UnknownObjectTypeException.cs
│       ├── RefreshPolicy/
│       │   ├── RefreshPolicyComparer.cs
│       │   └── RetentionCalculator.cs
│       ├── Restore/
│       │   └── RestorePartitionSet.cs
│       └── Tmsl/
│           ├── TmslBuilder.cs
│           ├── TmslSequence.cs
│           ├── PartitionIntegrityException.cs
│           └── PartitionIntegrityValidator.cs
└── test/
    └── Weft.Core.Tests/
        ├── Weft.Core.Tests.csproj
        ├── Loading/
        ├── Partitions/
        ├── Diffing/
        ├── RefreshPolicy/
        ├── Restore/
        ├── Tmsl/
        ├── Snapshots/                  # Verify .verified.txt files
        └── fixtures/
            ├── models/
            │   ├── tiny-static/
            │   ├── tiny-incremental/
            │   ├── tiny-dynamic/
            │   └── *.bim
            └── manifests/
```

---

## Task 1: Repository scaffolding

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `weft.sln` (via `dotnet new sln -n weft`)

- [ ] **Step 1: Pin SDK**

Create `global.json`:
```json
{
  "sdk": { "version": "10.0.105", "rollForward": "latestFeature" }
}
```

- [ ] **Step 2: Shared C# settings**

Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Editor + ignore**

Create `.editorconfig` with standard C# rules (4-space indent, `csharp_style_*` defaults, `dotnet_diagnostic.IDE0073.severity = warning`).

Create `.gitignore` from `dotnet new gitignore` content; add `artifacts/`, `*.user`, `.vs/`.

- [ ] **Step 4: License (MIT placeholder)**

Create `LICENSE` with the standard MIT text, copyright "Marcos Magri / Weft contributors".

- [ ] **Step 5: Create empty solution**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet new sln -n weft
```

- [ ] **Step 6: Verify and commit**

```bash
dotnet --version    # confirm 10.0.x
ls weft.sln Directory.Build.props global.json .gitignore LICENSE
git add global.json Directory.Build.props .editorconfig .gitignore LICENSE weft.sln
git commit -m "chore: repo scaffolding (sdk pin, build props, license, solution)"
```

---

## Task 2: Create `Weft.Core` project

**Files:**
- Create: `src/Weft.Core/Weft.Core.csproj`

- [ ] **Step 1: New library**

```bash
mkdir -p src/Weft.Core
cd src/Weft.Core
dotnet new classlib -n Weft.Core -o . --force
rm -f Class1.cs
```

- [ ] **Step 2: Add TOM dependency**

Edit `src/Weft.Core/Weft.Core.csproj` to add the package reference:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AnalysisServices.NetCore.retail.amd64" Version="19.84.1" />
</ItemGroup>
```

- [ ] **Step 3: Add to solution**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add src/Weft.Core/Weft.Core.csproj
dotnet build
```

Expected: build succeeds with 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/Weft.Core/ weft.sln
git commit -m "feat(core): add Weft.Core class library with TOM dependency"
```

---

## Task 3: Create `Weft.Core.Tests` project

**Files:**
- Create: `test/Weft.Core.Tests/Weft.Core.Tests.csproj`

- [ ] **Step 1: New test project**

```bash
mkdir -p test/Weft.Core.Tests
cd test/Weft.Core.Tests
dotnet new xunit -o . --force
rm -f UnitTest1.cs
```

- [ ] **Step 2: Add test packages**

Add to `test/Weft.Core.Tests/Weft.Core.Tests.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="FluentAssertions" Version="6.12.1" />
  <PackageReference Include="Verify.Xunit" Version="24.2.0" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\..\src\Weft.Core\Weft.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 3: Sanity test**

Create `test/Weft.Core.Tests/Sanity/SanityTests.cs`:
```csharp
using FluentAssertions;
using Xunit;

namespace Weft.Core.Tests.Sanity;

public class SanityTests
{
    [Fact]
    public void Tom_assembly_loads()
    {
        var dbType = typeof(Microsoft.AnalysisServices.Tabular.Database);
        dbType.Should().NotBeNull();
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet sln add test/Weft.Core.Tests/Weft.Core.Tests.csproj
dotnet test
```

Expected: 1 test passes.

```bash
git add test/Weft.Core.Tests/ weft.sln
git commit -m "test(core): scaffold Weft.Core.Tests with xUnit + FluentAssertions + Verify"
```

---

## Task 4: Tiny-static fixture model

**Files:**
- Create: `test/Weft.Core.Tests/fixtures/models/tiny-static.bim`
- Create: `test/Weft.Core.Tests/Fixtures/FixtureLoader.cs`

- [ ] **Step 1: Author the fixture**

Create `test/Weft.Core.Tests/fixtures/models/tiny-static.bim`. This is a minimal valid TMSL Database JSON: one data source, two tables (`DimDate` with one column + one partition; `FactSales` with two columns + one partition), one measure, one relationship.

```json
{
  "name": "TinyStatic",
  "compatibilityLevel": 1600,
  "model": {
    "culture": "en-US",
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
      },
      {
        "name": "FactSales",
        "columns": [
          { "name": "Date", "dataType": "dateTime", "sourceColumn": "Date" },
          { "name": "Amount", "dataType": "double", "sourceColumn": "Amount" }
        ],
        "partitions": [
          {
            "name": "FactSales",
            "mode": "import",
            "source": { "type": "m", "expression": "let Source = #table({\"Date\",\"Amount\"}, {}) in Source" }
          }
        ],
        "measures": [
          { "name": "Total Sales", "expression": "SUM(FactSales[Amount])" }
        ]
      }
    ],
    "relationships": [
      {
        "name": "rel_factsales_dimdate",
        "fromTable": "FactSales", "fromColumn": "Date",
        "toTable":   "DimDate",   "toColumn":   "Date"
      }
    ]
  }
}
```

Mark the file as content copied to output:
```xml
<!-- in Weft.Core.Tests.csproj -->
<ItemGroup>
  <None Update="fixtures\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: Helper to resolve fixture paths**

Create `test/Weft.Core.Tests/Fixtures/FixtureLoader.cs`:
```csharp
using System.IO;
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Tests.Fixtures;

public static class FixtureLoader
{
    public static string FixturePath(params string[] segments)
        => Path.Combine(new[] { AppContext.BaseDirectory, "fixtures" }.Concat(segments).ToArray());

    public static Database LoadBim(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath);
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.DeserializeDatabase(json);
    }
}
```

- [ ] **Step 3: Smoke test**

Create `test/Weft.Core.Tests/Fixtures/FixtureLoaderTests.cs`:
```csharp
using FluentAssertions;
using Xunit;

namespace Weft.Core.Tests.Fixtures;

public class FixtureLoaderTests
{
    [Fact]
    public void Loads_tiny_static_with_two_tables()
    {
        var db = FixtureLoader.LoadBim("models/tiny-static.bim");
        db.Name.Should().Be("TinyStatic");
        db.Model.Tables.Should().HaveCount(2);
        db.Model.Tables["FactSales"].Measures.Should().ContainSingle(m => m.Name == "Total Sales");
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~FixtureLoaderTests
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add test/Weft.Core.Tests/
git commit -m "test(core): add tiny-static fixture and FixtureLoader helper"
```

---

## Task 5: `IModelLoader` and `BimFileLoader`

**Files:**
- Create: `src/Weft.Core/Loading/IModelLoader.cs`
- Create: `src/Weft.Core/Loading/BimFileLoader.cs`
- Create: `test/Weft.Core.Tests/Loading/BimFileLoaderTests.cs`

- [ ] **Step 1: Failing test**

Create `test/Weft.Core.Tests/Loading/BimFileLoaderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Core.Loading;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class BimFileLoaderTests
{
    [Fact]
    public void Loads_a_bim_file_into_a_TOM_database()
    {
        var path = FixtureLoader.FixturePath("models", "tiny-static.bim");
        var loader = new BimFileLoader();

        var db = loader.Load(path);

        db.Name.Should().Be("TinyStatic");
        db.Model.Tables.Should().HaveCount(2);
    }

    [Fact]
    public void Throws_on_missing_file()
    {
        var loader = new BimFileLoader();
        var act = () => loader.Load("/no/such/path.bim");
        act.Should().Throw<FileNotFoundException>();
    }
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~BimFileLoaderTests
```
Expected: FAIL — `BimFileLoader` not defined.

- [ ] **Step 2: Implement**

Create `src/Weft.Core/Loading/IModelLoader.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Loading;

public interface IModelLoader
{
    Database Load(string path);
}
```

Create `src/Weft.Core/Loading/BimFileLoader.cs`:
```csharp
using System.IO;
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Loading;

public sealed class BimFileLoader : IModelLoader
{
    public Database Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Source .bim not found: {path}", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.DeserializeDatabase(json);
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~BimFileLoaderTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Loading/IModelLoader.cs src/Weft.Core/Loading/BimFileLoader.cs test/Weft.Core.Tests/Loading/
git commit -m "feat(core): IModelLoader + BimFileLoader"
```

---

## Task 6: `ModelLoaderFactory` (path → loader selection)

**Files:**
- Create: `src/Weft.Core/Loading/ModelLoaderFactory.cs`
- Create: `test/Weft.Core.Tests/Loading/ModelLoaderFactoryTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Weft.Core.Loading;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class ModelLoaderFactoryTests
{
    [Fact]
    public void Picks_BimFileLoader_for_bim_paths()
    {
        var loader = ModelLoaderFactory.For("/some/path/model.bim");
        loader.Should().BeOfType<BimFileLoader>();
    }

    [Fact]
    public void Picks_TabularEditorFolderLoader_for_directories()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var loader = ModelLoaderFactory.For(dir);
        loader.Should().BeOfType<TabularEditorFolderLoader>();
    }

    [Fact]
    public void Throws_for_unknown_path()
    {
        var act = () => ModelLoaderFactory.For("/no/such/thing.xyz");
        act.Should().Throw<FileNotFoundException>();
    }
}
```

Expected: FAIL — factory and folder loader missing.

- [ ] **Step 2: Stub `TabularEditorFolderLoader` (impl in Task 7)**

Create `src/Weft.Core/Loading/TabularEditorFolderLoader.cs`:
```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Loading;

public sealed class TabularEditorFolderLoader : IModelLoader
{
    public Database Load(string path) => throw new NotImplementedException("Implemented in Task 7");
}
```

- [ ] **Step 3: Implement factory**

Create `src/Weft.Core/Loading/ModelLoaderFactory.cs`:
```csharp
namespace Weft.Core.Loading;

public static class ModelLoaderFactory
{
    public static IModelLoader For(string path)
    {
        if (Directory.Exists(path)) return new TabularEditorFolderLoader();
        if (File.Exists(path) && path.EndsWith(".bim", StringComparison.OrdinalIgnoreCase))
            return new BimFileLoader();
        throw new FileNotFoundException($"Cannot resolve a model loader for path: {path}", path);
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ModelLoaderFactoryTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Loading/ test/Weft.Core.Tests/Loading/ModelLoaderFactoryTests.cs
git commit -m "feat(core): ModelLoaderFactory selects loader by path shape"
```

---

## Task 7: `TabularEditorFolderLoader`

**Files:**
- Modify: `src/Weft.Core/Loading/TabularEditorFolderLoader.cs`
- Create: `test/Weft.Core.Tests/fixtures/models/tiny-folder/` (folder fixture)
- Create: `test/Weft.Core.Tests/Loading/TabularEditorFolderLoaderTests.cs`

**Background:** Tabular Editor's "Save to Folder" splits the model into one `database.json` plus per-table folders (`tables/<TableName>/table.json`) and per-object child files. This loader stitches them back into a single in-memory `Database`. We use the simplest layout TE 2 produces: per-table JSON files merged into the model.

- [ ] **Step 1: Build the fixture**

Create directory `test/Weft.Core.Tests/fixtures/models/tiny-folder/` with the following files. The structure mirrors what TE writes when "Save to Folder" is enabled with default settings.

`tiny-folder/database.json`:
```json
{
  "name": "TinyFolder",
  "compatibilityLevel": 1600,
  "model": {
    "culture": "en-US",
    "tables": []
  }
}
```

`tiny-folder/tables/DimDate.json`:
```json
{
  "name": "DimDate",
  "columns": [{ "name": "Date", "dataType": "dateTime", "sourceColumn": "Date" }],
  "partitions": [{
    "name": "DimDate", "mode": "import",
    "source": { "type": "m", "expression": "let Source = #table({\"Date\"}, {{#date(2025,1,1)}}) in Source" }
  }]
}
```

`tiny-folder/tables/FactSales.json`:
```json
{
  "name": "FactSales",
  "columns": [
    { "name": "Date", "dataType": "dateTime", "sourceColumn": "Date" },
    { "name": "Amount", "dataType": "double", "sourceColumn": "Amount" }
  ],
  "partitions": [{
    "name": "FactSales", "mode": "import",
    "source": { "type": "m", "expression": "let Source = #table({\"Date\",\"Amount\"}, {}) in Source" }
  }],
  "measures": [{ "name": "Total Sales", "expression": "SUM(FactSales[Amount])" }]
}
```

- [ ] **Step 2: Failing test**

Create `test/Weft.Core.Tests/Loading/TabularEditorFolderLoaderTests.cs`:
```csharp
using FluentAssertions;
using Weft.Core.Loading;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class TabularEditorFolderLoaderTests
{
    [Fact]
    public void Stitches_folder_into_a_TOM_database()
    {
        var dir = FixtureLoader.FixturePath("models", "tiny-folder");
        var loader = new TabularEditorFolderLoader();

        var db = loader.Load(dir);

        db.Name.Should().Be("TinyFolder");
        db.Model.Tables.Select(t => t.Name).Should().BeEquivalentTo(new[] { "DimDate", "FactSales" });
        db.Model.Tables["FactSales"].Measures.Should().ContainSingle(m => m.Name == "Total Sales");
    }

    [Fact]
    public void Throws_on_missing_database_json()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var loader = new TabularEditorFolderLoader();
        var act = () => loader.Load(dir);
        act.Should().Throw<FileNotFoundException>().WithMessage("*database.json*");
    }
}
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Replace `src/Weft.Core/Loading/TabularEditorFolderLoader.cs`:
```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using TomJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;

namespace Weft.Core.Loading;

public sealed class TabularEditorFolderLoader : IModelLoader
{
    public Database Load(string path)
    {
        var dbPath = Path.Combine(path, "database.json");
        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"database.json not found in {path}", dbPath);

        var root = JsonNode.Parse(File.ReadAllText(dbPath))!.AsObject();
        var model = root["model"]!.AsObject();
        var tables = (model["tables"] as JsonArray) ?? new JsonArray();

        var tablesDir = Path.Combine(path, "tables");
        if (Directory.Exists(tablesDir))
        {
            foreach (var file in Directory.EnumerateFiles(tablesDir, "*.json", SearchOption.AllDirectories))
            {
                var tableNode = JsonNode.Parse(File.ReadAllText(file))!;
                tables.Add(tableNode.DeepClone());
            }
        }
        model["tables"] = tables;

        return TomJsonSerializer.DeserializeDatabase(root.ToJsonString());
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~TabularEditorFolderLoaderTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Loading/TabularEditorFolderLoader.cs test/Weft.Core.Tests/fixtures/models/tiny-folder test/Weft.Core.Tests/Loading/TabularEditorFolderLoaderTests.cs
git commit -m "feat(core): TabularEditorFolderLoader stitches folder model into TOM Database"
```

---

## Task 8: `PartitionRecord` + `PartitionManifest` types

**Files:**
- Create: `src/Weft.Core/Partitions/PartitionRecord.cs`
- Create: `src/Weft.Core/Partitions/PartitionManifest.cs`

- [ ] **Step 1: Define data shapes (no logic yet)**

Create `src/Weft.Core/Partitions/PartitionRecord.cs`:
```csharp
namespace Weft.Core.Partitions;

public sealed record PartitionRecord(
    string Name,
    string? RefreshBookmark,
    DateTime? ModifiedTime,
    long? RowCount);
```

Create `src/Weft.Core/Partitions/PartitionManifest.cs`:
```csharp
namespace Weft.Core.Partitions;

public sealed record PartitionManifest(
    DateTimeOffset CapturedAtUtc,
    string TargetDatabase,
    IReadOnlyDictionary<string, IReadOnlyList<PartitionRecord>> Tables);
```

- [ ] **Step 2: Compile + commit (no test yet — readers/writers in next tasks)**

```bash
dotnet build
git add src/Weft.Core/Partitions/
git commit -m "feat(core): PartitionRecord + PartitionManifest data types"
```

---

## Task 9: `PartitionManifestReader` (TOM Database → manifest)

**Files:**
- Create: `src/Weft.Core/Partitions/PartitionManifestReader.cs`
- Create: `test/Weft.Core.Tests/Partitions/PartitionManifestReaderTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Weft.Core.Partitions;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Partitions;

public class PartitionManifestReaderTests
{
    [Fact]
    public void Captures_all_tables_and_partitions_with_bookmarks()
    {
        var db = FixtureLoader.LoadBim("models/tiny-static.bim");
        // Simulate a bookmark on FactSales partition
        db.Model.Tables["FactSales"].Partitions["FactSales"].RefreshBookmark = "wm-001";

        var manifest = new PartitionManifestReader().Read(db);

        manifest.TargetDatabase.Should().Be("TinyStatic");
        manifest.Tables.Should().HaveCount(2);
        manifest.Tables["FactSales"].Should().ContainSingle()
            .Which.RefreshBookmark.Should().Be("wm-001");
        manifest.Tables["DimDate"].Should().ContainSingle()
            .Which.RefreshBookmark.Should().BeNull();
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Partitions;

public sealed class PartitionManifestReader
{
    public PartitionManifest Read(Database database)
    {
        var tables = new Dictionary<string, IReadOnlyList<PartitionRecord>>();
        foreach (var table in database.Model.Tables)
        {
            var records = table.Partitions
                .Select(p => new PartitionRecord(
                    Name: p.Name,
                    RefreshBookmark: string.IsNullOrEmpty(p.RefreshBookmark) ? null : p.RefreshBookmark,
                    ModifiedTime: p.ModifiedTime == default ? null : p.ModifiedTime,
                    RowCount: null))
                .ToList();
            tables[table.Name] = records;
        }

        return new PartitionManifest(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            TargetDatabase: database.Name,
            Tables: tables);
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~PartitionManifestReaderTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Partitions/PartitionManifestReader.cs test/Weft.Core.Tests/Partitions/
git commit -m "feat(core): PartitionManifestReader captures partitions + bookmarks from TOM Database"
```

---

## Task 10: `PartitionManifestWriter` (manifest → JSON)

**Files:**
- Create: `src/Weft.Core/Partitions/PartitionManifestWriter.cs`
- Create: `test/Weft.Core.Tests/Partitions/PartitionManifestWriterTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using System.Text.Json;
using FluentAssertions;
using Weft.Core.Partitions;
using Xunit;

namespace Weft.Core.Tests.Partitions;

public class PartitionManifestWriterTests
{
    [Fact]
    public void Round_trips_manifest_through_json()
    {
        var manifest = new PartitionManifest(
            CapturedAtUtc: new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
            TargetDatabase: "TinyStatic",
            Tables: new Dictionary<string, IReadOnlyList<PartitionRecord>>
            {
                ["FactSales"] = new[] { new PartitionRecord("FactSales", "wm-001", null, null) }
            });

        var json = new PartitionManifestWriter().ToJson(manifest);
        var parsed = JsonSerializer.Deserialize<PartitionManifest>(json,
            PartitionManifestWriter.JsonOptions)!;

        parsed.TargetDatabase.Should().Be("TinyStatic");
        parsed.Tables["FactSales"][0].RefreshBookmark.Should().Be("wm-001");
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weft.Core.Partitions;

public sealed class PartitionManifestWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson(PartitionManifest manifest)
        => JsonSerializer.Serialize(manifest, JsonOptions);

    public void Write(PartitionManifest manifest, string path)
        => File.WriteAllText(path, ToJson(manifest));
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~PartitionManifestWriterTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Partitions/PartitionManifestWriter.cs test/Weft.Core.Tests/Partitions/PartitionManifestWriterTests.cs
git commit -m "feat(core): PartitionManifestWriter round-trips manifest as JSON"
```

---

## Task 11: `TableClassification` enum + `TableClassifier`

**Files:**
- Create: `src/Weft.Core/Diffing/TableClassification.cs`
- Create: `src/Weft.Core/Diffing/TableClassifier.cs`
- Create: `test/Weft.Core.Tests/Diffing/TableClassifierTests.cs`

- [ ] **Step 1: Define the enum**

```csharp
namespace Weft.Core.Diffing;

public enum TableClassification
{
    Static,
    DynamicallyPartitioned,
    IncrementalRefreshPolicy
}
```

- [ ] **Step 2: Failing test**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Diffing;

public class TableClassifierTests
{
    [Fact]
    public void Static_when_partitions_match_and_no_policy()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.Static);
    }

    [Fact]
    public void DynamicallyPartitioned_when_target_has_extra_partitions()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        // Target has an extra partition not in source
        var extra = new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        };
        tgt.Model.Tables["FactSales"].Partitions.Add(extra);

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.DynamicallyPartitioned);
    }

    [Fact]
    public void IncrementalRefreshPolicy_wins_over_dynamic()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        // Add policy on source AND extra partition on target
        src.Model.Tables["FactSales"].RefreshPolicy = new BasicRefreshPolicy
        {
            RollingWindowGranularity = RefreshGranularityType.Year,
            RollingWindowPeriods = 5,
            IncrementalGranularity = RefreshGranularityType.Day,
            IncrementalPeriods = 10,
            SourceExpression = "let s = #table({\"Date\",\"Amount\"}, {}) in s"
        };
        tgt.Model.Tables["FactSales"].Partitions.Add(new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        });

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.IncrementalRefreshPolicy);
    }
}
```

Expected: FAIL.

- [ ] **Step 3: Implement classifier**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing;

public sealed class TableClassifier
{
    public TableClassification Classify(Table? source, Table? target)
    {
        var sourceHasPolicy = source?.RefreshPolicy is not null;
        var targetHasPolicy = target?.RefreshPolicy is not null;
        if (sourceHasPolicy || targetHasPolicy)
            return TableClassification.IncrementalRefreshPolicy;

        if (source is null || target is null)
            return TableClassification.Static;

        var sourceNames = source.Partitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (target.Partitions.Any(p => !sourceNames.Contains(p.Name)))
            return TableClassification.DynamicallyPartitioned;

        return TableClassification.Static;
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~TableClassifierTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Diffing/TableClassification.cs src/Weft.Core/Diffing/TableClassifier.cs test/Weft.Core.Tests/Diffing/TableClassifierTests.cs
git commit -m "feat(core): TableClassifier (Static | DynamicallyPartitioned | IncrementalRefreshPolicy)"
```

---

## Task 12: `RefreshPolicyComparer` (field-by-field equality)

**Files:**
- Create: `src/Weft.Core/RefreshPolicy/RefreshPolicyComparer.cs`
- Create: `test/Weft.Core.Tests/RefreshPolicy/RefreshPolicyComparerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RefreshPolicyComparerTests
{
    private static BasicRefreshPolicy MakePolicy() => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = 5,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        IncrementalPeriodsOffset = 0,
        SourceExpression = "let Source = ... in Source",
        PollingExpression = null
    };

    [Fact]
    public void Equal_when_all_fields_match() =>
        new RefreshPolicyComparer().AreEqual(MakePolicy(), MakePolicy()).Should().BeTrue();

    [Fact]
    public void Not_equal_when_RollingWindowPeriods_differ()
    {
        var a = MakePolicy(); var b = MakePolicy(); b.RollingWindowPeriods = 3;
        new RefreshPolicyComparer().AreEqual(a, b).Should().BeFalse();
    }

    [Fact]
    public void Not_equal_when_one_side_null()
    {
        new RefreshPolicyComparer().AreEqual(MakePolicy(), null).Should().BeFalse();
        new RefreshPolicyComparer().AreEqual(null, MakePolicy()).Should().BeFalse();
    }

    [Fact]
    public void Equal_when_both_null() =>
        new RefreshPolicyComparer().AreEqual(null, null).Should().BeTrue();
}
```

Expected: FAIL.

- [ ] **Step 2: Implement**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.RefreshPolicy;

public sealed class RefreshPolicyComparer
{
    public bool AreEqual(Microsoft.AnalysisServices.Tabular.RefreshPolicy? a,
                         Microsoft.AnalysisServices.Tabular.RefreshPolicy? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a is BasicRefreshPolicy ba && b is BasicRefreshPolicy bb)
        {
            return ba.RollingWindowGranularity == bb.RollingWindowGranularity
                && ba.RollingWindowPeriods    == bb.RollingWindowPeriods
                && ba.IncrementalGranularity  == bb.IncrementalGranularity
                && ba.IncrementalPeriods      == bb.IncrementalPeriods
                && ba.IncrementalPeriodsOffset == bb.IncrementalPeriodsOffset
                && string.Equals(ba.SourceExpression,  bb.SourceExpression,  StringComparison.Ordinal)
                && string.Equals(ba.PollingExpression, bb.PollingExpression, StringComparison.Ordinal)
                && ba.Mode == bb.Mode;
        }
        return a.GetType() == b.GetType();
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RefreshPolicyComparerTests
```
Expected: PASS.

```bash
git add src/Weft.Core/RefreshPolicy/RefreshPolicyComparer.cs test/Weft.Core.Tests/RefreshPolicy/
git commit -m "feat(core): RefreshPolicyComparer field-by-field equality"
```

---

## Task 13: `ChangeSet` / `TableDiff` / `TablePlan` types

**Files:**
- Create: `src/Weft.Core/Diffing/TablePlan.cs`
- Create: `src/Weft.Core/Diffing/TableDiff.cs`
- Create: `src/Weft.Core/Diffing/ChangeSet.cs`

- [ ] **Step 1: Define data types**

```csharp
// TablePlan.cs
using Microsoft.AnalysisServices.Tabular;
namespace Weft.Core.Diffing;
public sealed record TablePlan(
    string Name,
    TableClassification Classification,
    Table SourceTable);
```

```csharp
// TableDiff.cs
using Microsoft.AnalysisServices.Tabular;
namespace Weft.Core.Diffing;

public enum PartitionStrategy { PreserveTarget, UseSource }

public sealed record TableDiff(
    string Name,
    TableClassification Classification,
    bool RefreshPolicyChanged,
    IReadOnlyList<string> ColumnsAdded,
    IReadOnlyList<string> ColumnsRemoved,
    IReadOnlyList<string> ColumnsModified,
    IReadOnlyList<string> MeasuresAdded,
    IReadOnlyList<string> MeasuresRemoved,
    IReadOnlyList<string> MeasuresModified,
    IReadOnlyList<string> HierarchiesChanged,
    PartitionStrategy PartitionStrategy,
    Table SourceTable,
    Table TargetTable);
```

```csharp
// ChangeSet.cs
namespace Weft.Core.Diffing;

public sealed record ChangeSet(
    IReadOnlyList<TablePlan> TablesToAdd,
    IReadOnlyList<string> TablesToDrop,
    IReadOnlyList<TableDiff> TablesToAlter,
    IReadOnlyList<string> TablesUnchanged,
    IReadOnlyList<string> MeasuresChanged,
    IReadOnlyList<string> RelationshipsChanged,
    IReadOnlyList<string> RolesChanged,
    IReadOnlyList<string> PerspectivesChanged,
    IReadOnlyList<string> CulturesChanged,
    IReadOnlyList<string> ExpressionsChanged,
    IReadOnlyList<string> DataSourcesChanged)
{
    public bool IsEmpty =>
        TablesToAdd.Count == 0 && TablesToDrop.Count == 0 && TablesToAlter.Count == 0
        && MeasuresChanged.Count == 0 && RelationshipsChanged.Count == 0
        && RolesChanged.Count == 0 && PerspectivesChanged.Count == 0
        && CulturesChanged.Count == 0 && ExpressionsChanged.Count == 0
        && DataSourcesChanged.Count == 0;

    public IEnumerable<string> RefreshTargets =>
        TablesToAdd.Select(t => t.Name).Concat(TablesToAlter.Select(t => t.Name));
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build
git add src/Weft.Core/Diffing/TablePlan.cs src/Weft.Core/Diffing/TableDiff.cs src/Weft.Core/Diffing/ChangeSet.cs
git commit -m "feat(core): ChangeSet, TableDiff, TablePlan data types"
```

---

## Task 14: `ColumnComparer` and `MeasureComparer` (per-object diff helpers)

**Files:**
- Create: `src/Weft.Core/Diffing/Comparers/ColumnComparer.cs`
- Create: `src/Weft.Core/Diffing/Comparers/MeasureComparer.cs`
- Create: `test/Weft.Core.Tests/Diffing/Comparers/ColumnComparerTests.cs`
- Create: `test/Weft.Core.Tests/Diffing/Comparers/MeasureComparerTests.cs`

- [ ] **Step 1: Failing tests for ColumnComparer**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Xunit;

namespace Weft.Core.Tests.Diffing.Comparers;

public class ColumnComparerTests
{
    private static DataColumn Col(string name, DataType dt = DataType.String, string src = "x") =>
        new() { Name = name, DataType = dt, SourceColumn = src };

    [Fact]
    public void Detects_added_removed_modified()
    {
        var source = new ColumnCollection(null!) { Col("A"), Col("B", DataType.Int64), Col("C") };
        var target = new ColumnCollection(null!) { Col("A"), Col("B", DataType.String), Col("D") };

        var d = new ColumnComparer().Compare(source, target);

        d.Added.Should().Equal("C");
        d.Removed.Should().Equal("D");
        d.Modified.Should().Equal("B");
    }
}
```

NOTE: `ColumnCollection` constructor is internal; use `Table.Columns` from a fresh `Table()` instance for instantiation in tests.

Adjust the test to:
```csharp
var sourceTable = new Table();
sourceTable.Columns.Add(Col("A"));
sourceTable.Columns.Add(Col("B", DataType.Int64));
sourceTable.Columns.Add(Col("C"));

var targetTable = new Table();
targetTable.Columns.Add(Col("A"));
targetTable.Columns.Add(Col("B", DataType.String));
targetTable.Columns.Add(Col("D"));

var d = new ColumnComparer().Compare(sourceTable.Columns, targetTable.Columns);
```

- [ ] **Step 2: Implement ColumnComparer**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing.Comparers;

public sealed record ColumnDiffResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Modified);

public sealed class ColumnComparer
{
    public ColumnDiffResult Compare(ColumnCollection source, ColumnCollection target)
    {
        var srcByName = source.OfType<Column>().ToDictionary(c => c.Name, StringComparer.Ordinal);
        var tgtByName = target.OfType<Column>().ToDictionary(c => c.Name, StringComparer.Ordinal);

        var added    = srcByName.Keys.Except(tgtByName.Keys).OrderBy(x => x).ToList();
        var removed  = tgtByName.Keys.Except(srcByName.Keys).OrderBy(x => x).ToList();
        var modified = srcByName.Keys
            .Intersect(tgtByName.Keys)
            .Where(name => !ColumnsEqual(srcByName[name], tgtByName[name]))
            .OrderBy(x => x)
            .ToList();

        return new ColumnDiffResult(added, removed, modified);
    }

    private static bool ColumnsEqual(Column a, Column b) =>
        a.DataType == b.DataType
        && string.Equals(a is DataColumn da ? da.SourceColumn : null,
                          b is DataColumn db ? db.SourceColumn : null, StringComparison.Ordinal)
        && string.Equals((a as CalculatedColumn)?.Expression,
                          (b as CalculatedColumn)?.Expression, StringComparison.Ordinal)
        && a.IsHidden == b.IsHidden
        && a.IsKey == b.IsKey;
}
```

- [ ] **Step 3: Failing test for MeasureComparer**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Xunit;

namespace Weft.Core.Tests.Diffing.Comparers;

public class MeasureComparerTests
{
    [Fact]
    public void Detects_added_removed_modified_measures()
    {
        var src = new Table();
        src.Measures.Add(new Measure { Name = "A", Expression = "1" });
        src.Measures.Add(new Measure { Name = "B", Expression = "2" });

        var tgt = new Table();
        tgt.Measures.Add(new Measure { Name = "A", Expression = "1" });
        tgt.Measures.Add(new Measure { Name = "B", Expression = "999" });
        tgt.Measures.Add(new Measure { Name = "Z", Expression = "0" });

        var d = new MeasureComparer().Compare(src.Measures, tgt.Measures);

        d.Added.Should().BeEmpty();
        d.Removed.Should().Equal("Z");
        d.Modified.Should().Equal("B");
    }
}
```

- [ ] **Step 4: Implement MeasureComparer**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing.Comparers;

public sealed record MeasureDiffResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Modified);

public sealed class MeasureComparer
{
    public MeasureDiffResult Compare(MeasureCollection source, MeasureCollection target)
    {
        var srcByName = source.OfType<Measure>().ToDictionary(m => m.Name, StringComparer.Ordinal);
        var tgtByName = target.OfType<Measure>().ToDictionary(m => m.Name, StringComparer.Ordinal);

        var added    = srcByName.Keys.Except(tgtByName.Keys).OrderBy(x => x).ToList();
        var removed  = tgtByName.Keys.Except(srcByName.Keys).OrderBy(x => x).ToList();
        var modified = srcByName.Keys
            .Intersect(tgtByName.Keys)
            .Where(n => !MeasuresEqual(srcByName[n], tgtByName[n]))
            .OrderBy(x => x)
            .ToList();

        return new MeasureDiffResult(added, removed, modified);
    }

    private static bool MeasuresEqual(Measure a, Measure b) =>
        string.Equals(a.Expression, b.Expression, StringComparison.Ordinal)
        && a.IsHidden == b.IsHidden
        && string.Equals(a.FormatString, b.FormatString, StringComparison.Ordinal)
        && string.Equals(a.DisplayFolder, b.DisplayFolder, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Run + commit**

```bash
dotnet test --filter "FullyQualifiedName~ColumnComparerTests|FullyQualifiedName~MeasureComparerTests"
```
Expected: PASS.

```bash
git add src/Weft.Core/Diffing/Comparers/ test/Weft.Core.Tests/Diffing/Comparers/
git commit -m "feat(core): ColumnComparer + MeasureComparer per-object diffs"
```

---

## Task 15: `ModelDiffer` — table add/drop/unchanged classification

**Files:**
- Create: `src/Weft.Core/Diffing/ModelDiffer.cs`
- Create: `test/Weft.Core.Tests/Diffing/ModelDifferTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Diffing;

public class ModelDifferTests
{
    [Fact]
    public void Empty_changeset_when_source_equals_target()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.IsEmpty.Should().BeTrue();
        cs.TablesUnchanged.Should().Contain(new[] { "DimDate", "FactSales" });
    }

    [Fact]
    public void Detects_added_table_in_source()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.TablesToAdd.Select(t => t.Name).Should().Equal("NewTable");
        cs.TablesToDrop.Should().BeEmpty();
    }

    [Fact]
    public void Detects_dropped_table_when_only_in_target()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        tgt.Model.Tables.Add(new Table { Name = "OldTable" });

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.TablesToDrop.Should().Equal("OldTable");
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement skeleton**

```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Weft.Core.RefreshPolicy;

namespace Weft.Core.Diffing;

public sealed class ModelDiffer
{
    private readonly TableClassifier _classifier = new();
    private readonly ColumnComparer _columns = new();
    private readonly MeasureComparer _measures = new();
    private readonly RefreshPolicyComparer _policies = new();

    public ChangeSet Compute(Database source, Database target)
    {
        var srcTables = source.Model.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var tgtTables = target.Model.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);

        var add = srcTables.Keys.Except(tgtTables.Keys).Select(n => MakeAdd(srcTables[n])).ToList();
        var drop = tgtTables.Keys.Except(srcTables.Keys).OrderBy(x => x).ToList();

        var alter = new List<TableDiff>();
        var unchanged = new List<string>();
        foreach (var name in srcTables.Keys.Intersect(tgtTables.Keys))
        {
            var diff = DiffTable(srcTables[name], tgtTables[name]);
            if (diff is null) unchanged.Add(name);
            else alter.Add(diff);
        }

        return new ChangeSet(
            TablesToAdd: add,
            TablesToDrop: drop,
            TablesToAlter: alter,
            TablesUnchanged: unchanged,
            MeasuresChanged: Array.Empty<string>(),
            RelationshipsChanged: Array.Empty<string>(),
            RolesChanged: Array.Empty<string>(),
            PerspectivesChanged: Array.Empty<string>(),
            CulturesChanged: Array.Empty<string>(),
            ExpressionsChanged: Array.Empty<string>(),
            DataSourcesChanged: Array.Empty<string>());
    }

    private TablePlan MakeAdd(Table sourceTable) =>
        new(sourceTable.Name, _classifier.Classify(sourceTable, null), sourceTable);

    private TableDiff? DiffTable(Table src, Table tgt)
    {
        var classification = _classifier.Classify(src, tgt);
        var policyChanged = !_policies.AreEqual(src.RefreshPolicy, tgt.RefreshPolicy);
        var cols = _columns.Compare(src.Columns, tgt.Columns);
        var meas = _measures.Compare(src.Measures, tgt.Measures);

        var hasChange =
            policyChanged
            || cols.Added.Count + cols.Removed.Count + cols.Modified.Count > 0
            || meas.Added.Count + meas.Removed.Count + meas.Modified.Count > 0;

        if (!hasChange) return null;

        return new TableDiff(
            Name: src.Name,
            Classification: classification,
            RefreshPolicyChanged: policyChanged,
            ColumnsAdded: cols.Added, ColumnsRemoved: cols.Removed, ColumnsModified: cols.Modified,
            MeasuresAdded: meas.Added, MeasuresRemoved: meas.Removed, MeasuresModified: meas.Modified,
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: src,
            TargetTable: tgt);
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~ModelDifferTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Diffing/ModelDiffer.cs test/Weft.Core.Tests/Diffing/ModelDifferTests.cs
git commit -m "feat(core): ModelDiffer table add/drop/unchanged + skeleton alter detection"
```

---

## Task 16: `ModelDiffer` — column-change alters with partition preservation

**Files:**
- Modify: `test/Weft.Core.Tests/Diffing/ModelDifferTests.cs` (extend)

- [ ] **Step 1: Add tests for alter behavior**

Append to `ModelDifferTests`:
```csharp
[Fact]
public void Column_added_to_existing_table_is_an_alter_with_preserved_partitions()
{
    var src = FixtureLoader.LoadBim("models/tiny-static.bim");
    var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

    src.Model.Tables["FactSales"].Columns.Add(
        new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });

    var cs = new ModelDiffer().Compute(src, tgt);

    cs.TablesToAlter.Should().ContainSingle()
        .Which.Should().Match<TableDiff>(d =>
            d.Name == "FactSales" &&
            d.ColumnsAdded.SequenceEqual(new[] { "Region" }) &&
            d.PartitionStrategy == PartitionStrategy.PreserveTarget);
}

[Fact]
public void Refresh_policy_change_marks_table_alter_with_policy_flag()
{
    var src = FixtureLoader.LoadBim("models/tiny-static.bim");
    var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

    src.Model.Tables["FactSales"].RefreshPolicy = new BasicRefreshPolicy
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = 5,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let Source = #table({\"Date\",\"Amount\"}, {}) in Source"
    };

    var cs = new ModelDiffer().Compute(src, tgt);

    var diff = cs.TablesToAlter.Single(d => d.Name == "FactSales");
    diff.RefreshPolicyChanged.Should().BeTrue();
    diff.Classification.Should().Be(TableClassification.IncrementalRefreshPolicy);
}
```

Run:
```bash
dotnet test --filter FullyQualifiedName~ModelDifferTests
```
Expected: PASS (the existing implementation already handles these).

- [ ] **Step 2: Commit (no impl change — coverage only)**

```bash
git add test/Weft.Core.Tests/Diffing/ModelDifferTests.cs
git commit -m "test(core): cover column-add alter and refresh-policy-change alter cases"
```

---

## Task 17: `RetentionCalculator` — partitions removed under shrunk policy

**Files:**
- Create: `src/Weft.Core/RefreshPolicy/RetentionCalculator.cs`
- Create: `test/Weft.Core.Tests/RefreshPolicy/RetentionCalculatorTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RetentionCalculatorTests
{
    private static BasicRefreshPolicy Policy(int years) => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = years,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void No_loss_when_window_unchanged()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(5), newPolicy: Policy(5),
            existingPartitionNames: new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        lost.Should().BeEmpty();
    }

    [Fact]
    public void Lists_partitions_outside_new_window()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(5), newPolicy: Policy(3),
            existingPartitionNames: new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        // Window of 3 years from 2026 retains 2024,2025,2026; loses 2021,2022,2023
        lost.Should().BeEquivalentTo(new[] { "Year2021", "Year2022", "Year2023" });
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement (Year-granularity for v1; Quarter/Month deferred to follow-on task)**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.RefreshPolicy;

public sealed class RetentionCalculator
{
    private readonly DateOnly _today;
    public RetentionCalculator(DateOnly today) => _today = today;
    public RetentionCalculator() : this(DateOnly.FromDateTime(DateTime.UtcNow)) {}

    public IReadOnlyList<string> PartitionsRemovedBy(
        BasicRefreshPolicy oldPolicy,
        BasicRefreshPolicy newPolicy,
        IEnumerable<string> existingPartitionNames)
    {
        if (newPolicy.RollingWindowGranularity != RefreshGranularityType.Year)
            throw new NotSupportedException("Only Year granularity supported in v1; extend for Quarter/Month in a follow-up task.");

        var keepYears = Enumerable.Range(0, newPolicy.RollingWindowPeriods)
            .Select(i => _today.Year - i)
            .ToHashSet();

        return existingPartitionNames
            .Where(IsYearPartition)
            .Where(name => !keepYears.Contains(YearOf(name)))
            .OrderBy(x => x)
            .ToList();
    }

    private static bool IsYearPartition(string name) =>
        name.StartsWith("Year", StringComparison.Ordinal) &&
        name.Length == 8 && int.TryParse(name.AsSpan(4), out _);

    private static int YearOf(string name) => int.Parse(name.AsSpan(4));
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RetentionCalculatorTests
```
Expected: PASS.

```bash
git add src/Weft.Core/RefreshPolicy/RetentionCalculator.cs test/Weft.Core.Tests/RefreshPolicy/RetentionCalculatorTests.cs
git commit -m "feat(core): RetentionCalculator computes partitions lost when rolling window shrinks (Year)"
```

> Follow-up TODO captured here, NOT in the code: extend RetentionCalculator to support Quarter/Month granularity. Tracked as Task 17b in a future plan revision; not blocking Plan 1.

---

## Task 18: `RestorePartitionSet` — derive partition names for a date range under a policy

**Files:**
- Create: `src/Weft.Core/Restore/RestorePartitionSet.cs`
- Create: `test/Weft.Core.Tests/Restore/RestorePartitionSetTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Restore;
using Xunit;

namespace Weft.Core.Tests.Restore;

public class RestorePartitionSetTests
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
    public void Year_granularity_returns_year_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Year, 5),
            from: new DateOnly(2021, 1, 1),
            to:   new DateOnly(2023, 12, 31));

        set.Should().Equal(new[] { "Year2021", "Year2022", "Year2023" });
    }

    [Fact]
    public void Quarter_granularity_returns_quarter_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Quarter, 8),
            from: new DateOnly(2024, 1, 1),
            to:   new DateOnly(2024, 9, 30));

        set.Should().Equal(new[] { "Quarter2024Q1", "Quarter2024Q2", "Quarter2024Q3" });
    }

    [Fact]
    public void Month_granularity_returns_month_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Month, 24),
            from: new DateOnly(2025, 11, 1),
            to:   new DateOnly(2026, 1, 31));

        set.Should().Equal(new[] { "Month2025-11", "Month2025-12", "Month2026-01" });
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement**

```csharp
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Restore;

public sealed class RestorePartitionSet
{
    public IReadOnlyList<string> Compute(BasicRefreshPolicy policy, DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException("from must be <= to");

        return policy.RollingWindowGranularity switch
        {
            RefreshGranularityType.Year    => Years(from, to),
            RefreshGranularityType.Quarter => Quarters(from, to),
            RefreshGranularityType.Month   => Months(from, to),
            _ => throw new NotSupportedException(
                $"Restore partition set unsupported for granularity {policy.RollingWindowGranularity}")
        };
    }

    private static IReadOnlyList<string> Years(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        for (var y = from.Year; y <= to.Year; y++) list.Add($"Year{y}");
        return list;
    }

    private static IReadOnlyList<string> Quarters(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var start = new DateOnly(from.Year, ((from.Month - 1) / 3) * 3 + 1, 1);
        var cursor = start;
        while (cursor <= to)
        {
            var q = (cursor.Month - 1) / 3 + 1;
            list.Add($"Quarter{cursor.Year}Q{q}");
            cursor = cursor.AddMonths(3);
        }
        return list;
    }

    private static IReadOnlyList<string> Months(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        while (cursor <= to)
        {
            list.Add($"Month{cursor.Year:D4}-{cursor.Month:D2}");
            cursor = cursor.AddMonths(1);
        }
        return list;
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~RestorePartitionSetTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Restore/ test/Weft.Core.Tests/Restore/
git commit -m "feat(core): RestorePartitionSet derives partition names for a date range and granularity"
```

---

## Task 19: `TmslSequence` and `TmslBuilder` skeleton (create + drop)

**Files:**
- Create: `src/Weft.Core/Tmsl/TmslSequence.cs`
- Create: `src/Weft.Core/Tmsl/TmslBuilder.cs`
- Create: `test/Weft.Core.Tests/Tmsl/TmslBuilderTests.cs`

- [ ] **Step 1: Define `TmslSequence`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weft.Core.Tmsl;

public sealed class TmslSequence
{
    private readonly JsonArray _operations = new();

    public void Add(JsonNode op) => _operations.Add(op);

    public string ToJson()
    {
        var root = new JsonObject
        {
            ["sequence"] = new JsonObject
            {
                ["maxParallelism"] = 1,
                ["operations"] = _operations
            }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
```

- [ ] **Step 2: Failing test for TmslBuilder**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

public class TmslBuilderTests
{
    [Fact]
    public void Empty_changeset_produces_an_empty_sequence()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"sequence\"");
        json.Should().Contain("\"operations\": []");
    }

    [Fact]
    public void Adding_a_table_emits_create_command()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"create\"").And.Contain("NewTable");
    }

    [Fact]
    public void Dropping_a_table_emits_delete_command()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        tgt.Model.Tables.Add(new Table { Name = "OldTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"delete\"").And.Contain("OldTable");
    }
}
```

Expected: FAIL.

- [ ] **Step 3: Implement TmslBuilder (create + drop only; alter in next task)**

```csharp
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using TomJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;

namespace Weft.Core.Tmsl;

public sealed class TmslBuilder
{
    public string Build(ChangeSet changeSet, Database source, Database target)
    {
        var seq = new TmslSequence();
        var dbName = target.Name;

        foreach (var name in changeSet.TablesToDrop)
            seq.Add(DeleteTable(dbName, name));

        foreach (var add in changeSet.TablesToAdd)
            seq.Add(CreateTable(dbName, add.SourceTable));

        // Alters in Task 20.

        var json = seq.ToJson();
        new PartitionIntegrityValidator().Validate(json, target, changeSet);
        return json;
    }

    private static JsonNode DeleteTable(string database, string table) =>
        new JsonObject
        {
            ["delete"] = new JsonObject
            {
                ["object"] = new JsonObject
                {
                    ["database"] = database,
                    ["table"] = table
                }
            }
        };

    private static JsonNode CreateTable(string database, Table table)
    {
        var tableJson = SerializeTableObject(table);
        return new JsonObject
        {
            ["create"] = new JsonObject
            {
                ["parentObject"] = new JsonObject
                {
                    ["database"] = database,
                    ["model"] = new JsonObject()
                },
                ["table"] = tableJson
            }
        };
    }

    internal static JsonNode SerializeTableObject(Table table)
    {
        // Round-trip the table through TOM serialization to capture every property.
        var clone = new Database { Name = "_t", CompatibilityLevel = 1600 };
        clone.Model = new Model();
        clone.Model.Tables.Add(table.Clone());
        var dbJson = TomJsonSerializer.SerializeDatabase(clone, new SerializeOptions
        {
            IgnoreTimestamps = true,
            IgnoreInferredObjects = true,
            IgnoreInferredProperties = true
        });
        var node = JsonNode.Parse(dbJson)!;
        return node["model"]!["tables"]!.AsArray().First()!.DeepClone();
    }
}
```

- [ ] **Step 4: Stub `PartitionIntegrityValidator` (real impl in Task 21)**

```csharp
// src/Weft.Core/Tmsl/PartitionIntegrityValidator.cs
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;

namespace Weft.Core.Tmsl;

public sealed class PartitionIntegrityValidator
{
    public void Validate(string tmslJson, Database target, ChangeSet changeSet) { /* Task 21 */ }
}
```

```csharp
// src/Weft.Core/Tmsl/PartitionIntegrityException.cs
namespace Weft.Core.Tmsl;
public sealed class PartitionIntegrityException : Exception
{
    public PartitionIntegrityException(string message) : base(message) {}
}
```

- [ ] **Step 5: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~TmslBuilderTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Tmsl/ test/Weft.Core.Tests/Tmsl/
git commit -m "feat(core): TmslBuilder skeleton emits create/delete in a single sequence"
```

---

## Task 20: `TmslBuilder` — alter with partition + bookmark preservation

**Files:**
- Modify: `src/Weft.Core/Tmsl/TmslBuilder.cs`
- Modify: `test/Weft.Core.Tests/Tmsl/TmslBuilderTests.cs`

- [ ] **Step 1: Failing test**

Append to `TmslBuilderTests`:
```csharp
[Fact]
public void Altering_a_table_attaches_target_partitions_with_bookmarks()
{
    var src = FixtureLoader.LoadBim("models/tiny-static.bim");
    var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

    // Source: add a column. Target: stamp a bookmark on the existing partition.
    src.Model.Tables["FactSales"].Columns.Add(
        new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });
    tgt.Model.Tables["FactSales"].Partitions["FactSales"].RefreshBookmark = "wm-001";

    var cs = new ModelDiffer().Compute(src, tgt);
    var json = new TmslBuilder().Build(cs, src, tgt);

    json.Should().Contain("\"createOrReplace\"");
    json.Should().Contain("\"Region\"");
    json.Should().Contain("\"refreshBookmark\": \"wm-001\"");
}
```

Expected: FAIL.

- [ ] **Step 2: Implement alter emission**

Replace the `Build` method body in `TmslBuilder.cs` with:
```csharp
public string Build(ChangeSet changeSet, Database source, Database target)
{
    var seq = new TmslSequence();
    var dbName = target.Name;

    foreach (var name in changeSet.TablesToDrop)
        seq.Add(DeleteTable(dbName, name));

    foreach (var add in changeSet.TablesToAdd)
        seq.Add(CreateTable(dbName, add.SourceTable));

    foreach (var alter in changeSet.TablesToAlter)
        seq.Add(AlterTable(dbName, alter, source, target));

    var json = seq.ToJson();
    new PartitionIntegrityValidator().Validate(json, target, changeSet);
    return json;
}

private static JsonNode AlterTable(string database, TableDiff diff, Database source, Database target)
{
    var srcTable = source.Model.Tables[diff.Name];
    var tgtTable = target.Model.Tables[diff.Name];

    // Build a new Table that has source schema but TARGET partitions (deep-cloned).
    var merged = srcTable.Clone();
    merged.Partitions.Clear();
    foreach (var p in tgtTable.Partitions)
        merged.Partitions.Add(p.Clone());   // RefreshBookmark + all properties carry over

    var tableJson = SerializeTableObject(merged);

    return new JsonObject
    {
        ["createOrReplace"] = new JsonObject
        {
            ["object"] = new JsonObject
            {
                ["database"] = database,
                ["table"]    = diff.Name
            },
            ["table"] = tableJson
        }
    };
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~TmslBuilderTests
```
Expected: PASS (all four tests in the class now).

```bash
git add src/Weft.Core/Tmsl/TmslBuilder.cs test/Weft.Core.Tests/Tmsl/TmslBuilderTests.cs
git commit -m "feat(core): TmslBuilder emits alter with target partitions + bookmarks preserved"
```

---

## Task 21: `PartitionIntegrityValidator` — enforce the §5.4 invariant

**Files:**
- Modify: `src/Weft.Core/Tmsl/PartitionIntegrityValidator.cs`
- Create: `test/Weft.Core.Tests/Tmsl/PartitionIntegrityValidatorTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

public class PartitionIntegrityValidatorTests
{
    [Fact]
    public void Passes_when_alter_includes_all_target_partitions()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        var act = () => new PartitionIntegrityValidator().Validate(json, tgt, cs);
        act.Should().NotThrow();
    }

    [Fact]
    public void Throws_when_alter_drops_a_target_partition()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        // Target has a 2nd partition. A buggy alter that omits it should be rejected.
        tgt.Model.Tables["FactSales"].Partitions.Add(new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        });

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
                      { "name": "FactSales", "mode": "import", "source": { "type": "m", "expression": "x" } }
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
        act.Should().Throw<PartitionIntegrityException>()
           .WithMessage("*FactSales_2024*");
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement validator**

```csharp
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;

namespace Weft.Core.Tmsl;

public sealed class PartitionIntegrityValidator
{
    public void Validate(string tmslJson, Database target, ChangeSet changeSet)
    {
        var root = JsonNode.Parse(tmslJson)!.AsObject();
        var operations = root["sequence"]?["operations"]?.AsArray();
        if (operations is null) return;

        var droppedTables = new HashSet<string>(changeSet.TablesToDrop, StringComparer.Ordinal);

        // For each createOrReplace on a non-dropped table, check that every existing
        // partition is still present in the new table block.
        foreach (var op in operations)
        {
            if (op?["createOrReplace"] is not JsonObject cor) continue;
            var tableName = cor["object"]?["table"]?.GetValue<string>();
            if (tableName is null) continue;
            if (droppedTables.Contains(tableName)) continue;

            if (!target.Model.Tables.ContainsName(tableName)) continue;
            var existingPartitions = target.Model.Tables[tableName]
                .Partitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            var emittedPartitions = (cor["table"]?["partitions"] as JsonArray)?
                .Select(p => p?["name"]?.GetValue<string>())
                .Where(n => n is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();

            var missing = existingPartitions.Except(emittedPartitions).OrderBy(x => x).ToList();
            if (missing.Count > 0)
            {
                throw new PartitionIntegrityException(
                    $"Partition integrity violation on table '{tableName}': " +
                    $"the generated TMSL would remove existing partition(s) {string.Join(", ", missing)}. " +
                    $"This is forbidden for preserved tables (see spec §5.4).");
            }

            // Bookmark preservation: every emitted partition that exists on target
            // must carry the same refreshBookmark as the target snapshot.
            foreach (var emitted in (cor["table"]?["partitions"] as JsonArray) ?? new JsonArray())
            {
                var name = emitted?["name"]?.GetValue<string>();
                if (name is null) continue;
                if (!target.Model.Tables[tableName].Partitions.ContainsName(name)) continue;
                var targetBookmark = target.Model.Tables[tableName].Partitions[name].RefreshBookmark;
                var emittedBookmark = emitted!["refreshBookmark"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(targetBookmark)
                    && !string.Equals(targetBookmark, emittedBookmark, StringComparison.Ordinal))
                {
                    throw new PartitionIntegrityException(
                        $"Bookmark integrity violation on '{tableName}'/'{name}': " +
                        $"target bookmark '{targetBookmark}' was not preserved in generated TMSL " +
                        $"(emitted: '{emittedBookmark ?? "<null>"}').");
                }
            }
        }
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~PartitionIntegrityValidatorTests
```
Expected: PASS.

```bash
git add src/Weft.Core/Tmsl/PartitionIntegrityValidator.cs test/Weft.Core.Tests/Tmsl/PartitionIntegrityValidatorTests.cs
git commit -m "feat(core): PartitionIntegrityValidator enforces partition + bookmark preservation invariant"
```

---

## Task 22: Snapshot tests for canonical TMSL outputs (Verify.Xunit)

**Files:**
- Create: `test/Weft.Core.Tests/Tmsl/TmslSnapshotTests.cs`
- Create: `test/Weft.Core.Tests/Snapshots/` (Verify will write `.verified.txt` here)

- [ ] **Step 1: Configure Verify**

Create `test/Weft.Core.Tests/ModuleInitializer.cs`:
```csharp
using System.Runtime.CompilerServices;
using VerifyTests;

namespace Weft.Core.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.UseStrictJson();
        Verifier.DerivePathInfo((sourceFile, projectDir, type, method) =>
        {
            var snapshotDir = Path.Combine(projectDir, "Snapshots");
            Directory.CreateDirectory(snapshotDir);
            return new PathInfo(snapshotDir, type.Name, method);
        });
    }
}
```

- [ ] **Step 2: Snapshot tests**

```csharp
using Microsoft.AnalysisServices.Tabular;
using VerifyXunit;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

[UsesVerify]
public class TmslSnapshotTests
{
    [Fact]
    public Task Snapshot_add_table()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "AddedTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);
        return Verifier.Verify(json).UseExtension("json");
    }

    [Fact]
    public Task Snapshot_alter_with_added_column_preserves_bookmark()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });
        tgt.Model.Tables["FactSales"].Partitions["FactSales"].RefreshBookmark = "wm-001";

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);
        return Verifier.Verify(json).UseExtension("json");
    }
}
```

- [ ] **Step 3: First run produces `.received.json` files**

```bash
dotnet test --filter FullyQualifiedName~TmslSnapshotTests
```
Expected: FAIL on first run because no `.verified.json` exists yet.

- [ ] **Step 4: Inspect and accept the snapshots**

Open the generated `*.received.json` files under `test/Weft.Core.Tests/Snapshots/`. They should look correct (single sequence, expected operations, bookmarks preserved). When confirmed, rename them:

```bash
cd test/Weft.Core.Tests/Snapshots
for f in *.received.json; do mv "$f" "${f%.received.json}.verified.json"; done
cd -
```

Re-run:
```bash
dotnet test --filter FullyQualifiedName~TmslSnapshotTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add test/Weft.Core.Tests/Tmsl/TmslSnapshotTests.cs test/Weft.Core.Tests/Snapshots/ test/Weft.Core.Tests/ModuleInitializer.cs
git commit -m "test(core): Verify snapshot tests for canonical TMSL outputs"
```

---

## Task 23: Public façade `WeftCore` for in-memory orchestration

**Files:**
- Create: `src/Weft.Core/WeftCore.cs`
- Create: `test/Weft.Core.Tests/WeftCoreTests.cs`

This is the public entry point Plan 2's CLI will call. It wires loading + diffing + TMSL building.

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests;

public class WeftCoreTests
{
    [Fact]
    public void Plan_returns_changeset_and_tmsl_for_in_memory_databases()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var result = WeftCore.Plan(src, tgt);

        result.ChangeSet.TablesToAdd.Should().ContainSingle().Which.Name.Should().Be("NewTable");
        result.TmslJson.Should().Contain("\"create\"").And.Contain("NewTable");
        result.TmslJson.Should().Contain("\"sequence\"");
    }
}
```

Expected: FAIL.

- [ ] **Step 2: Implement**

```csharp
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;

namespace Weft.Core;

public sealed record PlanResult(ChangeSet ChangeSet, string TmslJson);

public static class WeftCore
{
    public static PlanResult Plan(Database source, Database target)
    {
        var changeSet = new ModelDiffer().Compute(source, target);
        var tmsl      = new TmslBuilder().Build(changeSet, source, target);
        return new PlanResult(changeSet, tmsl);
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
dotnet test --filter FullyQualifiedName~WeftCoreTests
```
Expected: PASS.

```bash
git add src/Weft.Core/WeftCore.cs test/Weft.Core.Tests/WeftCoreTests.cs
git commit -m "feat(core): public WeftCore.Plan(source, target) façade"
```

---

## Task 24: Run the whole suite + verify zero warnings

- [ ] **Step 1: Full clean test pass**

```bash
cd /Users/marcosmagri/Documents/MUFG/PowerBIAutomationDeploy
dotnet clean
dotnet build  /warnaserror
dotnet test
```

Expected:
- 0 build warnings
- All tests PASS
- Test count >= 25 across the projects

- [ ] **Step 2: Tag the milestone**

```bash
git tag -a plan-1-core-mvp-complete -m "Weft Plan 1: Core MVP complete"
git log --oneline | head -30
```

- [ ] **Step 3: Final commit (if anything pending)**

If `git status` shows uncommitted file (e.g., a stray `.csproj` change), commit it with a message describing what slipped in. Otherwise this step is a no-op.

---

## Spec coverage check (run after Task 24)

Walk the spec sections this plan claims to implement and confirm each has a task:

| Spec section | Plan task(s) |
|---|---|
| §5.1 ModelLoader (bim) | Task 5 |
| §5.1 ModelLoader (TE folder) | Task 7 |
| §5.3 ChangeSet, TableDiff, TablePlan, classification | Tasks 11, 13, 15, 16 |
| §5.3 RefreshPolicy diffing | Task 12 |
| §5.4 TmslBuilder + integrity invariant + bookmark preservation | Tasks 19, 20, 21, 22 |
| §6 step 4 partition manifest write | Tasks 8, 9, 10 (writer ready; Plan 2 writes it during deploy flow) |
| §7A.7 bookmark preservation in alter | Tasks 20, 21 |
| §7A.8 retention math (history-loss detection) | Task 17 (Year only; Quarter/Month follow-up) |
| §7A.9 restore partition-set computation | Task 18 |
| §10 unit-test priorities 1, 2, 3, 6, 7, 8 | Tasks 5–22 |

Items NOT in this plan and tracked elsewhere:
- Refresh-scope derivation per-table refresh-type matrix (§6 step 13) — needs the refresh runner; Plan 2.
- §7A.4 refresh config (`bookmarkMode`, `applyOnFirstDeploy`) — needs config loader; Plan 3.
- Pre/post manifest diff integrity gate (§6 step 12a) — needs deploy flow orchestration; Plan 2.
- §10 unit-test priority 11 (manifest-diff gate test) — Plan 2.

---

## Done criteria for Plan 1

- [ ] All 24 tasks committed.
- [ ] `dotnet test` passes with 25+ tests, 0 warnings.
- [ ] Snapshot tests under `test/Weft.Core.Tests/Snapshots/` are committed and verified.
- [ ] Tag `plan-1-core-mvp-complete` exists.
- [ ] `WeftCore.Plan(source, target)` is callable and returns a `(ChangeSet, TmslJson)` for any in-memory `Database` pair.
- [ ] No code references network APIs (XMLA, MSAL, HTTPS) anywhere in `Weft.Core`.

When all items above are checked, Plan 1 is complete and Plan 2 (I/O: Auth + XMLA + CLI shell) can begin.
