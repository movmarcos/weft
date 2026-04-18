// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Loading;

public static class ModelLoaderFactory
{
    public static IModelLoader For(string path)
    {
        if (Directory.Exists(path)) return new TabularEditorFolderLoader();
        if (File.Exists(path) && path.EndsWith(".bim", StringComparison.OrdinalIgnoreCase))
            return new BimFileLoader();
        throw new FileNotFoundException($"Cannot resolve a model loader for path: {path}", path);
    }
}
