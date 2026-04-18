// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace WeftStudio.App.Settings;

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WeftStudio");

    public Settings Load()
    {
        if (!File.Exists(_path)) return new Settings();
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings();
        }
        catch (JsonException)
        {
            // Corrupted settings — return clean defaults rather than crashing.
            return new Settings();
        }
    }

    public void Save(Settings s) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
}
