// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace WeftStudio.Ui.Settings;

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

    public Settings Load() =>
        File.Exists(_path)
            ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings()
            : new Settings();

    public void Save(Settings s) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
}
