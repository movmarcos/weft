// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App.Connections;

namespace WeftStudio.App.Tests;

public class ConnectionManagerTests
{
    [Fact]
    public async Task SignInAsync_delegates_to_IAuthProvider_and_returns_AccessToken()
    {
        var auth = Substitute.For<IAuthProvider>();
        var fake = new AccessToken("jwt-token-value", DateTimeOffset.UtcNow.AddHours(1));
        auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(fake);

        var factory = Substitute.For<Func<AuthOptions, IAuthProvider>>();
        factory(Arg.Any<AuthOptions>()).Returns(auth);

        var mgr = new ConnectionManager(factory, reader: Substitute.For<ITargetReader>());

        var opts = new AuthOptions(
            Mode: AuthMode.Interactive,
            TenantId: "",
            ClientId: "client-id");

        var token = await mgr.SignInAsync(opts, CancellationToken.None);

        token.Value.Should().Be("jwt-token-value");
    }

    [Fact]
    public async Task ListDatasetsAsync_returns_DatasetInfo_rows_from_reader()
    {
        var reader = Substitute.For<ITargetReader>();
        reader.ListDatabasesAsync(
                Arg.Any<string>(),
                Arg.Any<AccessToken>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { "DatasetA", "DatasetB" });

        var mgr = new ConnectionManager(_ => Substitute.For<IAuthProvider>(), reader);
        var ws = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/Test");
        var token = new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1));

        var datasets = await mgr.ListDatasetsAsync(ws, token, CancellationToken.None);

        datasets.Select(d => d.Name).Should().Equal("DatasetA", "DatasetB");
    }

    [Fact]
    public async Task FetchModelAsync_returns_ReadOnly_ModelSession()
    {
        var reader = Substitute.For<ITargetReader>();
        var fakeDb = OpenFixtureDatabase();
        reader.ReadAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(fakeDb);

        var mgr = new ConnectionManager(_ => Substitute.For<IAuthProvider>(), reader);
        var ws = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/Test");
        var ds = new DatasetInfo("Sales", null, null, null, null);
        var token = new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1));

        var session = await mgr.FetchModelAsync(ws, ds, token, CancellationToken.None);

        session.ReadOnly.Should().BeTrue();
        session.SourcePath.Should().BeNull();
        session.Database.Should().NotBeNull();
    }

    private static Microsoft.AnalysisServices.Tabular.Database OpenFixtureDatabase()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
        return new Weft.Core.Loading.BimFileLoader().Load(path);
    }
}
