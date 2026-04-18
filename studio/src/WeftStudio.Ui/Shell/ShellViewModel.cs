// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using ReactiveUI;

namespace WeftStudio.Ui.Shell;

public enum ActivityMode { Explorer, Diagram, Diff, Search }

public sealed class ShellViewModel : ReactiveObject
{
    private ActivityMode _activeMode = ActivityMode.Explorer;

    public ActivityMode ActiveMode
    {
        get => _activeMode;
        set => this.RaiseAndSetIfChanged(ref _activeMode, value);
    }
}
