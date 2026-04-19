// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Xmla;

public sealed class XmlaConnectionStringBuilder
{
    public string Build(string workspaceUrl, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(workspaceUrl))
            throw new ArgumentException("workspaceUrl is required.", nameof(workspaceUrl));
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("databaseName is required.", nameof(databaseName));

        return $"Provider=MSOLAP;Data Source={workspaceUrl};Initial Catalog={databaseName};";
    }
}
