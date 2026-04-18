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

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt)
            dt.Shutdown();
    }
}
