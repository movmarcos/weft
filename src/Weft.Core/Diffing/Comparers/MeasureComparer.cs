// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing.Comparers;

public sealed record MeasureDiffResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Modified);

public sealed class MeasureComparer
{
    public MeasureDiffResult Compare(MeasureCollection source, MeasureCollection target)
    {
        var srcByName = source.OfType<Measure>().ToDictionary(m => m.Name, StringComparer.Ordinal);
        var tgtByName = target.OfType<Measure>().ToDictionary(m => m.Name, StringComparer.Ordinal);

        var added    = srcByName.Keys.Except(tgtByName.Keys).OrderBy(x => x).ToList();
        var removed  = tgtByName.Keys.Except(srcByName.Keys).OrderBy(x => x).ToList();
        var modified = srcByName.Keys
            .Intersect(tgtByName.Keys)
            .Where(n => !MeasuresEqual(srcByName[n], tgtByName[n]))
            .OrderBy(x => x)
            .ToList();

        return new MeasureDiffResult(added, removed, modified);
    }

    private static bool MeasuresEqual(Measure a, Measure b) =>
        string.Equals(a.Expression, b.Expression, StringComparison.Ordinal)
        && a.IsHidden == b.IsHidden
        && string.Equals(a.FormatString, b.FormatString, StringComparison.Ordinal)
        && string.Equals(a.DisplayFolder, b.DisplayFolder, StringComparison.Ordinal);
}
