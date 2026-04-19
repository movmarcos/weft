// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using ReactiveUI;
using WeftStudio.App.Connections;

namespace WeftStudio.Ui.Connect;

public sealed class ConnectDialogViewModel : ReactiveObject
{
    private readonly IConnectionManager _mgr;
    private string _url = "";
    private string? _urlError;
    private ConnectDialogState _state = ConnectDialogState.Idle;
    private string? _errorBanner;
    private WorkspaceReference? _parsed;

    public ConnectDialogViewModel(IConnectionManager mgr) => _mgr = mgr;

    public string Url
    {
        get => _url;
        set
        {
            this.RaiseAndSetIfChanged(ref _url, value);
            TryParseUrl();
        }
    }

    public string? UrlError
    {
        get => _urlError;
        private set => this.RaiseAndSetIfChanged(ref _urlError, value);
    }

    public ConnectDialogState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string? ErrorBanner
    {
        get => _errorBanner;
        private set => this.RaiseAndSetIfChanged(ref _errorBanner, value);
    }

    public ObservableCollection<DatasetRow> Datasets { get; } = new();

    private void TryParseUrl()
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            UrlError = null;
            _parsed = null;
            State = ConnectDialogState.Idle;
            return;
        }

        try
        {
            _parsed = WorkspaceReference.Parse(_url);
            UrlError = null;
            State = ConnectDialogState.Ready;
        }
        catch (WorkspaceUrlException ex)
        {
            _parsed = null;
            UrlError = ex.Message;
            State = ConnectDialogState.Idle;
        }
    }
}
