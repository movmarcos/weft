// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public sealed class RenameMeasureCommand : ModelCommand
{
    private readonly string _tableName;
    private readonly string _originalName;
    private readonly string _newName;

    public RenameMeasureCommand(string tableName, string originalName, string newName)
    {
        _tableName = tableName;
        _originalName = originalName;
        _newName = newName;
    }

    public override string Description =>
        $"Rename measure {_tableName}[{_originalName}] → {_newName}";

    public override void Apply(Database db)
    {
        var table = db.Model.Tables[_tableName];
        if (table.Measures.Contains(_newName))
            throw new InvalidOperationException(
                $"Measure '{_newName}' already exists on table '{_tableName}'.");
        table.Measures[_originalName].Name = _newName;
    }

    public override void Revert(Database db)
    {
        var table = db.Model.Tables[_tableName];
        table.Measures[_newName].Name = _originalName;
    }
}
