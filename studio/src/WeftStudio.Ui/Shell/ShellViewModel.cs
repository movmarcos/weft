// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WeftStudio.App;
using WeftStudio.App.Persistence;
using WeftStudio.Ui.DaxEditor;
using WeftStudio.Ui.Explorer;
using WeftStudio.Ui.Inspector;
using WeftStudio.App.Settings;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private readonly SettingsStore _store = new(SettingsStore.DefaultDirectory);

    private ActivityMode _activeMode = ActivityMode.Explorer;
    private ExplorerViewModel? _explorer;
    private DaxEditorViewModel? _activeTab;
    private InspectorViewModel? _inspector;
    private bool _isReadOnly;
    private string? _workspaceLabel;

    public ShellViewModel()
    {
        this.WhenAnyValue(x => x.ActiveTab).Subscribe(tab =>
        {
            if (tab is null || Explorer is null) Inspector = null;
            else Inspector = new InspectorViewModel(Explorer.Session, tab.TableName, tab.MeasureName);
        });

        var canSave = this.WhenAnyValue(x => x.Explorer, x => x.IsReadOnly,
            (exp, ro) => exp is not null && !ro);

        SaveCommand = ReactiveCommand.Create(() =>
        {
            if (Explorer is not null) BimSaver.Save(Explorer.Session);
        }, canSave);

        OpenModelCommand = ReactiveCommand.Create<string>(OpenModel);

        var canSaveAs = this.WhenAnyValue(x => x.Explorer).Select(e => e is not null);
        SaveAsCommand = ReactiveCommand.Create(
            () => SaveAsRequested?.Invoke(this, EventArgs.Empty),
            canSaveAs);

        var canReload = this.WhenAnyValue(x => x.IsReadOnly);
        ReloadFromWorkspaceCommand = ReactiveCommand.Create(
            () => ReloadRequested?.Invoke(this, EventArgs.Empty),
            canReload);

        this.WhenAnyValue(x => x.Explorer).Subscribe(_ =>
            this.RaisePropertyChanged(nameof(StatusText)));
    }

    public ActivityMode ActiveMode
    {
        get => _activeMode;
        set => this.RaiseAndSetIfChanged(ref _activeMode, value);
    }

    public ExplorerViewModel? Explorer
    {
        get => _explorer;
        set => this.RaiseAndSetIfChanged(ref _explorer, value);
    }

    public ObservableCollection<DaxEditorViewModel> OpenTabs { get; } = new();

    public DaxEditorViewModel? ActiveTab
    {
        get => _activeTab;
        set => this.RaiseAndSetIfChanged(ref _activeTab, value);
    }

    public InspectorViewModel? Inspector
    {
        get => _inspector;
        set => this.RaiseAndSetIfChanged(ref _inspector, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    public string? WorkspaceLabel
    {
        get => _workspaceLabel;
        private set => this.RaiseAndSetIfChanged(ref _workspaceLabel, value);
    }

    public ReactiveCommand<Unit, Unit>   SaveCommand                  { get; }
    public ReactiveCommand<string, Unit> OpenModelCommand             { get; }
    public ReactiveCommand<Unit, Unit>   SaveAsCommand                { get; }
    public ReactiveCommand<Unit, Unit>   ReloadFromWorkspaceCommand   { get; }

    public event EventHandler? SaveAsRequested;
    public event EventHandler? ReloadRequested;

    public string StatusText => Explorer is null
        ? "No model open"
        : $"{Path.GetFileName(Explorer.Session.SourcePath)}" +
          (Explorer.Session.IsDirty ? " · unsaved changes" : "");

    /// <summary>
    /// Installs a pre-built ModelSession into the shell (used by the
    /// Connect-to-workspace flow). OpenModel still handles the .bim path.
    /// </summary>
    public void AdoptSession(ModelSession session, string? workspaceLabel = null)
    {
        OpenTabs.Clear();
        ActiveTab = null;
        Explorer = new ExplorerViewModel(session);
        IsReadOnly = session.ReadOnly;
        WorkspaceLabel = workspaceLabel;
    }

    public void OpenModel(string bimPath)
    {
        var session = ModelSession.OpenBim(bimPath);
        IsReadOnly = false;
        WorkspaceLabel = null;
        Explorer = new ExplorerViewModel(session);

        var s = _store.Load();
        s.RecentFiles.Remove(bimPath);
        s.RecentFiles.Insert(0, bimPath);
        if (s.RecentFiles.Count > 10) s.RecentFiles.RemoveRange(10, s.RecentFiles.Count - 10);
        _store.Save(s);
    }

    public void OpenMeasure(string tableName, string measureName)
    {
        if (Explorer is null) return;
        var existing = OpenTabs.FirstOrDefault(
            t => t.TableName == tableName && t.MeasureName == measureName);
        if (existing is not null)
        {
            ActiveTab = existing;
            return;
        }
        var tab = new DaxEditorViewModel(Explorer.Session, tableName, measureName);
        OpenTabs.Add(tab);
        ActiveTab = tab;
    }
}
