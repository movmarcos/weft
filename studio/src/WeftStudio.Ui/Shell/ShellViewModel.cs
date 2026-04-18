// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using ReactiveUI;
using WeftStudio.App;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private ActivityMode _activeMode = ActivityMode.Explorer;
    private ExplorerViewModel? _explorer;

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

    public void OpenModel(string bimPath)
    {
        var session = ModelSession.OpenBim(bimPath);
        Explorer = new ExplorerViewModel(session);
    }
}
