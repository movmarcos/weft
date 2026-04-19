// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class ServerConnectionFactory
{
    public Server Connect(string workspaceUrl, string databaseName, AccessToken token)
    {
        var server = new Server();
        var conn = new XmlaConnectionStringBuilder().Build(workspaceUrl, databaseName, token);
        server.Connect(conn);
        return server;
    }
}
