// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Weft.Auth;

public static class CertificateLoader
{
    public static X509Certificate2 LoadFromFile(string pfxPath, string password)
    {
        if (!File.Exists(pfxPath))
            throw new FileNotFoundException($"Certificate file not found: {pfxPath}", pfxPath);

        // .NET 10: prefer X509CertificateLoader over deprecated X509Certificate2 ctor.
        return X509CertificateLoader.LoadPkcs12FromFile(
            pfxPath,
            password,
            X509KeyStorageFlags.Exportable);
    }

    public static X509Certificate2 LoadFromStore(
        string thumbprint,
        StoreLocation location = StoreLocation.LocalMachine,
        StoreName storeName = StoreName.My)
    {
        using var store = new X509Store(storeName, location);
        store.Open(OpenFlags.ReadOnly);
        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);
        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"Certificate with thumbprint '{thumbprint}' not found in {location}/{storeName}.");
        return matches[0];
    }
}
