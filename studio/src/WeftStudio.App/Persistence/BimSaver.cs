// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Persistence;

public static class BimSaver
{
    public static void Save(ModelSession session)
    {
        if (string.IsNullOrEmpty(session.SourcePath))
            throw new InvalidOperationException(
                "Cannot save: session has no source path. Use Save-As instead.");

        var json = JsonSerializer.SerializeDatabase(session.Database,
            new SerializeOptions { IgnoreInferredObjects = true,
                                   IgnoreInferredProperties = true,
                                   IgnoreTimestamps = true });

        File.WriteAllText(session.SourcePath!, json);
        session.ChangeTracker.MarkClean();
    }

    public static void SaveAs(ModelSession session, string path)
    {
        var json = JsonSerializer.SerializeDatabase(session.Database,
            new SerializeOptions
            {
                IgnoreInferredObjects = true,
                IgnoreInferredProperties = true,
                IgnoreTimestamps = true
            });
        File.WriteAllText(path, json);
    }
}
