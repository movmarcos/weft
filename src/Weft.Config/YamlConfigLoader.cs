// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Weft.Config;

public static class YamlConfigLoader
{
    public static WeftConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}", path);

        var yaml = File.ReadAllText(path);
        return LoadFromString(yaml);
    }

    public static WeftConfig LoadFromString(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var dto = deserializer.Deserialize<WeftConfigDto>(yaml)
            ?? throw new WeftConfigValidationException("Empty YAML.");
        return dto.ToDomain();
    }
}
