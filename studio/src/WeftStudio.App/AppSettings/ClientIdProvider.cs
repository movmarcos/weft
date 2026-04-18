// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.AppSettings;

public static class ClientIdProvider
{
    public const string EnvVarName = "WEFT_STUDIO_CLIENTID";

    public static string Resolve(
        string? commandLineArg,
        string? envVar,
        string? userOverride,
        string baked)
    {
        if (!string.IsNullOrWhiteSpace(commandLineArg)) return commandLineArg;
        if (!string.IsNullOrWhiteSpace(envVar))         return envVar;
        if (!string.IsNullOrWhiteSpace(userOverride))   return userOverride;
        return baked;
    }

    public static string ResolveFromEnvironment(string? commandLineArg, string? userOverride, string baked) =>
        Resolve(
            commandLineArg,
            Environment.GetEnvironmentVariable(EnvVarName),
            userOverride,
            baked);
}
