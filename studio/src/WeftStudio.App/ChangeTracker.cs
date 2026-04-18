// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using WeftStudio.App.Commands;

namespace WeftStudio.App;

public sealed class ChangeTracker
{
    private readonly Stack<ModelCommand> _undo = new();
    private readonly Stack<ModelCommand> _redo = new();
    private int _cleanBoundary = 0;

    public IReadOnlyList<ModelCommand> UndoHistory => _undo.Reverse().ToList();
    public IReadOnlyList<ModelCommand> RedoHistory => _redo.Reverse().ToList();
    public bool HasUncommittedCommands => _undo.Count != _cleanBoundary;

    public void Execute(Database db, ModelCommand command)
    {
        command.Apply(db);
        _undo.Push(command);
        _redo.Clear();
    }

    public void Undo(Database db)
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Revert(db);
        _redo.Push(cmd);
    }

    public void Redo(Database db)
    {
        if (_redo.Count == 0) return;
        var cmd = _redo.Pop();
        cmd.Apply(db);
        _undo.Push(cmd);
    }

    public void MarkClean() => _cleanBoundary = _undo.Count;
}
