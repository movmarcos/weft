// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class TargetReader : ITargetReader
{
    public Task<Database> ReadAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        CancellationToken cancellationToken = default)
    {
        using var server = new ServerConnectionFactory().Connect(workspaceUrl, databaseName, token);
        var sourceDb = server.Databases.FindByName(databaseName)
            ?? throw new InvalidOperationException(
                $"Database '{databaseName}' not found on {workspaceUrl}.");

        var serialized = JsonSerializer.SerializeDatabase(sourceDb, new SerializeOptions
        {
            IgnoreTimestamps = true,
            IgnoreInferredObjects = true,
            IgnoreInferredProperties = true
        });
        var detached = JsonSerializer.DeserializeDatabase(serialized);
        return Task.FromResult(detached);
    }

    public Task<IReadOnlyList<string>> ListDatabasesAsync(
        string serverUrl,
        AccessToken token,
        CancellationToken ct)
    {
        using var server = new Server();
        var conn = new XmlaConnectionStringBuilder().BuildServerOnly(serverUrl, token);
        server.Connect(conn);

        var names = new List<string>();
        foreach (Database db in server.Databases)
        {
            ct.ThrowIfCancellationRequested();
            names.Add(db.Name);
        }

        return Task.FromResult<IReadOnlyList<string>>(names);
    }
}
