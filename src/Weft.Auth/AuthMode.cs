// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Auth;

public enum AuthMode
{
    ServicePrincipalSecret,
    ServicePrincipalCertFile,
    ServicePrincipalCertStore,
    Interactive,
    DeviceCode
}
