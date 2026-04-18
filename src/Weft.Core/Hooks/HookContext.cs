// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

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
