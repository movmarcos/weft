// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using VerifyTests;

namespace Weft.Core.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.UseStrictJson();
        Verifier.DerivePathInfo((sourceFile, projectDir, type, method) =>
        {
            var snapshotDir = Path.Combine(projectDir, "Snapshots");
            Directory.CreateDirectory(snapshotDir);
            return new PathInfo(snapshotDir, type.Name, method.Name);
        });
    }
}
