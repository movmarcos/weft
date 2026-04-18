// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int Generic = 1;
    public const int ConfigError = 2;
    public const int AuthError = 3;
    public const int SourceLoadError = 4;
    public const int TargetReadError = 5;
    public const int DiffValidationError = 6;
    public const int TmslExecutionError = 7;
    public const int RefreshError = 8;
    public const int PartitionIntegrityError = 9;
    public const int ParameterError = 10;
}
