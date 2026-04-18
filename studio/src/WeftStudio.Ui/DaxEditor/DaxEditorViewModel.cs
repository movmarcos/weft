// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using ReactiveUI;
using WeftStudio.App;
using WeftStudio.App.Commands;

namespace WeftStudio.Ui.DaxEditor;

public sealed class DaxEditorViewModel : ReactiveObject
{
    private string _text = "";
    private readonly ModelSession _session;
    private readonly string _tableName;
    private readonly string _measureName;
    private string _originalText;

    public DaxEditorViewModel(ModelSession session, string tableName, string measureName)
    {
        _session = session;
        _tableName = tableName;
        _measureName = measureName;
        _originalText = session.Database.Model.Tables[tableName]
            .Measures[measureName].Expression ?? "";
        _text = _originalText;
    }

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public string MeasureName => _measureName;
    public string TableName   => _tableName;
    public bool IsDirty       => _text != _originalText;

    public void Commit()
    {
        if (!IsDirty) return;
        var cmd = new UpdateDaxCommand(_tableName, _measureName, _originalText, _text);
        _session.ChangeTracker.Execute(_session.Database, cmd);
        _originalText = _text;
        this.RaisePropertyChanged(nameof(IsDirty));
    }
}
