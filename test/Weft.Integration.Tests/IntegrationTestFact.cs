// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Weft.Integration.Tests;

public sealed class IntegrationTestFactAttribute : FactAttribute
{
    public IntegrationTestFactAttribute()
    {
        var required = new[]
        {
            "WEFT_INT_WORKSPACE",
            "WEFT_INT_DATABASE",
            "WEFT_INT_TENANT_ID",
            "WEFT_INT_CLIENT_ID",
            "WEFT_INT_CLIENT_SECRET"
        };
        var missing = required.Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))).ToList();
        if (missing.Count > 0)
            Skip = $"Integration tests skipped — missing env: {string.Join(", ", missing)}";
    }
}
