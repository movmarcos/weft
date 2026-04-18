// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

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
