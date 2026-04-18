// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using WeftStudio.App;
using WeftStudio.Ui.DaxEditor;
using WeftStudio.Ui.Explorer;
using WeftStudio.Ui.Inspector;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private ActivityMode _activeMode = ActivityMode.Explorer;
    private ExplorerViewModel? _explorer;
    private DaxEditorViewModel? _activeTab;
    private InspectorViewModel? _inspector;

    public ShellViewModel()
    {
        this.WhenAnyValue(x => x.ActiveTab).Subscribe(tab =>
        {
            if (tab is null || Explorer is null) Inspector = null;
            else Inspector = new InspectorViewModel(Explorer.Session, tab.TableName, tab.MeasureName);
        });
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

    public void OpenModel(string bimPath)
    {
        var session = ModelSession.OpenBim(bimPath);
        Explorer = new ExplorerViewModel(session);
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
