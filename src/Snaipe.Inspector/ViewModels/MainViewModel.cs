using System.Collections.ObjectModel;
using System.IO;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private enum ConnectionState { Disconnected, Connecting, Connected }

    private readonly InspectorIpcClient _client = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private AgentInfo? _selectedAgent;
    private TreeNodeViewModel? _selectedNode;
    private string _statusMessage = "Ready";
    private bool _isLoadingProperties;
    private string _connectButtonLabel = "Connect";
    private CancellationTokenSource? _propertiesCts;

    public MainViewModel()
    {
        ConnectCommand = new AsyncRelayCommand(
            ConnectAsync,
            () => _selectedAgent is not null && _state == ConnectionState.Disconnected);

        DisconnectCommand = new RelayCommand(
            Disconnect,
            () => _state == ConnectionState.Connected);

        RefreshAgentsCommand = new RelayCommand(RefreshAgents);

        RefreshTreeCommand = new AsyncRelayCommand(
            FetchTreeAsync,
            () => _state == ConnectionState.Connected);

        DrillIntoCommand = new RelayCommand<PropertyRowViewModel>(row =>
        {
            if (row is null) return;
            var newPath = Breadcrumb.Count > 0
                ? [.. Breadcrumb.Last().Path, row.Entry.Name]
                : new[] { row.Entry.Name };
            Breadcrumb.Add(MakeCrumb(row.Entry.Name, newPath));
            if (_selectedNode is not null)
                _ = LoadPropertiesAsync(_selectedNode, newPath);
        });

        NavigateToBreadcrumbCommand = new RelayCommand<BreadcrumbSegment>(crumb =>
        {
            if (crumb is null) return;
            var idx = Breadcrumb.IndexOf(crumb);
            if (idx < 0) return;
            while (Breadcrumb.Count > idx + 1)
                Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
            if (_selectedNode is not null)
                _ = LoadPropertiesAsync(_selectedNode, crumb.Path);
        });

        RefreshAgents();
    }

    private BreadcrumbSegment MakeCrumb(string label, string[] path)
    {
        BreadcrumbSegment? crumb = null;
        var navigate = new RelayCommand(() => NavigateToBreadcrumbCommand.Execute(crumb));
        crumb = new BreadcrumbSegment(label, path, navigate);
        return crumb;
    }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<AgentInfo> DiscoveredAgents { get; } = [];
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = [];
    public PropertyGridViewModel PropertyGrid { get; } = new();
    public ObservableCollection<BreadcrumbSegment> Breadcrumb { get; } = [];

    // ── State properties ──────────────────────────────────────────────────────
    public bool IsConnected => _state == ConnectionState.Connected;

    public string ConnectButtonLabel { get => _connectButtonLabel; private set => SetField(ref _connectButtonLabel, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsLoadingProperties
    {
        get => _isLoadingProperties;
        private set => SetField(ref _isLoadingProperties, value);
    }

    public AgentInfo? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (SetField(ref _selectedAgent, value))
                ConnectCommand.RaiseCanExecuteChanged();
        }
    }

    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetField(ref _selectedNode, value))
                _ = OnSelectedNodeChangedAsync(value);
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public AsyncRelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand RefreshAgentsCommand { get; }
    public AsyncRelayCommand RefreshTreeCommand { get; }
    public RelayCommand<PropertyRowViewModel> DrillIntoCommand { get; }
    public RelayCommand<BreadcrumbSegment>   NavigateToBreadcrumbCommand { get; }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void RefreshCommandStates()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        RefreshTreeCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsConnected));
        ConnectButtonLabel = _state == ConnectionState.Connected ? "Connected" : "Connect";
    }

    private void ClearSession()
    {
        _propertiesCts?.Cancel();
        RootNodes.Clear();
        PropertyGrid.Clear();
        Breadcrumb.Clear();
        _selectedNode = null;
        OnPropertyChanged(nameof(SelectedNode));
    }

    private void HandleConnectionLost(string reason)
    {
        _client.Disconnect();
        _state = ConnectionState.Disconnected;
        StatusMessage = $"Connection lost — {reason}. Reconnect?";
        ClearSession();
        RefreshCommandStates();
    }

    private static HashSet<string> CollectExpandedIds(IEnumerable<TreeNodeViewModel> nodes)
    {
        var ids = new HashSet<string>();
        Collect(nodes, ids);
        return ids;

        static void Collect(IEnumerable<TreeNodeViewModel> items, HashSet<string> result)
        {
            foreach (var n in items)
            {
                if (n.IsExpanded) result.Add(n.Node.Id);
                Collect(n.Children, result);
            }
        }
    }

    private static TreeNodeViewModel BuildTreeNode(ElementNode node, HashSet<string> expandedIds)
    {
        var vm = new TreeNodeViewModel(node) { IsExpanded = true };
        foreach (var child in node.Children)
            vm.Children.Add(BuildTreeNode(child, expandedIds));
        return vm;
    }

    private static int CountNodes(ElementNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }

    // ── Command implementations (IPC calls added in Tasks 5 & 6) ─────────────
    private void RefreshAgents()
    {
        var agents = AgentDiscoveryScanner.Scan();
        DiscoveredAgents.Clear();
        foreach (var a in agents)
            DiscoveredAgents.Add(a);

        if (agents.Count == 0)
            StatusMessage = "No Snaipe agents found — is your app running with Snaipe.Agent?";
        else
            StatusMessage = $"Found {agents.Count} agent(s).";

        ConnectCommand.RaiseCanExecuteChanged();
    }

    private async Task ConnectAsync()
    {
        if (_selectedAgent is null) return;
        _state = ConnectionState.Connecting;
        StatusMessage = $"Connecting to {_selectedAgent.DisplayName}...";
        RefreshCommandStates();

        try
        {
            await _client.ConnectAsync(_selectedAgent.PipeName);
            _state = ConnectionState.Connected;
            StatusMessage = $"Connected to {_selectedAgent.DisplayName}";
            RefreshCommandStates();
            await FetchTreeAsync();
        }
        catch (Exception ex)
        {
            _state = ConnectionState.Disconnected;
            StatusMessage = $"Connection failed: {ex.Message}";
            RefreshCommandStates();
        }
    }
    private void Disconnect()
    {
        _client.Disconnect();
        _state = ConnectionState.Disconnected;
        StatusMessage = "Disconnected.";
        ClearSession();
        RefreshCommandStates();
    }
    private async Task FetchTreeAsync()
    {
        try
        {
            StatusMessage = "Fetching tree...";
            var expandedIds = CollectExpandedIds(RootNodes);

            var response = await _client.SendAsync<TreeResponse>(
                new GetTreeRequest { MessageId = Guid.NewGuid().ToString("N") });

            RootNodes.Clear();
            RootNodes.Add(BuildTreeNode(response.Root, expandedIds));
            StatusMessage = $"Tree loaded ({CountNodes(response.Root)} elements).";
        }
        catch (IOException ex)
        {
            HandleConnectionLost(ex.Message);
        }
        catch (SnaipeProtocolException ex)
        {
            StatusMessage = $"Tree error: {ex.Message}";
        }
    }
    private async Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node)
    {
        Breadcrumb.Clear();
        PropertyGrid.Clear();
        _propertiesCts?.Cancel();

        if (node is null || _state != ConnectionState.Connected) return;

        Breadcrumb.Add(MakeCrumb(node.Node.TypeName, []));
        await LoadPropertiesAsync(node, []);
    }

    private async Task LoadPropertiesAsync(TreeNodeViewModel node, string[] path)
    {
        _propertiesCts?.Cancel();
        _propertiesCts = new CancellationTokenSource();
        var ct = _propertiesCts.Token;

        PropertyGrid.Clear();
        IsLoadingProperties = true;
        try
        {
            var response = await _client.SendAsync<PropertiesResponse>(
                new GetPropertiesRequest
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ElementId = node.Node.Id,
                    PropertyPath = path.Length > 0 ? path : null,
                });

            if (ct.IsCancellationRequested) return;

            var capturedPath = path;
            var rows = response.Properties.Select(prop =>
            {
                PropertyRowViewModel? row = null;
                RelayCommand? drillCmd = prop.IsObjectValued
                    ? new RelayCommand(() => DrillIntoCommand.Execute(row))
                    : null;
                row = new PropertyRowViewModel(prop,
                    r => SetPropertyAsync(node.Node.Id, capturedPath, r.Entry.Name, r.EditValue, r),
                    drillCmd);
                return row;
            }).ToList();

            PropertyGrid.Load(rows);

            if (path.Length == 0)
                _ = SendHighlightAsync(node.Node.Id, show: true);
        }
        catch (OperationCanceledException)
        {
            // Superseded — do nothing.
        }
        catch (IOException ex)
        {
            HandleConnectionLost(ex.Message);
        }
        catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.ElementNotFound)
        {
            StatusMessage = path.Length > 0
                ? "Drill-down target no longer available — navigated back to root."
                : "Element no longer in tree — refresh the tree.";
            if (path.Length > 0)
            {
                Breadcrumb.Clear();
                if (_selectedNode is not null)
                    Breadcrumb.Add(MakeCrumb(_selectedNode.Node.TypeName, []));
            }
        }
        finally
        {
            IsLoadingProperties = false;
        }
    }

    public async Task SetPropertyAsync(string elementId, string[]? propertyPath,
        string propertyName, string newValue, PropertyRowViewModel row)
    {
        try
        {
            var ack = await _client.SendAsync<AckResponse>(
                new SetPropertyRequest
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ElementId = elementId,
                    PropertyPath = propertyPath?.Length > 0 ? propertyPath : null,
                    PropertyName = propertyName,
                    NewValue = newValue,
                });

            row.ClearError();
            if (ack.NormalizedValue != null)
                row.EditValue = ack.NormalizedValue;
        }
        catch (SnaipeProtocolException ex)
        {
            row.SetError(ex.Details ?? ex.Message);
        }
        catch (IOException ex)
        {
            HandleConnectionLost(ex.Message);
        }
        catch (Exception ex)
        {
            row.SetError(ex.Message);
        }
    }

    private async Task SendHighlightAsync(string elementId, bool show)
    {
        try
        {
            await _client.SendRawAsync(new HighlightElementRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = elementId,
                Show = show,
            });
        }
        catch
        {
            // Best effort — highlight failures are non-fatal.
        }
    }
}
