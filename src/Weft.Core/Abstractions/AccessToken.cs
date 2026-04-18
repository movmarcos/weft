// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresOnUtc);
