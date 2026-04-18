// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.RefreshPolicy;

public sealed class RefreshPolicyComparer
{
    public bool AreEqual(Microsoft.AnalysisServices.Tabular.RefreshPolicy? a,
                         Microsoft.AnalysisServices.Tabular.RefreshPolicy? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a is BasicRefreshPolicy ba && b is BasicRefreshPolicy bb)
        {
            return ba.RollingWindowGranularity == bb.RollingWindowGranularity
                && ba.RollingWindowPeriods    == bb.RollingWindowPeriods
                && ba.IncrementalGranularity  == bb.IncrementalGranularity
                && ba.IncrementalPeriods      == bb.IncrementalPeriods
                && ba.IncrementalPeriodsOffset == bb.IncrementalPeriodsOffset
                && string.Equals(ba.SourceExpression,  bb.SourceExpression,  StringComparison.Ordinal)
                && string.Equals(ba.PollingExpression, bb.PollingExpression, StringComparison.Ordinal)
                && ba.Mode == bb.Mode;
        }
        throw new NotSupportedException(
            $"Refresh policy comparison not implemented for type {a.GetType().FullName}. " +
            $"File an issue if Microsoft has shipped a new RefreshPolicy subclass.");
    }
}
