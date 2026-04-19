// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var explorerView = this.GetVisualDescendants().OfType<ExplorerView>().FirstOrDefault();
            if (explorerView is null) return;
            explorerView.MeasureDoubleClicked += (table, measure) =>
            {
                if (DataContext is ShellViewModel vm) vm.OpenMeasure(table, measure);
            };
        };
    }

    private async void OnFileOpen(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a .bim model",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Power BI model") { Patterns = new[] { "*.bim" } }
            }
        });
        var f = files.FirstOrDefault()?.TryGetLocalPath();
        if (f is not null && DataContext is ShellViewModel vm)
            await vm.OpenModelCommand.Execute(f).FirstAsync();
    }

    private async void OnConnectToWorkspace(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel vm) return;

        var reader = new Weft.Xmla.TargetReader();
        var mgr = new ConnectionManager(
            authProviderFactory: opts => Weft.Auth.AuthProviderFactory.Create(opts),
            reader: reader);

        var dialogVm = new ConnectDialogViewModel(mgr)
        {
            ClientId = WeftStudio.App.AppSettings.ClientIdProvider.ResolveFromEnvironment(
                commandLineArg: null,
                userOverride: null,
                baked: ""),
        };

        var dialog = new ConnectDialog { DataContext = dialogVm };
        await dialog.ShowDialog(this);

        if (dialog.Result is not null)
            vm.AdoptSession(dialog.Result, workspaceLabel: dialog.WorkspaceLabel);
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt)
            dt.Shutdown();
    }
}
