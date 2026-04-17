# Weft — Power BI Semantic Model Deployment Tool

**Status:** Draft for review
**Date:** 2026-04-17
**Author:** Marcos Magri
**Audience:** Engineering team, ops, open-source community (target: MIT license on GitHub)

## 1. Problem Statement

Power BI semantic models with dynamically-created partitions (e.g., date-based rolling partitions on large fact tables) are painful to deploy. The current manual process at MUFG:

1. Engineers maintain the model in Git as `.bim` files (or Tabular Editor folder format).
2. A dynamic-partitioning job creates new partitions on production over time.
3. To deploy a change, engineers wait until the last possible moment, connect to the prod replica with Tabular Editor, export a TMSL script (which includes all current partitions), open SQL Server Management Studio, connect to the prod XMLA endpoint, and execute the TMSL.
4. A full-model replace refreshes every partition, which is slow and expensive.

**Pain points:**
- Manual, timing-sensitive, error-prone.
- Can't be automated in CI/CD.
- Full refresh is wasteful when only a handful of tables changed.
- No audit trail of what was deployed.

**Goal:** Build an open-source CLI tool — **Weft** — that performs a **diff-based, surgical deploy** of Power BI semantic models to Premium workspaces via XMLA endpoints, preserving existing partitions on unchanged tables. Integrates with the existing GitHub → TeamCity → Octopus pipeline (no Azure DevOps required).

## 2. Core Requirements

1. **Partition preservation is non-negotiable.** Tables that already exist in the target and whose schema has not changed must NEVER have their partition collection altered.
2. **Auto-detect dynamic partitions.** The tool must not rely on developers maintaining a list of dynamically-partitioned tables; the model has 200+ tables.
3. **Diff-based deploy.** Only tables that are new or have schema changes get touched. Untouched tables are not altered and not refreshed.
4. **Parameter injection per environment.** Model parameters (especially database names) must be overridable per deployment target.
5. **Authentication must include Service Principal + Certificate** (certs installed on machines, referenced by thumbprint), plus fallbacks for dev workflows.
6. **Integrates with GitHub / TeamCity / Octopus.** No dependency on Azure DevOps.
7. **Cross-platform development.** Engineers on macOS/Linux can build, test, and run the tool locally.
8. **Open-source-ready.** Clean repo structure, MIT license, good docs, reusable as a public project.
9. **Incremental refresh policy aware.** Tables with a declarative `RefreshPolicy` (Power BI incremental refresh) must be detected and refreshed via the policy, not via full reload. Policy-generated historical partitions (years → quarters → months) must be preserved across deploys; changes to the policy itself must propagate as a schema alter.

## 3. Naming

**Weft.** In weaving, the weft is the crosswise thread that binds lengthwise warp threads into fabric. Metaphor: the source schema is the weft threading across existing partitions to form the deployed model. CLI name: `weft`.

## 4. Architecture Overview

Cross-platform .NET 8 console CLI, single repo, layered projects:

```
┌────────────────────────────────────────────────────┐
│  weft CLI (System.CommandLine)                     │
│  Commands: validate · plan · deploy · refresh      │
└──────────────┬─────────────────────────────────────┘
               │
┌──────────────▼─────────────────────────────────────┐
│  Weft.Core  (pure logic, no I/O)                   │
│  - ModelLoader   (bim + TE folder -> TOM Database) │
│  - ModelDiffer   (source vs. target -> ChangeSet)  │
│  - TmslBuilder   (ChangeSet -> TMSL JSON script)   │
│  - ParameterResolver                               │
│  - Config        (profiles, auth, targets, hooks)  │
└──────┬───────────────────────────────┬─────────────┘
       │                               │
┌──────▼──────────┐           ┌────────▼─────────────┐
│ Weft.Xmla       │           │ Weft.Auth            │
│ (executes TMSL  │           │ (AAD token acquire:  │
│  + refresh via  │           │  SP+cert, SP+secret, │
│  TOM Server)    │           │  interactive, device)│
└─────────────────┘           └──────────────────────┘
```

**Design principles:**
- `Weft.Core` is pure and unit-testable. Takes models as input, returns a `ChangeSet`. No network I/O.
- `Weft.Xmla` is the thin I/O layer — TOM `Server` connections, TMSL execution, refresh polling.
- `Weft.Auth` is isolated — swap auth modes without touching logic.
- **Package output:** framework-dependent `win-x64` publish for Octopus targets; `osx-arm64` and `linux-x64` for dev and future Linux agents.

**CLI commands:**
- `weft validate --source ./model.bim` — parse source, report structural issues.
- `weft plan --source ./model.bim --target prod` — dry-run; shows ChangeSet + generated TMSL, writes nothing.
- `weft deploy --source ./model.bim --target prod` — runs plan + executes.
- `weft refresh --target prod --tables NewFactX,DimY` — refresh selected tables.

## 5. Core Components

### 5.1 `ModelLoader`
Loads source from either format into an in-memory TOM `Database`:
- Single `.bim` file → `JsonSerializer.DeserializeDatabase(text)`.
- Tabular Editor folder format (`Model/database.json` + per-object files) → reconstruct the `Database`.

One interface, two implementations, chosen by inspecting the path.

### 5.2 `TargetReader`
Opens a TOM `Server` against the XMLA endpoint using a token from `Weft.Auth`, reads the live `Database`. Returns the same `Database` type as `ModelLoader` — so the differ sees both sides symmetrically.

### 5.3 `ModelDiffer` (the core logic)
Compares source vs. target and emits a typed `ChangeSet`:

```
ChangeSet {
  TablesToAdd     : TablePlan[]  // new in source, with classification
  TablesToDrop    : string[]     // in target only
  TablesToAlter   : TableDiff[]  // schema-changed, keep partitions
  TablesUnchanged : string[]     // skip entirely
  Measures / Relationships / Roles / Perspectives
    / Cultures / DataSources / Expressions : per-type diffs
}

TableClassification =
  | Static                        // no policy, stable partitions
  | DynamicallyPartitioned        // target has partitions not in source
  | IncrementalRefreshPolicy      // table has a RefreshPolicy object

TablePlan {
  Name
  Classification: TableClassification
  SourceTable: Table
}

TableDiff {
  Name
  Classification: TableClassification
  ColumnChanges, HierarchyChanges, MeasureChanges,
  RefreshPolicyChanged: bool          // true when policy object differs
  PartitionStrategy: PreserveTarget | UseSource   // PreserveTarget is default
}
```

**Table schema equality excludes the `Partitions` collection** — only columns, expressions, hierarchies, annotations, the `RefreshPolicy` object, and table-level properties are compared. Partitions on existing tables are never touched.

**Classification logic (in priority order, first match wins):**

1. **IncrementalRefreshPolicy** — source or target table has a non-null `RefreshPolicy`. Partitions always preserved on target (the service materializes them per policy). Policy object itself is diffed as schema.
2. **DynamicallyPartitioned** — target has partition names not present in source (e.g., partitions created by an external job). Partitions always preserved.
3. **Static** — neither of the above. Partition collection participates in equality (so renaming a partition in source is a real change).

Classification feeds refresh-type selection (§6, step 13) and the human-readable plan output.

### 5.4 `TmslBuilder`
Translates `ChangeSet` into a single TMSL `sequence` script:
- `createOrReplace` only for the specific objects that changed (never the whole model).
- For altered tables: `alter` on the `Table` with new columns/measures, with the **target's current partition collection re-attached**.
- For new tables: `create` with source partitions.
- The whole thing is wrapped in one `sequence` so the server treats it as a transaction.

### 5.5 `XmlaExecutor`
Executes TMSL via `server.Execute(tmsl)`, captures `XmlaResult` messages, maps errors to typed exceptions. Also:
- `ExecuteDryRun` — server-side validation where supported; otherwise falls back to parse-only validation in `Weft.Core`.
- `Refresh(tables[], refreshType)` — builds a `refresh` TMSL command, polls `TraceEvent` for progress.

### 5.6 `AuthProvider`
Single interface `Task<AccessToken> GetTokenAsync()`. Implementations built on MSAL (`Microsoft.Identity.Client`), scope `https://analysis.windows.net/powerbi/api/.default`:
- `ServicePrincipalCertStoreAuth` — thumbprint → Windows cert store (prod/Octopus).
- `ServicePrincipalCertFileAuth` — `.pfx` file + password (dev on any OS).
- `ServicePrincipalSecretAuth` — client secret (fallback).
- `InteractiveAuth` — browser popup (dev).
- `DeviceCodeAuth` — headless dev boxes.

### 5.7 `ConfigLoader`
Loads a YAML config (`weft.yaml`) defining environment profiles, auth method per env, parameter declarations and per-env values, and hooks. Expands `${VAR}` environment-variable references.

### 5.8 `ParameterResolver`
Runs after the source model is loaded, before the diff. Rewrites the `Expression` property of each matching M parameter in the in-memory source `Database`. Details in §7.

### 5.9 `HookRunner`
Executes optional pre/post shell/PowerShell scripts per lifecycle phase: `pre-plan`, `pre-deploy`, `post-deploy`, `pre-refresh`, `post-refresh`, `on-failure`. Runs on the current OS's shell; passes `ChangeSet` summary as env vars and JSON on stdin.

## 6. Data Flow — Deploy Pipeline

A single `weft deploy --source ./model.bim --target prod --config weft.yaml` runs this sequence:

```
 1. Load config
    - Resolve "prod" profile: workspace URL, auth method, hook paths, overrides

 2. Resolve auth -> access token
    - AuthProvider.GetTokenAsync()  (SP+cert on Octopus; interactive on dev)

 3. Load source model
    - ModelLoader.Load("./model.bim") -> Database (source)
    - Apply parameter overrides: inject env-specific values via ParameterResolver

 4. Read target model
    - TargetReader.Read(workspace, token) -> Database (target)

 5. Diff
    - ModelDiffer.Compute(source, target) -> ChangeSet

 6. Pre-flight checks
    - Assert target model is not currently refreshing (reject if busy)
    - Assert TablesToDrop is empty OR --allow-drops is set AND profile permits
    - Assert ChangeSet is non-empty (otherwise: "nothing to do", exit 0)

 7. Hook: pre-plan (optional)

 8. Build TMSL
    - TmslBuilder.Build(ChangeSet) -> tmsl.json
    - Always written to ./artifacts/<timestamp>-<target>-plan.tmsl for audit

 9. Print plan
    - Human-readable summary:
       + Table FactOrdersNew (12 cols, 3 measures, 1 partition)
       ~ Table FactSales (2 measure changes, partitions preserved: 48)
       - Table LegacyDim

10. If --dry-run: exit 0   (this is also what `weft plan` does)

11. Hook: pre-deploy

12. Execute TMSL
    - XmlaExecutor.Execute(tmsl) inside a single TMSL `sequence`
    - On failure: log XMLA messages, run on-failure hook, exit non-zero
    - The sequence is transactional server-side -> no partial state

13. Determine refresh scope and per-table refresh type
    - refreshTargets = TablesToAdd ∪ TablesToAlter
    - Unchanged tables are NEVER refreshed
    - For each target table, derive refresh type:
        * Classification = IncrementalRefreshPolicy
            -> RefreshType = Policy, ApplyRefreshPolicy = true
            -> New table: warn "first refresh will materialize historical window per policy"
            -> Existing table with RefreshPolicyChanged=true: warn
               "policy change will trigger partition re-materialization"
        * Classification = DynamicallyPartitioned
            -> RefreshType = defaults.refresh.type (typically Full) on SPECIFIC
               partitions the table owns; never drop existing partitions
            -> If partition list is large, refresh only the newest partition
               (configurable via refresh.dynamicPartitionStrategy)
        * Classification = Static
            -> RefreshType = defaults.refresh.type (typically Full)

14. Hook: pre-refresh

15. Refresh
    - Group refresh commands by type to honor maxParallelism
    - XmlaExecutor.Refresh(group) per classification group
    - Poll every N seconds; stream progress to stdout
    - On failure: log, run on-failure hook, exit non-zero

16. Hook: post-deploy / post-refresh

17. Write deployment receipt
    - ./artifacts/<timestamp>-<target>-receipt.json
      { changeSet summary, tmsl hash, refresh durations, run user, git sha }
```

**Key properties of this flow:**
- **Idempotent** — running twice with no source change produces an empty ChangeSet and exits cleanly.
- **Plan artifact is always written**, even on success; Octopus archives it for audit.
- **Single transactional TMSL sequence** — no half-deployed state.
- **Drops are gated** by `--allow-drops` flag AND profile permission.
- **Refresh scope is derived from the diff**, never hand-maintained.
- **Hooks run out-of-process** so ops can integrate Teams/Slack/ServiceNow without modifying Weft.

## 7. Parameter Management

### 7.1 Principles

1. **Weft auto-discovers** all M parameters in the source — no parallel list to maintain.
2. **Explicit declaration is optional but recommended** — when declared, you get validation, required/optional semantics, descriptions, and drift detection.
3. **Per-profile values with a clear resolution order.**

### 7.2 Config schema

```yaml
parameters:
  - name: DatabaseName
    description: "Warehouse database name"
    type: string
    required: true
  - name: ServerName
    type: string
    required: true
  - name: RefreshCutoffDate
    type: string
    required: false
  - name: EnableDebugMeasures
    type: bool
    required: false

profiles:
  dev:
    parameters:
      DatabaseName: "EDW_DEV"
      ServerName: "dev-sql.corp.local"
      EnableDebugMeasures: true
  uat:
    parameters:
      DatabaseName: "EDW_UAT"
      ServerName: "uat-sql.corp.local"
  prod:
    parameters:
      DatabaseName: "EDW_PROD"
      ServerName: "prod-sql.corp.local"
      RefreshCutoffDate: "2026-04-17"
```

### 7.3 Resolution order (highest wins)

1. `--param Key=Value` on CLI (repeatable, for hotfixes / one-offs).
2. `--params-file ./my-params.json` (for large sets).
3. Environment variables `WEFT_PARAM_<Name>` (Octopus sensitive variables fit here).
4. `profiles.<env>.parameters` in `weft.yaml`.
5. Model's own default expression.

### 7.4 Validation

- Required parameter with no value for target profile → **FAIL** (exit 6) with listed missing names.
- Parameter declared in config but not present in model → **WARN** (possible drift).
- Parameter in model but not declared in config → **WARN** by default, **FAIL** when `parameters.strictMode: true`.
- Type mismatch (e.g. string for a bool parameter) → **FAIL**.

### 7.5 Diff behavior

A parameter value change between resolved source and target is **not** treated as a schema change for partition purposes. Swapping `DatabaseName` from `EDW` → `EDW_PROD` alters the parameter expression but leaves partitions on consuming tables untouched.

### 7.6 Data sources (legacy)

Models with direct `DataSource` connection strings (pre-M-parameter style) can still use an `overrides.<profile>.dataSources` block as a secondary mechanism. Recommended migration path: move to M parameters.

## 7A. Incremental Refresh Policy Support

Power BI supports declarative **incremental refresh** via a `RefreshPolicy` object on a `Table`. The model source declares the policy (rolling window, incremental window, granularity, source expression with `RangeStart`/`RangeEnd` parameters). At refresh time with `ApplyRefreshPolicy = true`, the Analysis Services engine materializes partitions according to the policy — typically a stack of Years → Quarters → Months — and rolls the window forward on each subsequent refresh.

This interacts with Weft's core partition-preservation rule in three distinct ways.

### 7A.1 Detection

A table is classified `IncrementalRefreshPolicy` when either the source or target `Table.RefreshPolicy` is non-null. Weft reads this from the TOM model; no configuration required. A table cannot be both `IncrementalRefreshPolicy` and `DynamicallyPartitioned` — the policy classification wins because the service owns those partitions.

### 7A.2 Policy changes are schema changes

The `RefreshPolicy` object is compared field-by-field during diffing:
- `RollingWindowGranularity`, `RollingWindowPeriods`
- `IncrementalGranularity`, `IncrementalPeriods`
- `IncrementalPeriodsOffset`
- `SourceExpression`
- `PollingExpression`
- `Mode` (Import vs. Hybrid for Direct Lake coexistence, where applicable)

If any field differs, `TableDiff.RefreshPolicyChanged = true`. The alter is applied; partition collection on target is preserved as always. On the next refresh, `ApplyRefreshPolicy = true` causes the engine to re-materialize partitions under the new policy — which may be expensive — so `weft plan` prints an explicit warning:

```
~ Table FactSales (policy changed: RollingWindowPeriods 2y -> 3y)
  WARNING: applying the new policy will re-materialize historical partitions.
           Current partitions: 48    Projected after policy: 60
```

### 7A.3 Refresh semantics

Refresh type is derived per table in step 13 of §6 Data Flow:

| Classification | RefreshType | ApplyRefreshPolicy | Notes |
|---|---|---|---|
| IncrementalRefreshPolicy (new table) | `Policy` | `true` | First refresh materializes history |
| IncrementalRefreshPolicy (policy changed) | `Policy` | `true` | Re-materializes per new policy |
| IncrementalRefreshPolicy (only schema changed) | `Policy` | `false` | Rolls window forward, no history churn |
| DynamicallyPartitioned | `defaults.refresh.type` | `false` | Targets specific partitions only |
| Static | `defaults.refresh.type` | n/a | As per global default |

The `EffectiveDate` XMLA option (for running the policy as if on a specific date) is exposed via `--effective-date YYYY-MM-DD` on `weft deploy` and `weft refresh`, defaulting to today (UTC).

### 7A.4 Config knobs

```yaml
defaults:
  refresh:
    type: full
    maxParallelism: 10
    pollIntervalSeconds: 15
    # Behavior overrides for incremental-refresh tables:
    incrementalPolicy:
      applyOnFirstDeploy: true     # false = deploy only, leave materialization to caller
      applyOnPolicyChange: true    # false = deploy the policy, skip re-materialization
    # Behavior override for dynamically-partitioned tables:
    dynamicPartitionStrategy:
      mode: newestOnly             # newestOnly | allTouched | none
      newestN: 1
```

When `applyOnFirstDeploy` is `false`, a newly deployed incremental-refresh table is left with its single template partition; ops triggers the first materialization out-of-band. This is useful when the first materialization is known to take hours and should run on a data team's schedule, not in a release window.

### 7A.5 Validation

At `plan` time, Weft issues targeted warnings that appear as a separate block in the plan output:

- Detected `RangeStart` / `RangeEnd` parameters used in M expressions → confirm they are declared in the model (Power BI requires this).
- Policy source expression references columns/tables not visible from the source data source binding → WARN.
- Policy defines a rolling window beyond the available data source history → INFO.

These are non-blocking by default; `--strict` promotes them to errors.

### 7A.6 Diff behavior with parameters

Changing `RangeStart` / `RangeEnd` parameter VALUES per environment (via §7 parameter resolution) does NOT mark an incremental-refresh table as changed — the parameter values are expected to differ and are not part of policy identity. Changing the parameter EXPRESSIONS in source (rare) is a parameter schema change, not a policy schema change.

---

## 8. Full Config Schema — `weft.yaml`

```yaml
version: 1

source:
  format: folder       # bim | folder
  path: ./Model

defaults:
  refresh:
    type: full         # full | dataOnly | calculate | automatic
    maxParallelism: 10
    pollIntervalSeconds: 15
  allowDrops: false
  timeoutMinutes: 60

profiles:
  dev:
    workspace: "powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev"
    database: "SalesModel"
    auth:
      mode: interactive
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_CLIENT_ID}"

  uat:
    workspace: "powerbi://api.powerbi.com/v1.0/myorg/Weft-UAT"
    database: "SalesModel"
    auth:
      mode: servicePrincipalCertFile
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certPath: "${WEFT_CERT_PATH}"
      certPassword: "${WEFT_CERT_PASSWORD}"

  prod:
    workspace: "powerbi://api.powerbi.com/v1.0/myorg/Weft-Prod"
    database: "SalesModel"
    auth:
      mode: servicePrincipalCertStore
      tenantId: "${WEFT_TENANT_ID}"
      clientId: "${WEFT_SP_CLIENT_ID}"
      certThumbprint: "${WEFT_CERT_THUMBPRINT}"
      certStoreLocation: LocalMachine     # LocalMachine | CurrentUser
      certStoreName: My
    refresh:
      maxParallelism: 4
    allowDrops: false

# See §7 for details
parameters:
  - name: DatabaseName
    type: string
    required: true

overrides:
  prod:
    dataSources:
      "Warehouse DB":
        server: "prod-sql.corp.local"
        database: "EDW"

hooks:
  preDeploy:   ./hooks/notify-teams.ps1
  postDeploy:  ./hooks/tag-git-release.sh
  onFailure:   ./hooks/open-incident.ps1
```

A JSON Schema (`schemas/weft.schema.json`) ships with the tool so editors provide autocomplete and validation.

## 9. Error Handling & Safety

### 9.1 Failure classes and exit codes

| Class | Examples | Exit |
|---|---|---|
| Config errors | YAML malformed, missing env var, invalid `auth.mode` | 2 |
| Auth errors | Token failure, cert not found, tenant wrong | 3 |
| Source-load errors | Corrupt `.bim`, invalid TE folder | 4 |
| Target-read errors | XMLA unreachable, DB not found, insufficient perms | 5 |
| Diff/validation errors | Drop without `--allow-drops`, missing required param | 6 |
| TMSL execution errors | XMLA fault during deploy | 7 |
| Refresh errors | Table refresh fails, timeout | 8 |

Exit codes are documented in `--help` and README so pipelines can branch on them.

### 9.2 Safety mechanisms

1. **Single transactional TMSL `sequence`** — Analysis Services rolls back on partial failure; Weft does not need custom undo for the deploy phase.
2. **Pre-flight refresh-busy check** — abort before touching a model currently being refreshed.
3. **Drop gate** — table drop requires BOTH `--allow-drops` on CLI AND `allowDrops: true` in the profile. Prod profile hard-pins to `false`.
4. **Structured diff preview** — `weft plan` is read-only and always writes a plan artifact.
5. **Plan and receipt artifacts** — always written to `./artifacts/`; Octopus packages them into the release.
6. **Refresh watchdog** — `timeoutMinutes` caps the refresh poll loop; on timeout Weft issues an XMLA `Cancel`.
7. **No silent retries on deploy.** Refresh retries opt-in via `refresh.retries: N`.
8. **No secrets in logs.** Sensitive config values are wrapped in a `Sensitive<string>` type and redacted to `***`.
9. **UTC ISO-8601 timestamps** everywhere.
10. **Abort on unknown TMSL change.** If `ModelDiffer` encounters an object type it does not know how to diff safely, it fails fast instead of generating an incomplete script.

### 9.3 Observability

- Structured JSON logs to stdout with `--log-format=json`; human-readable by default.
- `--verbose` surfaces MSAL and TOM internal events.
- Deployment receipts include git SHA, run user, durations, and TMSL content hash.

## 10. Testing Strategy

### 10.1 Layer 1 — Unit tests (`Weft.Core.Tests`)

Pure logic, no network, no file I/O. Fixtures are hand-crafted tiny TOM models stored as JSON in `test/fixtures/`.

**Coverage priorities:**
1. **Partition preservation** — the core feature:
   - Table unchanged → skipped entirely.
   - Table with column added → `Alter` produced; target partition collection copied verbatim.
   - Table with target-only extra partitions → always preserved.
   - New table → source partitions used.
   - Dropped table → `Delete` only with `--allow-drops`.
2. **Table classification** (§5.3):
   - RefreshPolicy present on source or target → IncrementalRefreshPolicy wins over DynamicallyPartitioned.
   - Target-only partitions → DynamicallyPartitioned.
   - Neither → Static.
3. **Refresh policy diffing** (§7A):
   - Policy field-by-field equality; any difference sets `RefreshPolicyChanged=true`.
   - Parameter VALUE changes on `RangeStart`/`RangeEnd` do NOT mark the table changed.
   - Parameter EXPRESSION changes in source DO mark it changed.
4. **Per-table refresh type derivation** (§6 step 13 matrix) — every classification/change-type combination produces the expected `RefreshType` + `ApplyRefreshPolicy` pair.
5. **Parameter resolution** — priority order, type coercion, required-missing fails, parameter change does not flag table schema change.
6. **TMSL builder produces a single `sequence`** with the right ops in the right order.
7. **Refresh scope derivation** — `TablesToAdd ∪ TablesToAlter`, excluding unchanged.

**Framework:** xUnit + FluentAssertions + Verify.Xunit (snapshot testing for TMSL output).

### 10.2 Layer 2 — Integration tests (`Weft.Integration.Tests`)

Real XMLA endpoint. Gated by env var (`WEFT_INT_TEST_WORKSPACE`). Covers:
- Auth flows against a dedicated test workspace (SP+secret suffices; cert flows unit-tested for OS-specific loaders).
- End-to-end deploy of a known-good model into a scratch database.
- End-to-end deploy of a model change with partition counts on other tables verified unchanged.
- Refresh progress polling.
- Failure paths.

Scratch databases named `weft_it_<runid>`, cleaned up in teardown.

### 10.3 Layer 3 — Contract & smoke tests

- JSON Schema validation for `weft.yaml`.
- CLI smoke tests — shell out to the built `weft` binary with `--help`, `validate`, `plan`, assert exit codes and output.
- `weft plan` snapshot tests via Verify.Xunit for canonical source/target pairs.

### 10.4 Out of scope for testing

- Validating TOM library behavior (Microsoft's contract).
- Mocking the XMLA server (integration tests cover the real path).
- String-level TMSL JSON assertions (snapshot testing handles this better).

## 11. Repo Layout

```
weft/
├── README.md
├── LICENSE                              # MIT
├── CONTRIBUTING.md
├── .editorconfig
├── Directory.Build.props
├── global.json
├── weft.sln
│
├── src/
│   ├── Weft.Cli/                        # console entry point
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   │   ├── ValidateCommand.cs
│   │   │   ├── PlanCommand.cs
│   │   │   ├── DeployCommand.cs
│   │   │   └── RefreshCommand.cs
│   │   ├── Output/                      # human + JSON formatters
│   │   └── Weft.Cli.csproj
│   │
│   ├── Weft.Core/                       # pure logic
│   │   ├── Loading/
│   │   ├── Diffing/
│   │   ├── Parameters/
│   │   ├── Tmsl/
│   │   ├── Config/
│   │   ├── Hooks/
│   │   └── Weft.Core.csproj
│   │
│   ├── Weft.Xmla/                       # I/O layer
│   └── Weft.Auth/                       # token acquisition
│
├── test/
│   ├── Weft.Core.Tests/
│   ├── Weft.Integration.Tests/
│   ├── Weft.Cli.Tests/
│   └── fixtures/
│
├── schemas/
│   └── weft.schema.json
│
├── docs/
│   ├── getting-started.md
│   ├── authentication.md
│   ├── parameters.md
│   ├── partition-preservation.md
│   ├── hooks.md
│   ├── troubleshooting.md
│   └── superpowers/specs/
│
├── samples/
│   ├── 01-simple-bim/
│   ├── 02-tabular-editor-folder/
│   ├── 03-with-parameters/
│   └── 04-full-pipeline/
│
├── build/
│   ├── teamcity/
│   │   └── settings.kts
│   └── octopus/
│       ├── step-templates/
│       │   ├── weft-deploy.json
│       │   └── weft-refresh.json
│       └── README.md
│
└── .github/
    ├── workflows/
    │   ├── ci.yml
    │   └── release.yml
    └── ISSUE_TEMPLATE/
```

## 12. CI/CD Integration

### 12.1 TeamCity (CI)

Single build configuration, Kotlin DSL. Runs on every PR and push to `main` / `release/*`.

```
Step 1  Restore             dotnet restore
Step 2  Build               dotnet build -c Release --no-restore
Step 3  Unit tests          dotnet test test/Weft.Core.Tests -c Release --no-build
                                        test/Weft.Cli.Tests
Step 4  Integration tests   dotnet test test/Weft.Integration.Tests
        (main/release only; uses CI secrets for SP + workspace)
Step 5  Publish             dotnet publish src/Weft.Cli -c Release
                              -r win-x64   -o artifacts/win-x64   --self-contained false
                              -r linux-x64 -o artifacts/linux-x64 --self-contained false
                              -r osx-arm64 -o artifacts/osx-arm64 --self-contained false
Step 6  Package             Create NuGet package and per-RID zip
Step 7  Push to Octopus     octo push --package=Weft.<version>.<rid>.zip
                                      --server=$OCTOPUS_URL --apiKey=$OCTOPUS_API_KEY
Step 8  Create release      octo create-release --project=Weft --version=<ver>
```

PR builds skip integration tests; main-branch builds run the full suite. Nightly runs a full deploy/rollback cycle against the CI workspace.

### 12.2 Octopus (CD)

Ships two step templates in the repo at `build/octopus/step-templates/`:
- `Weft: Deploy Power BI Model`
- `Weft: Refresh Power BI Model`

**Deploy step parameters (examples):**
- Source Path, Config File, Target Profile
- Service Principal Client Id, Tenant Id (project variables)
- Certificate Thumbprint (sensitive, per-env scope)
- Allow Drops (checkbox)
- Refresh After Deploy (checkbox)
- Extra Parameters (multiline `Key=Value`)

**Execution contract:**
```bash
export WEFT_TENANT_ID="$(get_octopus_variable TenantId)"
export WEFT_SP_CLIENT_ID="$(get_octopus_variable SpClientId)"
export WEFT_CERT_THUMBPRINT="$(get_octopus_variable CertThumbprint)"

./weft deploy \
  --source "$SourcePath" \
  --config "$ConfigFile" \
  --target "$TargetProfile" \
  --log-format json \
  $( [ "$AllowDrops"  = "True" ] && echo "--allow-drops" ) \
  $( [ "$RefreshAfter" = "False" ] && echo "--no-refresh" )
```

Exit code from `weft` propagates to Octopus. Octopus surfaces `artifacts/*.tmsl` and `receipt.json` for audit.

### 12.3 Octopus variable pattern

- **Project variables:** `TenantId`, `SpClientId`.
- **Environment-scoped:** `CertThumbprint`, workspace URL, DB name, all `WEFT_PARAM_*`.
- **Sensitive:** cert thumbprint, cert password (file mode), SP secrets.

This means model parameters are managed in Octopus's variable UI, scoped per environment, with audit history — ops changes `DatabaseName` from `EDW_PROD` to `EDW_PROD_HOTFIX` in Octopus, triggers a re-deploy, model picks it up. No YAML edit, no commit.

### 12.4 GitHub Actions (open source users)

Mirrors TeamCity using the `dotnet` action with a RID matrix. Publishes to GitHub Releases on tag. Integration tests gated on repository secrets.

### 12.5 Release & versioning

- **SemVer** (`1.0.0`), tags prefixed `v`.
- `GitVersion` computes version from commit history.
- `release-please` generates changelog on `main` merges.
- Binaries published to **GitHub Releases**; `Weft.Core` published as a NuGet package on **nuget.org** for downstream tooling.

## 13. Out of Scope for v1

- **Plugin system** — third-party commands. YAGNI until someone asks.
- **Manual partition lifecycle management** — creating/dropping partitions on a custom schedule outside of a declared `RefreshPolicy` belongs to data-engineering rhythms, not release tooling. (Weft DOES handle `RefreshPolicy`-driven partition rolling via `ApplyRefreshPolicy` — see §7A.)
- **PBIX deployment** — Weft deploys the model (TMSL-compatible). PBIX-specific features (reports, visuals) are outside scope; `pbi-tools` already exists for that layer.
- **Azure Analysis Services / SSAS on-prem** support. XMLA library works with both, but targeting PBI Premium first keeps auth/config scope focused. Easy follow-on.
- **Managed Identity auth** — add if/when an Azure-VM Octopus worker needs it.
- **Rollback command** — the transactional `sequence` means deploys never leave partial state, and re-deploying an older git revision is the rollback. A dedicated command can be added if ops asks.

## 14. Open Questions

None at design-approval stage. Implementation details (e.g., specific MSAL builder patterns, exact TOM diff helpers for edge-case object types) will be settled in the plan and during TDD.
