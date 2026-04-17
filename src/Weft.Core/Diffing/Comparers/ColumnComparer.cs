// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing.Comparers;

public sealed record ColumnDiffResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Modified);

public sealed class ColumnComparer
{
    public ColumnDiffResult Compare(ColumnCollection source, ColumnCollection target)
    {
        var srcByName = source.OfType<Column>().ToDictionary(c => c.Name, StringComparer.Ordinal);
        var tgtByName = target.OfType<Column>().ToDictionary(c => c.Name, StringComparer.Ordinal);

        var added    = srcByName.Keys.Except(tgtByName.Keys).OrderBy(x => x).ToList();
        var removed  = tgtByName.Keys.Except(srcByName.Keys).OrderBy(x => x).ToList();
        var modified = srcByName.Keys
            .Intersect(tgtByName.Keys)
            .Where(name => !ColumnsEqual(srcByName[name], tgtByName[name]))
            .OrderBy(x => x)
            .ToList();

        return new ColumnDiffResult(added, removed, modified);
    }

    private static bool ColumnsEqual(Column a, Column b) =>
        a.DataType == b.DataType
        && string.Equals(a is DataColumn da ? da.SourceColumn : null,
                          b is DataColumn db ? db.SourceColumn : null, StringComparison.Ordinal)
        && string.Equals((a as CalculatedColumn)?.Expression,
                          (b as CalculatedColumn)?.Expression, StringComparison.Ordinal)
        && a.IsHidden == b.IsHidden
#pragma warning disable CS0618
        && a.IsKey == b.IsKey;
#pragma warning restore CS0618
}
