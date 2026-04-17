// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Loading;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class ModelLoaderFactoryTests
{
    [Fact]
    public void Picks_BimFileLoader_for_bim_paths()
    {
        // Plan says we just inspect the path; even a non-existent .bim file path should pick BimFileLoader
        // BUT the spec also says throws for unknown paths. So the test needs a real .bim path. Use a temp file.
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".bim");
        File.WriteAllText(tmp, "{}");
        try
        {
            var loader = ModelLoaderFactory.For(tmp);
            loader.Should().BeOfType<BimFileLoader>();
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void Picks_TabularEditorFolderLoader_for_directories()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var loader = ModelLoaderFactory.For(dir);
            loader.Should().BeOfType<TabularEditorFolderLoader>();
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void Throws_for_unknown_path()
    {
        var act = () => ModelLoaderFactory.For("/no/such/thing.xyz");
        act.Should().Throw<FileNotFoundException>();
    }
}
