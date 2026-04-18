// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.Ui.Settings;
using AppSettings = WeftStudio.Ui.Settings.Settings;

namespace WeftStudio.Ui.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Save_then_load_round_trips_RecentFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(dir);
            var data = new AppSettings { RecentFiles = { "a.bim", "b.bim" } };
            store.Save(data);

            var store2 = new SettingsStore(dir);
            var loaded = store2.Load();
            loaded.RecentFiles.Should().Equal("a.bim", "b.bim");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_returns_empty_when_no_file_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        var store = new SettingsStore(dir);
        var loaded = store.Load();
        loaded.RecentFiles.Should().BeEmpty();
    }
}
