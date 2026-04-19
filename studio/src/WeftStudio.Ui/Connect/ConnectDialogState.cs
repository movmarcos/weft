// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.Ui.Connect;

public enum ConnectDialogState
{
    Idle,          // empty or invalid URL
    Ready,         // URL parses; button enabled
    SigningIn,     // MSAL token request in flight
    Fetching,      // listing datasets
    Picker,        // dataset grid visible, waiting for user selection
    Loading,       // downloading the selected model
}
