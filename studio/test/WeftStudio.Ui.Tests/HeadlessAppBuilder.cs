// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Headless;
using WeftStudio.Ui;

[assembly: AvaloniaTestApplication(typeof(WeftStudio.Ui.Tests.TestAppBuilder))]

namespace WeftStudio.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
