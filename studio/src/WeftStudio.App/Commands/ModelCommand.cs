// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Commands;

public abstract class ModelCommand
{
    /// <summary>Human-readable, shows in Undo menu.</summary>
    public abstract string Description { get; }

    /// <summary>Apply the change to the model. Throws on invalid transition.</summary>
    public abstract void Apply(Database db);

    /// <summary>Undo the change. Must leave the model identical to pre-Apply state.</summary>
    public abstract void Revert(Database db);
}
