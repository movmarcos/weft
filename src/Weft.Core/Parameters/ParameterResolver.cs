// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Parameters;

public sealed class ParameterResolver
{
    private readonly MParameterDiscoverer _discoverer = new();

    public IReadOnlyList<ParameterResolution> Resolve(
        Database sourceDb,
        IEnumerable<ParameterDeclaration> declarations,
        IReadOnlyDictionary<string, object?>? profileValues,
        IReadOnlyDictionary<string, string>? cliOverrides,
        IReadOnlyDictionary<string, object?>? paramsFileValues)
    {
        var declsByName = declarations.ToDictionary(d => d.Name, StringComparer.Ordinal);

        var resolutions = new List<ParameterResolution>();
        foreach (var (name, decl) in declsByName)
        {
            (object? value, ParameterValueSource source) resolved;
            if (cliOverrides is not null && cliOverrides.TryGetValue(name, out var cliValue))
                resolved = (cliValue, ParameterValueSource.Cli);
            else if (paramsFileValues is not null && paramsFileValues.TryGetValue(name, out var fileValue))
                resolved = (fileValue, ParameterValueSource.ParamsFile);
            else if (Environment.GetEnvironmentVariable($"WEFT_PARAM_{name}") is { } envValue)
                resolved = (envValue, ParameterValueSource.EnvVar);
            else if (profileValues is not null && profileValues.TryGetValue(name, out var yamlValue))
                resolved = (yamlValue, ParameterValueSource.ProfileYaml);
            else if (decl.Default is not null)
                resolved = (decl.Default, ParameterValueSource.ModelDefault);
            else if (decl.Required)
                throw new ParameterApplicationException(
                    $"Required parameter '{name}' has no value (CLI, params file, env var, profile YAML, or declaration default).");
            else continue;

            resolutions.Add(new ParameterResolution(name, decl.Type, resolved.value, resolved.source));
        }
        return resolutions;
    }

    public void Apply(Database sourceDb, IEnumerable<ParameterResolution> resolutions)
    {
        var discovered = _discoverer.Discover(sourceDb).ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var r in resolutions)
        {
            if (!discovered.TryGetValue(r.Name, out var param))
                throw new ParameterApplicationException(
                    $"Parameter '{r.Name}' declared in config but not present in source model.");

            var literal = ParameterValueCoercer.ToMLiteral(r.DeclaredType, r.RawValue);
            var metaSuffix = ExtractMetaSuffix(param.ExpressionText);
            param.Source.Expression = literal + (metaSuffix ?? "");
        }
    }

    private static string? ExtractMetaSuffix(string expression)
    {
        var idx = expression.IndexOf(" meta ", StringComparison.Ordinal);
        return idx >= 0 ? expression[idx..] : null;
    }
}
