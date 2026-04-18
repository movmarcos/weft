// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Weft.Config;

public static class EnvVarExpander
{
    private static readonly Regex Pattern = new(@"\$\{([A-Z_][A-Z0-9_]*)\}", RegexOptions.Compiled);

    public static string? Expand(string? input)
    {
        if (input is null) return null;
        return Pattern.Replace(input, match =>
        {
            var name = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(name);
            if (value is null)
                throw new WeftConfigValidationException(
                    $"Environment variable '{name}' referenced in config is not set.");
            return value;
        });
    }
}
