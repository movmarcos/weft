// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;

namespace Weft.Core;

public sealed record PlanResult(ChangeSet ChangeSet, string TmslJson);

public static class WeftCore
{
    public static PlanResult Plan(Database source, Database target)
    {
        var changeSet = new ModelDiffer().Compute(source, target);
        var tmsl      = new TmslBuilder().Build(changeSet, source, target);
        return new PlanResult(changeSet, tmsl);
    }
}
