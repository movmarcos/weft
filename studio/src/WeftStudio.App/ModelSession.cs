// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Loading;

namespace WeftStudio.App;

public sealed class ModelSession
{
    public Database Database { get; }
    public string? SourcePath { get; }
    public bool IsDirty => ChangeTracker.HasUncommittedCommands;
    public ChangeTracker ChangeTracker { get; }

    private ModelSession(Database db, string? sourcePath)
    {
        Database = db;
        SourcePath = sourcePath;
        ChangeTracker = new ChangeTracker();
    }

    public static ModelSession OpenBim(string path)
    {
        var loader = new BimFileLoader();
        var database = loader.Load(path);
        return new ModelSession(database, path);
    }
}
