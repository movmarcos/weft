// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();

        // ContentControl materializes ExplorerView lazily — first when ShellViewModel.Explorer
        // becomes non-null. Window.Loaded fires once at startup before any model is open, so
        // a one-shot visual-tree search for ExplorerView there always returns null and the
        // MeasureDoubleClicked event never gets wired. Re-find + rewire whenever Explorer changes.
        Loaded += (_, _) =>
        {
            if (DataContext is not ShellViewModel shellVm) return;

            shellVm.SaveAsRequested += async (_, _) => await OnSaveAs();
            shellVm.ReloadRequested += (_, _) => OnReload();

            shellVm.WhenAnyValue(x => x.Explorer)
                .Subscribe(_ => Dispatcher.UIThread.Post(WireExplorerDoubleClick));
        };
    }

    private void WireExplorerDoubleClick()
    {
        var explorerView = this.GetVisualDescendants().OfType<ExplorerView>().FirstOrDefault();
        if (explorerView is null) return;
        explorerView.MeasureDoubleClicked -= OnMeasureDoubleClicked;
        explorerView.MeasureDoubleClicked += OnMeasureDoubleClicked;
        explorerView.SelectionChanged -= OnTreeSelectionChanged;
        explorerView.SelectionChanged += OnTreeSelectionChanged;
    }

    private void OnMeasureDoubleClicked(string table, string measure)
    {
        if (DataContext is ShellViewModel vm) vm.OpenMeasure(table, measure);
    }

    private void OnTreeSelectionChanged(object? payload)
    {
        if (DataContext is ShellViewModel vm) vm.ShowInspectorFor(payload);
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

        var store = new WeftStudio.App.Settings.SettingsStore(
            WeftStudio.App.Settings.SettingsStore.DefaultDirectory);
        var settings = store.Load();
        dialogVm.RecentUrls = settings.RecentWorkspaces
            .OrderByDescending(w => w.LastUsedUtc)
            .Select(w => w.WorkspaceUrl)
            .Distinct()
            .Take(10)
            .ToList();

        var dialog = new ConnectDialog { DataContext = dialogVm };
        await dialog.ShowDialog(this);

        if (dialog.Result is not null)
        {
            vm.AdoptSession(dialog.Result, workspaceLabel: dialog.WorkspaceLabel);

            var updated = store.Load();
            updated.RecentWorkspaces.RemoveAll(w => w.WorkspaceUrl == dialogVm.Url);
            updated.RecentWorkspaces.Insert(0, new WeftStudio.App.Settings.RecentWorkspace(
                WorkspaceUrl: dialogVm.Url,
                LastDatasetName: dialog.WorkspaceLabel ?? "",
                AuthMode: dialogVm.AuthMode.ToString(),
                LastUsedUtc: DateTime.UtcNow));
            if (updated.RecentWorkspaces.Count > 10)
                updated.RecentWorkspaces.RemoveRange(10, updated.RecentWorkspaces.Count - 10);
            store.Save(updated);
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt)
            dt.Shutdown();
    }

    private async Task OnSaveAs()
    {
        if (DataContext is not ShellViewModel vm || vm.Explorer is null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save model as .bim",
            DefaultExtension = "bim",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Power BI model") { Patterns = new[] { "*.bim" } }
            }
        });
        var path = file?.TryGetLocalPath();
        if (path is not null)
            WeftStudio.App.Persistence.BimSaver.SaveAs(vm.Explorer.Session, path);
    }

    private void OnReload()
    {
        // v0.1.1: simplest reload — re-open the Connect dialog pre-filled.
        // Full re-fetch with persisted workspace state is deferred to a later iteration.
        if (DataContext is ShellViewModel vm && vm.IsReadOnly)
            OnConnectToWorkspace(this, new RoutedEventArgs());
    }
}
