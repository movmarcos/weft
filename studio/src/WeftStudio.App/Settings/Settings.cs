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
