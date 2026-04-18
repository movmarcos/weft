// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new ShellViewModel();
            var args = desktop.Args ?? Array.Empty<string>();
            if (args.Length > 0 && File.Exists(args[0]))
                vm.OpenModel(args[0]);
            desktop.MainWindow = new ShellWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
