// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using ReactiveUI;
using WeftStudio.App;
using WeftStudio.App.Commands;

namespace WeftStudio.Ui.Inspector;

public sealed class InspectorViewModel : ReactiveObject
{
    private readonly ModelSession _session;
    private readonly string _tableName;
    private string _originalName;
    private string _name;

    public InspectorViewModel(ModelSession session, string tableName, string measureName)
    {
        _session = session;
        _tableName = tableName;
        _originalName = measureName;
        _name = measureName;
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public void CommitRename()
    {
        if (_name == _originalName) return;
        var cmd = new RenameMeasureCommand(_tableName, _originalName, _name);
        _session.ChangeTracker.Execute(_session.Database, cmd);
        _originalName = _name;
    }
}
