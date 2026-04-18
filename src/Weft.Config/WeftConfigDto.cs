// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

internal sealed class WeftConfigDto
{
    public int Version { get; set; }
    public SourceDto? Source { get; set; }
    public DefaultsDto? Defaults { get; set; }
    public Dictionary<string, ProfileDto> Profiles { get; set; } = new();
    public List<ParameterDeclarationDto> Parameters { get; set; } = new();
    public Dictionary<string, ProfileOverridesDto> Overrides { get; set; } = new();
    public HooksDto? Hooks { get; set; }

    public WeftConfig ToDomain() => new(
        Version,
        Source?.ToDomain(),
        Defaults?.ToDomain(),
        Profiles.ToDictionary(p => p.Key, p => p.Value.ToDomain()),
        Parameters.Select(p => p.ToDomain()).ToList(),
        Overrides.ToDictionary(o => o.Key, o => o.Value.ToDomain()),
        Hooks?.ToDomain());
}

internal sealed class SourceDto
{
    public string Format { get; set; } = "bim";
    public string Path { get; set; } = "";
    public SourceConfigSection ToDomain() => new(Format, Path);
}

internal sealed class DefaultsDto
{
    public RefreshDto? Refresh { get; set; }
    public bool AllowDrops { get; set; }
    public bool AllowHistoryLoss { get; set; }
    public int TimeoutMinutes { get; set; } = 60;
    public DefaultsConfigSection ToDomain() =>
        new(Refresh?.ToDomain(), AllowDrops, AllowHistoryLoss, TimeoutMinutes);
}

internal sealed class RefreshDto
{
    public string? Type { get; set; }
    public int? MaxParallelism { get; set; }
    public int? PollIntervalSeconds { get; set; }
    public IncrementalPolicyDto? IncrementalPolicy { get; set; }
    public DynamicPartitionStrategyDto? DynamicPartitionStrategy { get; set; }
    public RefreshConfigSection ToDomain() => new(Type, MaxParallelism, PollIntervalSeconds,
        IncrementalPolicy?.ToDomain(), DynamicPartitionStrategy?.ToDomain());
}

internal sealed class IncrementalPolicyDto
{
    public bool ApplyOnFirstDeploy { get; set; } = true;
    public bool ApplyOnPolicyChange { get; set; } = true;
    public string BookmarkMode { get; set; } = "preserve";
    public IncrementalPolicyConfig ToDomain() => new(ApplyOnFirstDeploy, ApplyOnPolicyChange, BookmarkMode);
}

internal sealed class DynamicPartitionStrategyDto
{
    public string Mode { get; set; } = "newestOnly";
    public int NewestN { get; set; } = 1;
    public DynamicPartitionStrategyConfig ToDomain() => new(Mode, NewestN);
}

internal sealed class ProfileDto
{
    public string Workspace { get; set; } = "";
    public string Database { get; set; } = "";
    public AuthDto Auth { get; set; } = new();
    public RefreshDto? Refresh { get; set; }
    public bool? AllowDrops { get; set; }
    public bool? AllowHistoryLoss { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public ProfileConfig ToDomain() => new(Workspace, Database, Auth.ToDomain(),
        Refresh?.ToDomain(), AllowDrops, AllowHistoryLoss, Parameters);
}

internal sealed class AuthDto
{
    public string Mode { get; set; } = "Interactive";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? CertPath { get; set; }
    public string? CertPassword { get; set; }
    public string? CertThumbprint { get; set; }
    public string? CertStoreLocation { get; set; }
    public string? CertStoreName { get; set; }
    public string? RedirectUri { get; set; }
    public AuthConfigSection ToDomain() => new(Mode, TenantId, ClientId, ClientSecret,
        CertPath, CertPassword, CertThumbprint, CertStoreLocation, CertStoreName, RedirectUri);
}

internal sealed class ParameterDeclarationDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public object? Default { get; set; }
    public ParameterDeclaration ToDomain() => new(Name, Description, Type, Required, Default);
}

internal sealed class ProfileOverridesDto
{
    public Dictionary<string, DataSourceOverrideDto>? DataSources { get; set; }
    public ProfileOverridesSection ToDomain() =>
        new(DataSources?.ToDictionary(d => d.Key, d => d.Value.ToDomain()));
}

internal sealed class DataSourceOverrideDto
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public DataSourceOverride ToDomain() => new(Server, Database);
}

internal sealed class HooksDto
{
    public string? PrePlan { get; set; }
    public string? PreDeploy { get; set; }
    public string? PostDeploy { get; set; }
    public string? PreRefresh { get; set; }
    public string? PostRefresh { get; set; }
    public string? OnFailure { get; set; }
    public HooksConfigSection ToDomain() =>
        new(PrePlan, PreDeploy, PostDeploy, PreRefresh, PostRefresh, OnFailure);
}
