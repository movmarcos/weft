// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class CertificateLoaderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Loads_pfx_file_with_password()
    {
        var pfx = FixturePath("test-cert.pfx");
        var pwd = File.ReadAllText(FixturePath("test-cert.password.txt")).TrimEnd();

        var cert = CertificateLoader.LoadFromFile(pfx, pwd);

        cert.Should().NotBeNull();
        cert.Subject.Should().Contain("CN=weft-test");
        cert.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void Throws_on_missing_pfx()
    {
        var act = () => CertificateLoader.LoadFromFile("/no/such/cert.pfx", "x");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Throws_on_wrong_password()
    {
        var pfx = FixturePath("test-cert.pfx");
        var act = () => CertificateLoader.LoadFromFile(pfx, "wrong-password");
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
