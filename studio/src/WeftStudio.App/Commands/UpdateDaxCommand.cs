// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public sealed class UpdateDaxCommand : ModelCommand
{
    private readonly string _tableName;
    private readonly string _measureName;
    private readonly string _originalExpression;
    private readonly string _newExpression;

    public UpdateDaxCommand(string tableName, string measureName,
        string originalExpression, string newExpression)
    {
        _tableName = tableName;
        _measureName = measureName;
        _originalExpression = originalExpression;
        _newExpression = newExpression;
    }

    public override string Description =>
        $"Update DAX for {_tableName}[{_measureName}]";

    public override void Apply(Database db) =>
        db.Model.Tables[_tableName].Measures[_measureName].Expression = _newExpression;

    public override void Revert(Database db) =>
        db.Model.Tables[_tableName].Measures[_measureName].Expression = _originalExpression;
}
