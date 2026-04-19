---
title: Read-only snapshot semantics (Studio)
eyebrow: Studio · Practical
order: 11
color: gold
icon: "◉"
related:
  - studio
  - studio-open-from-workspace
---
# Read-only snapshot semantics

When you open a model from a workspace (Connect to workspace…), Studio loads it as a **read-only snapshot**. This page explains why, what it changes in the UI, and how to persist edits anyway.

## Why read-only

Studio v0.1.x does not push changes back to a live workspace. Letting you edit and Save in-place would imply that Save deploys — which it doesn't. Rather than ship a half-feature, v0.1.1 makes the snapshot semantics explicit:

- The model is a point-in-time copy fetched at the moment you clicked **Open read-only**.
- The live server is not watching for changes — nobody knows you have it open.
- To push edits back, **Save As `.bim`**, then run `weft deploy` against the same workspace.

The full deploy flow lives in `weft deploy`. Studio is the editor; the CLI is the deployer.

## What changes in the UI

| Element | Behavior in read-only |
|---|---|
| Orange banner across the top | Shows `◉ READ-ONLY snapshot of <workspace> / <dataset>`. |
| **File → Save** | Disabled (`SaveCommand.CanExecute` returns false). |
| **File → Save As .bim…** | Enabled. Writes the current model JSON to a local file. |
| **File → Reload from workspace** | Enabled. Re-opens the Connect dialog so you can re-fetch a fresh snapshot. |
| Status bar | `<workspace> / <dataset> · read-only snapshot`. |

`.bim`-opened models still get the normal Save behavior — the banner / disabled-Save only apply when the source is a workspace.

## Save As .bim

`File → Save As .bim…` works on any open model — workspace-loaded or `.bim`-loaded. It serializes the current TOM database to JSON (with `IgnoreInferredObjects / IgnoreInferredProperties / IgnoreTimestamps`) and writes the file you choose.

Crucially, `Save As` does NOT mark the model as clean — your edits stay in the change tracker. Save As is a snapshot of "what's currently in memory", not a commit.

## Reload from workspace

In v0.1.1, **Reload from workspace** re-opens the Connect dialog rather than silently re-fetching with the same workspace + dataset + auth mode.

The full silent re-fetch (use the cached token, hit XMLA, swap the model in place) is deferred to a later iteration. For now, Reload = "open the dialog to grab a fresh snapshot" — your URL is still in the recent-workspaces dropdown so it's two clicks.

## Workflow: edit + redeploy

The intended cycle for editing a workspace model:

1. **Connect to workspace** → pick the dataset → Open read-only.
2. Edit measures in the DAX editor (changes stay in-memory, the change tracker turns dirty).
3. **Save As .bim** → write the model to your repo.
4. Commit the `.bim`.
5. Run `weft deploy --profile dev --source ./model.bim` from the CLI.
6. Back in Studio: **Reload from workspace** to verify the deploy landed.

Studio never deploys; the CLI never edits. Each tool does one thing.

## Concurrent edits

v0.1.1 does not detect server-side changes between the moment of snapshot and your Save As. If two people open the same workspace, edit independently, and Save As over the same `.bim`, the second writer wins — there's no merge UI. For team workflows, treat the `.bim` as your source of truth (commit it) and use the workspace as a deploy target only.

This is unchanged from v0.1.0's `.bim` editing model — Studio is single-writer per file.
