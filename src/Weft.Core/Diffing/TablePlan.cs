// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing;

public sealed record TablePlan(
    string Name,
    TableClassification Classification,
    Table SourceTable);
