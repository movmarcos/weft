// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using NSubstitute;
using Weft.Core.Abstractions;

namespace Weft.Cli.Tests.Helpers;

public static class CliTestHost
{
    public static IAuthProvider MakeAuth(string token = "fake-token")
    {
        var a = Substitute.For<IAuthProvider>();
        a.GetTokenAsync(default).ReturnsForAnyArgs(
            Task.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1))));
        return a;
    }

    public static ITargetReader StubTarget(Database db)
    {
        var r = Substitute.For<ITargetReader>();
        r.ReadAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromResult(db));
        return r;
    }

    public static IXmlaExecutor MakeExecutor(bool success = true)
    {
        var ex = Substitute.For<IXmlaExecutor>();
        ex.ExecuteAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(new XmlaExecutionResult(success, Array.Empty<string>(), TimeSpan.Zero)));
        return ex;
    }

    public static IRefreshRunner MakeRefreshRunner(bool success = true)
    {
        var r = Substitute.For<IRefreshRunner>();
        r.RefreshAsync(default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult(new XmlaExecutionResult(success, Array.Empty<string>(), TimeSpan.Zero)));
        return r;
    }
}
