// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.App.Persistence;

public static class BimSaver
{
    private static string Serialize(ModelSession session) =>
        JsonSerializer.SerializeDatabase(session.Database,
            new SerializeOptions
            {
                IgnoreInferredObjects = true,
                IgnoreInferredProperties = true,
                IgnoreTimestamps = true
            });

    public static void Save(ModelSession session)
    {
        if (string.IsNullOrEmpty(session.SourcePath))
            throw new InvalidOperationException(
                "Cannot save: session has no source path. Use Save-As instead.");

        File.WriteAllText(session.SourcePath!, Serialize(session));
        session.ChangeTracker.MarkClean();
    }

    public static void SaveAs(ModelSession session, string path)
    {
        File.WriteAllText(path, Serialize(session));
        // Workspace-origin sessions can also be saved — ReadOnly only gates the in-place Save.
    }
}
