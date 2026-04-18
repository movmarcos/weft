// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using UserSettings = WeftStudio.App.Settings.Settings;
using WeftStudio.App.Settings;

namespace WeftStudio.App.Tests;

public class SettingsRecentWorkspacesTests
{
    [Fact]
    public void RecentWorkspaces_round_trips_through_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ws-test-{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(dir);
            var data = new UserSettings
            {
                RecentWorkspaces =
                {
                    new RecentWorkspace("powerbi://x/myorg/a", "ds1", "Interactive", DateTime.UtcNow),
                    new RecentWorkspace("powerbi://x/myorg/b", "ds2", "DeviceCode",  DateTime.UtcNow),
                },
                ClientIdOverride = "abc-123"
            };
            store.Save(data);

            var reloaded = new SettingsStore(dir).Load();
            reloaded.RecentWorkspaces.Should().HaveCount(2);
            reloaded.RecentWorkspaces[0].WorkspaceUrl.Should().Be("powerbi://x/myorg/a");
            reloaded.ClientIdOverride.Should().Be("abc-123");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
