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
