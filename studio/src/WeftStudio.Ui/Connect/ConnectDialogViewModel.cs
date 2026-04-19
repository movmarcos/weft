// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App;
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
    private string _clientId = "";
    private AuthMode _authMode = AuthMode.Interactive;
    private readonly ObservableAsPropertyHelper<bool> _canSignIn;

    public ConnectDialogViewModel(IConnectionManager mgr)
    {
        _mgr = mgr;

        _canSignIn = this
            .WhenAnyValue(x => x.Url, x => x.ClientId, x => x.State,
                (url, cid, state) =>
                    !string.IsNullOrWhiteSpace(url) &&
                    !string.IsNullOrWhiteSpace(cid) &&
                    state != ConnectDialogState.SigningIn &&
                    state != ConnectDialogState.Fetching &&
                    state != ConnectDialogState.Loading)
            .ToProperty(this, x => x.CanSignIn);
    }

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

    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            RefreshVisibleDatasets();
        }
    }

    public ObservableCollection<DatasetRow> VisibleDatasets { get; } = new();

    private void RefreshVisibleDatasets()
    {
        VisibleDatasets.Clear();
        var filter = _filterText;
        foreach (var row in Datasets)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                VisibleDatasets.Add(row);
            }
        }
    }

    public string ClientId
    {
        get => _clientId;
        set => this.RaiseAndSetIfChanged(ref _clientId, value);
    }

    public AuthMode AuthMode
    {
        get => _authMode;
        set => this.RaiseAndSetIfChanged(ref _authMode, value);
    }

    public bool CanSignIn => _canSignIn.Value;

    public DatasetRow? SelectedRow
    {
        get => _selectedRow;
        set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
    }
    private DatasetRow? _selectedRow;

    private AccessToken? _token;

    public async Task SignInAsync()
    {
        if (_parsed is null || string.IsNullOrWhiteSpace(ClientId)) return;

        ErrorBanner = null;
        State = ConnectDialogState.SigningIn;

        var opts = new AuthOptions(
            Mode: AuthMode,
            TenantId: "",
            ClientId: ClientId);

        try
        {
            _token = await _mgr.SignInAsync(opts, CancellationToken.None);

            State = ConnectDialogState.Fetching;
            var datasets = await _mgr.ListDatasetsAsync(_parsed, _token, CancellationToken.None);

            Datasets.Clear();
            foreach (var d in datasets) Datasets.Add(new DatasetRow(d));

            State = ConnectDialogState.Picker;
            RefreshVisibleDatasets();
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
            State = ConnectDialogState.Ready;
            _token = null;
        }
    }

    public async Task<ModelSession?> OpenAsync()
    {
        if (_parsed is null || _token is null || SelectedRow is null) return null;

        State = ConnectDialogState.Loading;
        try
        {
            return await _mgr.FetchModelAsync(_parsed, SelectedRow.Info, _token, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
            State = ConnectDialogState.Picker;
            return null;
        }
    }

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
