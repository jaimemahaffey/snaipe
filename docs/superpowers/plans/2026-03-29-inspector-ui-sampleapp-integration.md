# Snaipe Inspector UI, SampleApp Enrichment & Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `Snaipe.Inspector` from code-behind to XAML + full MVVM, enrich `Snaipe.SampleApp` with a diverse control set, and verify the full connect→tree→properties→edit→disconnect loop works end-to-end.

**Architecture:** Four `UserControl`s (ConnectionBar, ElementTree, PropertyGrid, PreviewPane) share one `MainViewModel` via inherited `DataContext`. ViewModels own all session state; XAML binds via `x:Bind` using a typed `ViewModel` property on each UserControl code-behind. The existing `InspectorIpcClient` and `AgentDiscoveryScanner` are unchanged — `MainViewModel` composes them.

**Tech Stack:** .NET 9, Uno Platform 6.5 (Skia/Win32), WinUI 3 XAML, xUnit 2.x (ViewModel tests), `System.IO.Pipes`, `System.Text.Json`

---

## File Map

**Create:**
- `src/Snaipe.Inspector/ViewModels/ViewModelBase.cs`
- `src/Snaipe.Inspector/ViewModels/RelayCommand.cs`
- `src/Snaipe.Inspector/ViewModels/AsyncRelayCommand.cs`
- `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs`
- `src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs`
- `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`
- `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`
- `src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml`
- `src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs`
- `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml`
- `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs`
- `src/Snaipe.Inspector/Controls/PropertyEditorTemplateSelector.cs`
- `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`
- `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs`
- `src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml`
- `src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml.cs`
- `src/Snaipe.Inspector/MainWindow.xaml`
- `src/Snaipe.Inspector/MainWindow.xaml.cs`
- `samples/Snaipe.SampleApp/SampleViewModel.cs`
- `samples/Snaipe.SampleApp/MainWindow.xaml`
- `samples/Snaipe.SampleApp/MainWindow.xaml.cs`
- `tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj`
- `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs`
- `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`

**Delete:**
- `src/Snaipe.Inspector/MainWindow.cs`
- `samples/Snaipe.SampleApp/MainWindow.cs`

**Modify:**
- `Snaipe.sln` (add test project)

---

## Task 1: ViewModelBase, RelayCommand, AsyncRelayCommand, and test project

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/ViewModelBase.cs`
- Create: `src/Snaipe.Inspector/ViewModels/RelayCommand.cs`
- Create: `src/Snaipe.Inspector/ViewModels/AsyncRelayCommand.cs`
- Create: `tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj`

- [ ] **Step 1: Create the test project**

```xml
<!-- tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Snaipe.Inspector\Snaipe.Inspector.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create ViewModelBase**

```csharp
// src/Snaipe.Inspector/ViewModels/ViewModelBase.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Snaipe.Inspector.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 3: Create RelayCommand**

```csharp
// src/Snaipe.Inspector/ViewModels/RelayCommand.cs
using System.Windows.Input;

namespace Snaipe.Inspector.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: Create AsyncRelayCommand**

```csharp
// src/Snaipe.Inspector/ViewModels/AsyncRelayCommand.cs
using System.Windows.Input;

namespace Snaipe.Inspector.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try { await _execute(); }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 5: Add test project to solution**

```bash
cd C:\Users\mahaffey\snaipe
dotnet sln add tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/ tests/
git commit -m "feat: add ViewModelBase, RelayCommand, AsyncRelayCommand, and test project"
```

---

## Task 2: TreeNodeViewModel

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs`
- Create: `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs
using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using System.ComponentModel;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class TreeNodeViewModelTests
{
    private static ElementNode MakeNode(string id, string typeName, string? name = null,
        List<ElementNode>? children = null) => new()
    {
        Id = id, TypeName = typeName, Name = name,
        Children = children ?? [],
    };

    [Fact]
    public void DisplayName_WithName_IncludesQuotedName()
    {
        var node = MakeNode("1", "Button", "SubmitBtn");
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("Button \"SubmitBtn\"", vm.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutName_ShowsTypeOnly()
    {
        var node = MakeNode("1", "StackPanel");
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("StackPanel", vm.DisplayName);
    }

    [Fact]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Grid"));
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsExpanded = true;

        Assert.Contains(nameof(TreeNodeViewModel.IsExpanded), raised);
    }

    [Fact]
    public void Children_AreEmptyByDefault()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Border"));
        Assert.Empty(vm.Children);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Snaipe.Inspector.Tests/ --filter "FullyQualifiedName~TreeNodeViewModelTests"
```

Expected: Build error — `TreeNodeViewModel` not found.

- [ ] **Step 3: Implement TreeNodeViewModel**

```csharp
// src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs
using System.Collections.ObjectModel;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class TreeNodeViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public TreeNodeViewModel(ElementNode node)
    {
        Node = node;
        DisplayName = string.IsNullOrEmpty(node.Name)
            ? node.TypeName
            : $"{node.TypeName} \"{node.Name}\"";
    }

    public ElementNode Node { get; }
    public string DisplayName { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/ --filter "FullyQualifiedName~TreeNodeViewModelTests"
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs
git commit -m "feat: add TreeNodeViewModel with DisplayName and expansion state"
```

---

## Task 3: PropertyGroupViewModel and PropertyRowViewModel

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs`
- Create: `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`

> Note: `PropertyRowViewModel.CommitEditCommand` calls back into `MainViewModel.SetPropertyAsync` — that method is added in Task 6. For now the command is declared with a no-op body and wired up in Task 6.

- [ ] **Step 1: Implement PropertyGroupViewModel**

```csharp
// src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs
using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyGroupViewModel
{
    public PropertyGroupViewModel(string category)
    {
        Category = category;
    }

    public string Category { get; }
    public ObservableCollection<PropertyRowViewModel> Properties { get; } = [];
}
```

- [ ] **Step 2: Implement PropertyRowViewModel**

```csharp
// src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs
using System.Windows.Input;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyRowViewModel : ViewModelBase
{
    private string _editValue;
    private bool _hasError;
    private string? _errorMessage;

    // _owner is set in Task 6 after MainViewModel exists.
    // Declared as nullable; CommitEditCommand is a no-op until wired.
    private readonly Func<PropertyRowViewModel, Task>? _commit;

    public PropertyRowViewModel(PropertyEntry entry, Func<PropertyRowViewModel, Task>? commit = null)
    {
        Entry = entry;
        _editValue = entry.Value ?? string.Empty;
        _commit = commit;
        CommitEditCommand = new AsyncRelayCommand(
            () => _commit?.Invoke(this) ?? Task.CompletedTask,
            () => !Entry.IsReadOnly);
    }

    public PropertyEntry Entry { get; }

    public string EditValue
    {
        get => _editValue;
        set => SetField(ref _editValue, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetField(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public AsyncRelayCommand CommitEditCommand { get; }

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs
git commit -m "feat: add PropertyGroupViewModel and PropertyRowViewModel"
```

---

## Task 4: MainViewModel — connection state and collections

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` (partial — state, collections, helper methods)
- Create: `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write failing tests for initial state**

```csharp
// tests/Snaipe.Inspector.Tests/MainViewModelTests.cs
using Snaipe.Inspector.ViewModels;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class MainViewModelTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void InitialState_RootNodesIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void InitialState_PropertyGroupsIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.PropertyGroups);
    }

    [Fact]
    public void InitialState_StatusMessageIsReady()
    {
        var vm = new MainViewModel();
        Assert.Equal("Ready", vm.StatusMessage);
    }

    [Fact]
    public void SelectedAgent_Null_ConnectCommandCannotExecute()
    {
        var vm = new MainViewModel();
        Assert.Null(vm.SelectedAgent);
        Assert.False(vm.ConnectCommand.CanExecute(null));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test tests/Snaipe.Inspector.Tests/ --filter "FullyQualifiedName~MainViewModelTests"
```

Expected: Build error — `MainViewModel` not found.

- [ ] **Step 3: Implement MainViewModel (state + helpers, no IPC calls yet)**

```csharp
// src/Snaipe.Inspector/ViewModels/MainViewModel.cs
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

        RefreshAgents();
    }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<AgentInfo> DiscoveredAgents { get; } = [];
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = [];
    public ObservableCollection<PropertyGroupViewModel> PropertyGroups { get; } = [];

    // ── State properties ──────────────────────────────────────────────────────
    public bool IsConnected => _state == ConnectionState.Connected;

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
                (ConnectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void RefreshCommandStates()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        RefreshTreeCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsConnected));
    }

    private void ClearSession()
    {
        RootNodes.Clear();
        PropertyGroups.Clear();
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

    private static int CategoryOrder(string category) => category switch
    {
        "Common" => 0, "Layout" => 1, "Appearance" => 2,
        "Data Context" => 3, "Visual States" => 4,
        "Style" => 5, "Template" => 6, _ => 7,
    };

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

        (ConnectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private Task ConnectAsync() => Task.CompletedTask;   // wired in Task 5
    private void Disconnect() { }                         // wired in Task 5
    private Task FetchTreeAsync() => Task.CompletedTask; // wired in Task 5
    private Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node) => Task.CompletedTask; // wired in Task 6
    public Task SetPropertyAsync(string elementId, string propertyName, string newValue,
        PropertyRowViewModel row) => Task.CompletedTask; // wired in Task 6
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/ --filter "FullyQualifiedName~MainViewModelTests"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs tests/Snaipe.Inspector.Tests/MainViewModelTests.cs
git commit -m "feat: add MainViewModel with connection state machine and collections"
```

---

## Task 5: MainViewModel — Connect, Disconnect, and FetchTree

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`

Replace the three stub methods (`ConnectAsync`, `Disconnect`, `FetchTreeAsync`) with real implementations.

- [ ] **Step 1: Replace `ConnectAsync`**

Find and replace the stub:
```csharp
private Task ConnectAsync() => Task.CompletedTask;   // wired in Task 5
```

With:
```csharp
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
```

- [ ] **Step 2: Replace `Disconnect`**

Find and replace:
```csharp
private void Disconnect() { }                         // wired in Task 5
```

With:
```csharp
private void Disconnect()
{
    _client.Disconnect();
    _state = ConnectionState.Disconnected;
    StatusMessage = "Disconnected.";
    ClearSession();
    RefreshCommandStates();
}
```

- [ ] **Step 3: Replace `FetchTreeAsync`**

Find and replace:
```csharp
private Task FetchTreeAsync() => Task.CompletedTask; // wired in Task 5
```

With:
```csharp
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
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs
git commit -m "feat: wire MainViewModel Connect/Disconnect/FetchTree to IPC client"
```

---

## Task 6: MainViewModel — OnSelectedNodeChanged, SetProperty, Highlight

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`
- Modify: `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs` (wire commit callback)

- [ ] **Step 1: Replace `OnSelectedNodeChangedAsync`**

Find and replace:
```csharp
private Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node) => Task.CompletedTask; // wired in Task 6
```

With:
```csharp
private async Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node)
{
    PropertyGroups.Clear();
    if (node is null || _state != ConnectionState.Connected) return;

    IsLoadingProperties = true;
    try
    {
        var response = await _client.SendAsync<PropertiesResponse>(
            new GetPropertiesRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = node.Node.Id,
            });

        var orderedGroups = response.Properties
            .GroupBy(p => p.Category)
            .OrderBy(g => CategoryOrder(g.Key));

        foreach (var group in orderedGroups)
        {
            var groupVm = new PropertyGroupViewModel(group.Key);
            foreach (var prop in group.OrderBy(p => p.Name))
                groupVm.Properties.Add(new PropertyRowViewModel(prop,
                    row => SetPropertyAsync(node.Node.Id, row.Entry.Name, row.EditValue, row)));
            PropertyGroups.Add(groupVm);
        }

        // Fire-and-forget highlight — best effort, never crashes the Inspector.
        _ = SendHighlightAsync(node.Node.Id, show: true);
    }
    catch (IOException ex)
    {
        HandleConnectionLost(ex.Message);
    }
    catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.ElementNotFound)
    {
        StatusMessage = "Element no longer in tree — refresh the tree.";
    }
    finally
    {
        IsLoadingProperties = false;
    }
}
```

- [ ] **Step 2: Replace `SetPropertyAsync`**

Find and replace:
```csharp
public Task SetPropertyAsync(string elementId, string propertyName, string newValue,
    PropertyRowViewModel row) => Task.CompletedTask; // wired in Task 6
```

With:
```csharp
public async Task SetPropertyAsync(string elementId, string propertyName, string newValue,
    PropertyRowViewModel row)
{
    try
    {
        var response = await _client.SendAsync<PropertiesResponse>(
            new SetPropertyRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = elementId,
                PropertyName = propertyName,
                NewValue = newValue,
            });

        row.ClearError();
        // Refresh the row's displayed value with what the agent confirmed.
        var updated = response.Properties.FirstOrDefault(p => p.Name == propertyName);
        if (updated is not null)
            row.EditValue = updated.Value ?? string.Empty;
    }
    catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.PropertyReadOnly)
    {
        row.SetError("Property is read-only.");
    }
    catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.InvalidPropertyValue)
    {
        row.SetError($"Cannot parse value: {ex.Details ?? ex.Message}");
    }
    catch (SnaipeProtocolException ex)
    {
        row.SetError(ex.Message);
    }
    catch (IOException ex)
    {
        HandleConnectionLost(ex.Message);
    }
}
```

- [ ] **Step 3: Add `SendHighlightAsync` helper**

Add this private method to `MainViewModel` (anywhere after the other private helpers):

```csharp
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
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs
git commit -m "feat: wire MainViewModel property fetch, set, and highlight to IPC client"
```

---

## Task 7: MainWindow.xaml shell (replace MainWindow.cs)

**Files:**
- Create: `src/Snaipe.Inspector/MainWindow.xaml`
- Create: `src/Snaipe.Inspector/MainWindow.xaml.cs`
- Delete: `src/Snaipe.Inspector/MainWindow.cs`

- [ ] **Step 1: Create MainWindow.xaml**

```xml
<!-- src/Snaipe.Inspector/MainWindow.xaml -->
<Window
    x:Class="Snaipe.Inspector.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Snaipe.Inspector.Controls">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Connection bar -->
        <controls:ConnectionBarControl Grid.Row="0"/>

        <!-- Main split: tree | property+preview -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="300" MinWidth="150"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*" MinWidth="200"/>
            </Grid.ColumnDefinitions>

            <controls:ElementTreeControl Grid.Column="0"/>

            <GridSplitter Grid.Column="1"
                          Width="4"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Stretch"
                          ResizeDirection="Columns"/>

            <!-- Right pane: property grid over preview -->
            <Grid Grid.Column="2">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="160" MinHeight="80"/>
                </Grid.RowDefinitions>

                <controls:PropertyGridControl Grid.Row="0"/>

                <GridSplitter Grid.Row="1"
                              Height="4"
                              HorizontalAlignment="Stretch"
                              VerticalAlignment="Center"
                              ResizeDirection="Rows"/>

                <controls:PreviewPaneControl Grid.Row="2"/>
            </Grid>
        </Grid>

        <!-- Status bar -->
        <Border Grid.Row="2" Padding="8,4">
            <TextBlock Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
                       FontSize="12"
                       TextTrimming="CharacterEllipsis"/>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Create MainWindow.xaml.cs**

```csharp
// src/Snaipe.Inspector/MainWindow.xaml.cs
using Microsoft.UI.Xaml;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    // Typed accessor for x:Bind in this file.
    public MainViewModel ViewModel => _viewModel;
}
```

- [ ] **Step 3: Delete old MainWindow.cs**

```bash
rm src/Snaipe.Inspector/MainWindow.cs
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded. (The four UserControls don't exist yet — add temporary empty stubs in the next step if the build fails due to missing control types.)

If `controls:ConnectionBarControl` etc. cause build errors, create empty stub files for each:

```csharp
// src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs  (stub)
using Microsoft.UI.Xaml.Controls;
namespace Snaipe.Inspector.Controls;
public sealed partial class ConnectionBarControl : UserControl
{
    public ConnectionBarControl() { }
}
```

(And a matching minimal `.xaml` for each stub — full implementations follow in Tasks 8–12.)

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/MainWindow.xaml src/Snaipe.Inspector/MainWindow.xaml.cs
git rm src/Snaipe.Inspector/MainWindow.cs
git commit -m "feat: replace code-behind MainWindow with XAML shell wired to MainViewModel"
```

---

## Task 8: ConnectionBarControl

**Files:**
- Create: `src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml`
- Create: `src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs`

- [ ] **Step 1: Create ConnectionBarControl.xaml**

```xml
<!-- src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml -->
<UserControl
    x:Class="Snaipe.Inspector.Controls.ConnectionBarControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <StackPanel Orientation="Horizontal"
                Padding="8"
                Spacing="8">

        <ComboBox x:Name="AgentDropdown"
                  MinWidth="320"
                  PlaceholderText="Select an agent..."
                  ItemsSource="{x:Bind ViewModel.DiscoveredAgents, Mode=OneWay}"
                  SelectedItem="{x:Bind ViewModel.SelectedAgent, Mode=TwoWay}"
                  DisplayMemberPath="DisplayName"/>

        <Button Content="⟳"
                ToolTipService.ToolTip="Refresh agent list"
                Command="{x:Bind ViewModel.RefreshAgentsCommand}"/>

        <Button x:Name="ConnectButton"
                Content="{x:Bind ConnectButtonLabel, Mode=OneWay}"
                Command="{x:Bind ViewModel.ConnectCommand}"/>

        <Button Content="Disconnect"
                IsEnabled="{x:Bind ViewModel.IsConnected, Mode=OneWay}"
                Command="{x:Bind ViewModel.DisconnectCommand}"/>

        <ProgressRing Width="20" Height="20"
                      IsActive="{x:Bind ViewModel.IsLoadingProperties, Mode=OneWay}"/>

    </StackPanel>
</UserControl>
```

- [ ] **Step 2: Create ConnectionBarControl.xaml.cs**

```csharp
// src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ConnectionBarControl : UserControl
{
    public ConnectionBarControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    // Switches label based on connection state for the Connect button.
    public string ConnectButtonLabel =>
        ViewModel?.IsConnected == true ? "Connected" : "Connect";
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs
git commit -m "feat: add ConnectionBarControl with agent dropdown and connect/disconnect buttons"
```

---

## Task 9: ElementTreeControl

**Files:**
- Create: `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml`
- Create: `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs`

- [ ] **Step 1: Create ElementTreeControl.xaml**

```xml
<!-- src/Snaipe.Inspector/Controls/ElementTreeControl.xaml -->
<UserControl
    x:Class="Snaipe.Inspector.Controls.ElementTreeControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:Snaipe.Inspector.ViewModels">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <Button Grid.Row="0"
                Content="Refresh Tree"
                Margin="4"
                Command="{x:Bind ViewModel.RefreshTreeCommand}"/>

        <TreeView x:Name="ElementTree"
                  Grid.Row="1"
                  ItemsSource="{x:Bind ViewModel.RootNodes, Mode=OneWay}"
                  SelectionMode="Single">
            <TreeView.ItemTemplate>
                <HierarchicalDataTemplate ItemsSource="{Binding Children}"
                                          x:DataType="vm:TreeNodeViewModel">
                    <TextBlock Text="{x:Bind DisplayName}"
                               FontSize="12"/>
                </HierarchicalDataTemplate>
            </TreeView.ItemTemplate>
        </TreeView>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create ElementTreeControl.xaml.cs**

```csharp
// src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ElementTreeControl : UserControl
{
    public ElementTreeControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
        ElementTree.ItemInvoked += OnItemInvoked;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeNodeViewModel node)
            ViewModel.SelectedNode = node;
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/Controls/ElementTreeControl.xaml src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs
git commit -m "feat: add ElementTreeControl with hierarchical TreeView bound to RootNodes"
```

---

## Task 10: PropertyEditorTemplateSelector

**Files:**
- Create: `src/Snaipe.Inspector/Controls/PropertyEditorTemplateSelector.cs`

- [ ] **Step 1: Create PropertyEditorTemplateSelector**

```csharp
// src/Snaipe.Inspector/Controls/PropertyEditorTemplateSelector.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

/// <summary>
/// Selects the right DataTemplate for a PropertyRowViewModel based on ValueKind and IsReadOnly.
/// Templates are set from PropertyGridControl.xaml resources.
/// </summary>
public sealed class PropertyEditorTemplateSelector : DataTemplateSelector
{
    // Set in XAML resource
    public DataTemplate? ReadOnlyTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? BooleanTemplate { get; set; }
    public DataTemplate? NumberTemplate { get; set; }
    public DataTemplate? EnumTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is not PropertyRowViewModel row) return TextTemplate;
        if (row.Entry.IsReadOnly) return ReadOnlyTemplate ?? TextTemplate;

        return row.Entry.ValueKind switch
        {
            "Boolean" => BooleanTemplate ?? TextTemplate,
            "Number" => NumberTemplate ?? TextTemplate,
            "Enum" when row.Entry.EnumValues?.Count > 0 => EnumTemplate ?? TextTemplate,
            _ => TextTemplate,
        };
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PropertyEditorTemplateSelector.cs
git commit -m "feat: add PropertyEditorTemplateSelector for ValueKind-based editor selection"
```

---

## Task 11: PropertyGridControl

**Files:**
- Create: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`
- Create: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs`

- [ ] **Step 1: Create PropertyGridControl.xaml**

```xml
<!-- src/Snaipe.Inspector/Controls/PropertyGridControl.xaml -->
<UserControl
    x:Class="Snaipe.Inspector.Controls.PropertyGridControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Snaipe.Inspector.Controls"
    xmlns:vm="using:Snaipe.Inspector.ViewModels">

    <UserControl.Resources>

        <!-- Selector instance -->
        <local:PropertyEditorTemplateSelector x:Key="EditorSelector">
            <local:PropertyEditorTemplateSelector.ReadOnlyTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBlock Text="{x:Bind Entry.Value}"
                               FontSize="12"
                               TextTrimming="CharacterEllipsis"
                               VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.ReadOnlyTemplate>

            <local:PropertyEditorTemplateSelector.TextTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBox Text="{x:Bind EditValue, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                             FontSize="12"
                             BorderBrush="{x:Bind ErrorBorderBrush, Mode=OneWay}"
                             BorderThickness="1"
                             ToolTipService.ToolTip="{x:Bind Entry.ErrorMessage, Mode=OneWay}"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.TextTemplate>

            <local:PropertyEditorTemplateSelector.BooleanTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <CheckBox IsChecked="{x:Bind IsCheckedValue, Mode=TwoWay}"
                              VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.BooleanTemplate>

            <local:PropertyEditorTemplateSelector.NumberTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <NumberBox Value="{x:Bind NumberValue, Mode=TwoWay}"
                               SpinButtonPlacementMode="Compact"
                               FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.NumberTemplate>

            <local:PropertyEditorTemplateSelector.EnumTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <ComboBox ItemsSource="{x:Bind Entry.EnumValues}"
                              SelectedItem="{x:Bind EditValue, Mode=TwoWay}"
                              FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.EnumTemplate>
        </local:PropertyEditorTemplateSelector>

    </UserControl.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <ItemsControl ItemsSource="{x:Bind ViewModel.PropertyGroups, Mode=OneWay}">
            <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="vm:PropertyGroupViewModel">

                    <!-- Category header -->
                    <StackPanel>
                        <Border BorderThickness="0,1,0,0" Padding="4,6,4,2" Margin="0,8,0,0">
                            <TextBlock Text="{x:Bind Category}"
                                       FontWeight="Bold"
                                       FontSize="13"/>
                        </Border>

                        <!-- Property rows -->
                        <ItemsControl ItemsSource="{x:Bind Properties}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                                    <Grid Padding="4,2" ColumnSpacing="4">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="1.5*"/>
                                            <ColumnDefinition Width="0.7*"/>
                                            <ColumnDefinition Width="2*"/>
                                            <ColumnDefinition Width="1*"/>
                                        </Grid.ColumnDefinitions>

                                        <TextBlock Grid.Column="0"
                                                   Text="{x:Bind Entry.Name}"
                                                   FontSize="12"
                                                   VerticalAlignment="Center"
                                                   TextTrimming="CharacterEllipsis"
                                                   ToolTipService.ToolTip="{x:Bind Entry.Name}"/>

                                        <TextBlock Grid.Column="1"
                                                   Text="{x:Bind Entry.ValueType}"
                                                   FontSize="10"
                                                   Opacity="0.6"
                                                   VerticalAlignment="Center"
                                                   TextTrimming="CharacterEllipsis"/>

                                        <!-- Editor selected by ValueKind -->
                                        <ContentPresenter Grid.Column="2"
                                                          Content="{x:Bind}"
                                                          ContentTemplateSelector="{StaticResource EditorSelector}"/>

                                        <TextBlock Grid.Column="3"
                                                   Text="{x:Bind Entry.BindingExpression}"
                                                   FontSize="10"
                                                   Opacity="0.5"
                                                   VerticalAlignment="Center"
                                                   TextTrimming="CharacterEllipsis"
                                                   ToolTipService.ToolTip="{x:Bind Entry.BindingExpression}"/>
                                    </Grid>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>

                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 2: Add helper properties to PropertyRowViewModel**

`x:Bind` in the templates above uses two helper properties that map between `EditValue` (string) and typed values the editors need. Add these to `PropertyRowViewModel.cs`:

```csharp
// Append inside PropertyRowViewModel class body:

/// <summary>Brush used to show error state on text editors.</summary>
public Windows.UI.Color ErrorBorderColor =>
    HasError
        ? Windows.UI.Color.FromArgb(0xFF, 0xBF, 0x00, 0x00)
        : Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00);

/// <summary>Two-way bool bridge for CheckBox editor.</summary>
public bool? IsCheckedValue
{
    get => bool.TryParse(EditValue, out var b) ? b : null;
    set
    {
        EditValue = value?.ToString() ?? "False";
        CommitEditCommand.Execute(null);
    }
}

/// <summary>Two-way double bridge for NumberBox editor.</summary>
public double NumberValue
{
    get => double.TryParse(EditValue, System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
    set
    {
        EditValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CommitEditCommand.Execute(null);
    }
}
```

Also add the `using` at the top of `PropertyRowViewModel.cs`:
```csharp
using Microsoft.UI;  // for Windows.UI.Color via WinUI
```

- [ ] **Step 3: Create PropertyGridControl.xaml.cs**

```csharp
// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    public PropertyGridControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded. If the XAML compiler reports issues with `{x:Bind Entry.ErrorMessage}` on `ToolTipService.ToolTip`, change those to `{Binding Entry.ErrorMessage}` (Tooltip accepts object, `x:Bind` to nullable strings can be fussy).

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PropertyGridControl.xaml src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs
git commit -m "feat: add PropertyGridControl with type-specific editors and PropertyRowViewModel helpers"
```

---

## Task 12: PreviewPaneControl

**Files:**
- Create: `src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml`
- Create: `src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml.cs`

- [ ] **Step 1: Create PreviewPaneControl.xaml**

```xml
<!-- src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml -->
<UserControl
    x:Class="Snaipe.Inspector.Controls.PreviewPaneControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="8">
        <StackPanel Spacing="4">

            <TextBlock Text="Element Preview"
                       FontWeight="Bold"
                       FontSize="13"
                       Margin="0,0,0,4"/>

            <Grid ColumnSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" Text="Type:"    FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="0" Grid.Column="1" Text="{x:Bind SelectedTypeName, Mode=OneWay}"  FontSize="12"/>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="Name:"    FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="1" Grid.Column="1" Text="{x:Bind SelectedName, Mode=OneWay}"     FontSize="12"/>

                <TextBlock Grid.Row="2" Grid.Column="0" Text="ID:"      FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="2" Grid.Column="1" Text="{x:Bind SelectedId, Mode=OneWay}"       FontSize="11" FontFamily="Consolas"/>

                <TextBlock Grid.Row="3" Grid.Column="0" Text="X:"       FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="3" Grid.Column="1" Text="{x:Bind SelectedBoundsX, Mode=OneWay}"  FontSize="12"/>

                <TextBlock Grid.Row="4" Grid.Column="0" Text="Y:"       FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="4" Grid.Column="1" Text="{x:Bind SelectedBoundsY, Mode=OneWay}"  FontSize="12"/>

                <TextBlock Grid.Row="5" Grid.Column="0" Text="Width:"   FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="5" Grid.Column="1" Text="{x:Bind SelectedBoundsW, Mode=OneWay}"  FontSize="12"/>

                <TextBlock Grid.Row="6" Grid.Column="0" Text="Height:"  FontSize="12" Opacity="0.7"/>
                <TextBlock Grid.Row="6" Grid.Column="1" Text="{x:Bind SelectedBoundsH, Mode=OneWay}"  FontSize="12"/>
            </Grid>

            <TextBlock Text="Visual preview coming in a future release."
                       FontSize="11"
                       Opacity="0.5"
                       Margin="0,12,0,0"
                       TextWrapping="Wrap"/>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 2: Create PreviewPaneControl.xaml.cs**

```csharp
// src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PreviewPaneControl : UserControl
{
    public PreviewPaneControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private TreeNodeViewModel? Selected => ViewModel?.SelectedNode;

    public string SelectedTypeName => Selected?.Node.TypeName ?? "—";
    public string SelectedName     => Selected?.Node.Name ?? "(none)";
    public string SelectedId       => Selected?.Node.Id ?? "—";
    public string SelectedBoundsX  => Selected?.Node.Bounds?.X.ToString("F1") ?? "—";
    public string SelectedBoundsY  => Selected?.Node.Bounds?.Y.ToString("F1") ?? "—";
    public string SelectedBoundsW  => Selected?.Node.Bounds?.Width.ToString("F1") ?? "—";
    public string SelectedBoundsH  => Selected?.Node.Bounds?.Height.ToString("F1") ?? "—";
}
```

> **Note:** The `SelectedXxx` properties in PreviewPaneControl don't auto-update when `ViewModel.SelectedNode` changes because `x:Bind` doesn't know to re-evaluate them. Call `Bindings.Update()` in the control's `Loaded` event and subscribe to `ViewModel.PropertyChanged` to refresh on `SelectedNode` change:

Add this to the constructor after `DataContextChanged`:
```csharp
Loaded += (_, _) =>
{
    if (ViewModel is not null)
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedNode))
                Bindings.Update();
        };
};
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Run Inspector to do a visual sanity check**

```bash
dotnet run --project src/Snaipe.Inspector
```

Expected: Inspector window opens with connection bar at top, tree pane on the left, property grid and preview pane on the right. UI is empty until connected.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml src/Snaipe.Inspector/Controls/PreviewPaneControl.xaml.cs
git commit -m "feat: add PreviewPaneControl with element bounds and type display"
```

---

## Task 13: SampleViewModel

**Files:**
- Create: `samples/Snaipe.SampleApp/SampleViewModel.cs`

- [ ] **Step 1: Create SampleViewModel**

```csharp
// samples/Snaipe.SampleApp/SampleViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Snaipe.SampleApp;

public sealed class SampleViewModel : INotifyPropertyChanged
{
    private string _name = "Alice";
    private int _age = 30;
    private string _buttonLabel = "Click Me";
    private bool _isEnabled = true;
    private double _sliderValue = 0.8;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int Age
    {
        get => _age;
        set => SetField(ref _age, value);
    }

    public string ButtonLabel
    {
        get => _buttonLabel;
        set => SetField(ref _buttonLabel, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (SetField(ref _sliderValue, value))
            {
                OnPropertyChanged(nameof(SwatchColor));
            }
        }
    }

    /// <summary>
    /// Interpolates between blue (#0078D4) and orange (#FF8C00) based on SliderValue (0–1).
    /// Exercises the Color/SolidColorBrush PropertyEntry path in PropertyReader.
    /// </summary>
    public SolidColorBrush SwatchColor
    {
        get
        {
            var t = Math.Clamp(_sliderValue, 0, 1);
            var r = (byte)(0x00 + t * (0xFF - 0x00));
            var g = (byte)(0x78 + t * (0x8C - 0x78));
            var b = (byte)(0xD4 + t * (0x00 - 0xD4));
            return new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
        }
    }

    public ObservableCollection<SampleItem> Items { get; } =
    [
        new("Alpha",   "100"),
        new("Beta",    "200"),
        new("Gamma",   "300"),
        new("Delta",   "400"),
        new("Epsilon", "500"),
        new("Zeta",    "600"),
        new("Eta",     "700"),
        new("Theta",   "800"),
    ];
}

public sealed record SampleItem(string Name, string Value);
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add samples/Snaipe.SampleApp/SampleViewModel.cs
git commit -m "feat: add SampleViewModel with diverse property types for inspector testing"
```

---

## Task 14: SampleApp MainWindow.xaml (replace code-behind-only version)

**Files:**
- Create: `samples/Snaipe.SampleApp/MainWindow.xaml`
- Create: `samples/Snaipe.SampleApp/MainWindow.xaml.cs`
- Delete: `samples/Snaipe.SampleApp/MainWindow.cs`

- [ ] **Step 1: Create MainWindow.xaml**

```xml
<!-- samples/Snaipe.SampleApp/MainWindow.xaml -->
<Window
    x:Class="Snaipe.SampleApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Snaipe.SampleApp">

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel x:Name="RootPanel"
                    Padding="24"
                    Spacing="16"
                    MaxWidth="600">

            <!-- Header -->
            <TextBlock x:Name="HeaderText"
                       Text="Snaipe Sample App"
                       FontSize="24"
                       FontWeight="Bold"/>

            <!-- Form grid: Name + Age -->
            <Grid x:Name="FormGrid" ColumnSpacing="12" RowSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="80"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" Text="Name:"
                           VerticalAlignment="Center"/>
                <TextBox   Grid.Row="0" Grid.Column="1"
                           x:Name="NameBox"
                           Text="{x:Bind ViewModel.Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                           PlaceholderText="Enter your name"/>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="Age:"
                           VerticalAlignment="Center"/>
                <NumberBox Grid.Row="1" Grid.Column="1"
                           x:Name="AgeBox"
                           Value="{x:Bind ViewModel.Age, Mode=TwoWay}"
                           SpinButtonPlacementMode="Compact"
                           Minimum="0" Maximum="150"/>
            </Grid>

            <!-- Buttons row -->
            <StackPanel x:Name="ButtonsPanel"
                        Orientation="Horizontal"
                        Spacing="8">
                <Button x:Name="PrimaryBtn"
                        Content="{x:Bind ViewModel.ButtonLabel, Mode=OneWay}"/>
                <Button Content="Disabled"
                        IsEnabled="False"/>
                <ToggleButton x:Name="ToggleBtn"
                              Content="Toggle Me"/>
            </StackPanel>

            <!-- CheckBox -->
            <CheckBox x:Name="OptionsCheck"
                      Content="Enable options"
                      IsChecked="{x:Bind ViewModel.IsEnabled, Mode=TwoWay}"/>

            <!-- Slider -->
            <StackPanel Spacing="4">
                <TextBlock Text="Swatch blending:" FontSize="12" Opacity="0.7"/>
                <Slider x:Name="OpacitySlider"
                        Minimum="0" Maximum="1" StepFrequency="0.01"
                        Value="{x:Bind ViewModel.SliderValue, Mode=TwoWay}"/>
            </StackPanel>

            <!-- Color swatch: Fill bound to ViewModel.SwatchColor -->
            <Rectangle x:Name="ColorSwatch"
                       Height="40"
                       Fill="{x:Bind ViewModel.SwatchColor, Mode=OneWay}"
                       RadiusX="4" RadiusY="4"/>

            <!-- Styled border with nested TextBlock -->
            <Border x:Name="StyledBorder"
                    Margin="0,8"
                    Padding="12,8"
                    CornerRadius="6"
                    BorderThickness="2">
                <TextBlock Text="Styled content — inspects border + child TextBlock"/>
            </Border>

            <!-- DataContext demo panel -->
            <StackPanel x:Name="DataContextPanel"
                        Spacing="4"
                        Padding="8"
                        BorderThickness="1">
                <TextBlock x:Name="DataContextLabel"
                           Text="DataContext inherited below:"
                           Opacity="0.7"/>
                <TextBlock x:Name="DataContextChild"
                           Text="I inherit DataContext from RootPanel"/>
            </StackPanel>

            <!-- ListView with data-bound items -->
            <ListView x:Name="ItemList"
                      ItemsSource="{x:Bind ViewModel.Items}"
                      MaxHeight="200"
                      SelectionMode="Single">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="local:SampleItem">
                        <Grid ColumnSpacing="8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{x:Bind Name}" FontSize="13"/>
                            <TextBlock Grid.Column="1" Text="{x:Bind Value}"
                                       FontSize="12" Opacity="0.6"/>
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

        </StackPanel>
    </ScrollViewer>
</Window>
```

- [ ] **Step 2: Create MainWindow.xaml.cs**

```csharp
// samples/Snaipe.SampleApp/MainWindow.xaml.cs
using Microsoft.UI.Xaml;

namespace Snaipe.SampleApp;

public sealed partial class MainWindow : Window
{
    private readonly SampleViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Title = "Snaipe Sample App";
    }

    public SampleViewModel ViewModel => _viewModel;
}
```

- [ ] **Step 3: Delete old MainWindow.cs**

```bash
rm samples/Snaipe.SampleApp/MainWindow.cs
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Run SampleApp to verify UI**

```bash
dotnet run --project samples/Snaipe.SampleApp
```

Expected: SampleApp window opens showing the form, buttons, checkbox, slider, color swatch, border, and ListView.

- [ ] **Step 6: Commit**

```bash
git add samples/Snaipe.SampleApp/MainWindow.xaml samples/Snaipe.SampleApp/MainWindow.xaml.cs
git rm samples/Snaipe.SampleApp/MainWindow.cs
git commit -m "feat: replace SampleApp code-behind with XAML layout bound to SampleViewModel"
```

---

## Task 15: Feature C — Integration Smoke Test

This task has no code changes. It validates the full loop end-to-end by running both apps and walking through each scenario.

- [ ] **Step 1: Launch SampleApp**

```bash
dotnet run --project samples/Snaipe.SampleApp
```

Expected: SampleApp opens. In console output you should see:
```
[Snaipe.Agent] Starting agent with pipe name: snaipe-<PID>
[Snaipe.Agent] Discovery file created.
[Snaipe.Agent] Attached to window. Pipe: snaipe-<PID>
```

- [ ] **Step 2: Launch Inspector (in a second terminal)**

```bash
dotnet run --project src/Snaipe.Inspector
```

Expected: Inspector opens. Status bar shows "Found 1 agent(s)." SampleApp entry appears in the agent dropdown (e.g., `"Snaipe.SampleApp (PID 12345) — Snaipe Sample App"`).

- [ ] **Step 3: Connect**

Select the SampleApp entry in the dropdown and click **Connect**.

Expected:
- Status bar shows "Connecting to Snaipe.SampleApp..."
- Then "Tree loaded (N elements)."
- ElementTreeControl shows the visual tree rooted at a `ScrollViewer`.
- Root node is visible and expanded.

- [ ] **Step 4: Select a tree node**

Click on `Button "PrimaryBtn"` in the tree.

Expected:
- PropertyGridControl populates with property groups (Common, Layout, Appearance, …).
- `Content` property shows `"Click Me"` (bound value from ViewModel).
- SampleApp shows a blue highlight rectangle around the Primary button.

- [ ] **Step 5: Edit a property**

In the PropertyGrid, find the `Content` property row (Category: Common). Change the value to `"Hello!"` and press Tab or Enter.

Expected:
- SampleApp's Primary button label changes to `"Hello!"` live.
- The row's displayed value updates to `"Hello!"`.
- No error border on the row.

- [ ] **Step 6: Try editing a read-only property**

Find `ActualWidth` (read-only TextBlock in Common). Confirm it shows as plain text (not an editable field).

- [ ] **Step 7: Refresh Tree**

Click **Refresh Tree** in the ElementTreeControl.

Expected:
- Tree re-fetches. If you had expanded any nodes, they remain expanded (matched by element ID).
- Status bar shows updated element count.

- [ ] **Step 8: Close SampleApp**

Close the SampleApp window.

Expected:
- Inspector status bar shows "Connection lost — … Reconnect?"
- ElementTreeControl clears.
- PropertyGridControl clears.

- [ ] **Step 9: Verify no-agent state**

Click **⟳** (Refresh Agents) in the ConnectionBar.

Expected: Status bar shows "No Snaipe agents found — is your app running with Snaipe.Agent?" Dropdown is empty.

- [ ] **Step 10: Commit test log and update PROJECT_STATE.md**

Update `docs/PROJECT_STATE.md` to mark the integration test as complete:

```markdown
## 📈 Current Status
* **Stage:** Feature-complete MVP.
* **Working:** Full connect→tree→properties→edit→disconnect loop. Inspector UI (XAML + MVVM). SampleApp with diverse control set.
* **Pending:** Color picker editor, enum dropdown editor, visual preview (RenderTargetBitmap), Linux target.

## 📝 Recent Progress
* Migrated Snaipe.Inspector to XAML + MVVM (UserControl-per-pane, single MainViewModel).
* Added SampleViewModel + XAML layout to SampleApp covering all ValueKind variants.
* Verified full integration loop: connect, tree, select, properties, edit, disconnect, reconnect.

## 🚧 Next Steps
- [ ] Color picker editor for `"Color"` ValueKind
- [ ] Enum dropdown editor (EnumValues already available in PropertyEntry)
- [ ] Visual preview via RenderTargetBitmap
- [ ] Linux (X11) target parity
```

```bash
git add docs/PROJECT_STATE.md
git commit -m "docs: mark Inspector UI + SampleApp + integration as complete in PROJECT_STATE"
```

---

## Self-Review Checklist

- [x] **Spec §2.1 (XAML + UserControl approach):** Tasks 7–12 implement all four controls plus the shell.
- [x] **Spec §2.3 (MainViewModel shape):** Tasks 4–6 implement all properties and commands with exact signatures.
- [x] **Spec §2.4 (Connection lifecycle):** `ConnectAsync`/`Disconnect`/`HandleConnectionLost` in Task 5 cover all three transitions.
- [x] **Spec §2.5 (XAML layout):** MainWindow shell (Task 7), all four controls (Tasks 8–12) match the spec layout.
- [x] **Spec §2.6 (x:Bind pattern):** Every UserControl code-behind exposes `public MainViewModel ViewModel => (MainViewModel)DataContext` and calls `Bindings.Update()` on `DataContextChanged`.
- [x] **Spec §3 (SampleApp):** Task 13 (SampleViewModel) + Task 14 (XAML) cover all controls, bindings, and `SnaipeAgent.Attach` (already in `App.cs`).
- [x] **Spec §4.1 (Smoke test):** Task 15 steps 1–9 walk through all 7 smoke test scenarios.
- [x] **Spec §4.2 (Error surface):** `HandleConnectionLost`, `SnaipeProtocolException` catches in `SetPropertyAsync` and `OnSelectedNodeChangedAsync`, and the initial no-agents message all implement the error table.
- [x] **Type consistency:** `AsyncRelayCommand` used consistently (not `RelayCommand`) for async commands. `PropertyRowViewModel.CommitEditCommand` typed as `AsyncRelayCommand`. `ConnectCommand` typed as `AsyncRelayCommand`. `DisconnectCommand` and `RefreshAgentsCommand` typed as `RelayCommand`.
