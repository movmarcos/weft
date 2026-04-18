// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

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
