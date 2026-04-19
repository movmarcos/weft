// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class XmlaConnectionStringBuilder
{
    // Per Microsoft's canonical Power BI XMLA pattern, OAuth tokens go INTO the
    // connection string as Password=. The older Provider=MSOLAP + server.AccessToken
    // form works for SQL Server AS but is unreliable for powerbi:// endpoints.
    // See https://learn.microsoft.com/analysis-services/tom/tom-pbi-datasets

    public string Build(string workspaceUrl, string databaseName, AccessToken token)
    {
        if (string.IsNullOrWhiteSpace(workspaceUrl))
            throw new ArgumentException("workspaceUrl is required.", nameof(workspaceUrl));
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("databaseName is required.", nameof(databaseName));
        if (string.IsNullOrWhiteSpace(token.Value))
            throw new ArgumentException("access token is required.", nameof(token));

        return $"Data Source={workspaceUrl};Initial Catalog={databaseName};Password={token.Value};";
    }

    public string BuildServerOnly(string serverUrl, AccessToken token)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("serverUrl is required.", nameof(serverUrl));
        if (string.IsNullOrWhiteSpace(token.Value))
            throw new ArgumentException("access token is required.", nameof(token));

        return $"Data Source={serverUrl};Password={token.Value};";
    }
}
