# Snaipe Inspector UI, SampleApp Enrichment & End-to-End Integration — Design

**Date:** 2026-03-29
**Status:** Approved
**Scope:** Three sequential features — (A) Inspector UI migration to XAML + MVVM, (B) SampleApp enrichment, (C) end-to-end integration smoke test

---

## 1. Overview

This design covers the next three milestones for Snaipe:

- **Feature A — Inspector UI:** Migrate `Snaipe.Inspector` from pure code-behind to XAML + full MVVM. Introduce four `UserControl`-per-pane components sharing a single `MainViewModel`.
- **Feature B — SampleApp:** Enrich `Snaipe.SampleApp` with a representative variety of Uno controls, bindings, and data to exercise every PropertyReader path.
- **Feature C — Integration:** Wire up and validate the full connect → tree → properties → edit → disconnect loop, with explicit error surfaces.

---

## 2. Feature A — Inspector UI

### 2.1 Approach

**UserControl-per-pane with a single shared `MainViewModel`.** `MainWindow.xaml` is a thin shell. Each UI region is a self-contained `UserControl`. All controls inherit the window's `DataContext` (`MainViewModel`). System-native theming (Uno's built-in light/dark theme support). `x:Bind` (compiled bindings) throughout.

The existing `InspectorIpcClient.cs` and `AgentDiscoveryScanner.cs` are unchanged — `MainViewModel` holds instances of both. The existing `MainWindow.cs` (pure code-behind) is deleted.

### 2.2 File Structure

```
src/Snaipe.Inspector/
  App.cs                               (unchanged)
  Program.cs                           (unchanged)
  MainWindow.xaml                      (NEW — shell layout)
  MainWindow.xaml.cs                   (NEW — replaces MainWindow.cs)

  ViewModels/
    MainViewModel.cs                   (NEW)

  Controls/
    ConnectionBarControl.xaml          (NEW)
    ConnectionBarControl.xaml.cs
    ElementTreeControl.xaml            (NEW)
    ElementTreeControl.xaml.cs
    PropertyGridControl.xaml           (NEW)
    PropertyGridControl.xaml.cs
    PreviewPaneControl.xaml            (NEW)
    PreviewPaneControl.xaml.cs
```

### 2.3 MainViewModel

Single source of truth for all Inspector state.

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    // Services
    private readonly AgentDiscoveryScanner _scanner;
    private readonly InspectorIpcClient _client;

    // Connection state
    public ObservableCollection<AgentInfo> DiscoveredAgents { get; }
    public AgentInfo? SelectedAgent          { get; set; }
    public bool IsConnected                  { get; private set; }
    public string StatusMessage              { get; private set; }

    // Tree state
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; }
    public TreeNodeViewModel? SelectedNode   { get; set; }   // triggers property fetch on change

    // Property state
    public ObservableCollection<PropertyGroupViewModel> PropertyGroups { get; }
    public bool IsLoadingProperties          { get; private set; }

    // Commands
    public ICommand ConnectCommand           { get; }   // requires SelectedAgent, state=Disconnected
    public ICommand DisconnectCommand        { get; }   // requires state=Connected
    public ICommand RefreshAgentsCommand     { get; }
    public ICommand RefreshTreeCommand       { get; }   // requires state=Connected
    public ICommand SetPropertyCommand       { get; }   // args: (elementId, name, value)
}
```

#### TreeNodeViewModel

Wraps `ElementNode`, adds UI state without mutating protocol records.

```csharp
public class TreeNodeViewModel : INotifyPropertyChanged
{
    public ElementNode Node    { get; }
    public string DisplayName  { get; }  // "Button \"SubmitBtn\"" or "StackPanel"
    public bool IsExpanded     { get; set; }
    public bool IsSelected     { get; set; }
    public ObservableCollection<TreeNodeViewModel> Children { get; }
}
```

#### PropertyGroupViewModel / PropertyRowViewModel

```csharp
public class PropertyGroupViewModel
{
    public string Category { get; }
    public ObservableCollection<PropertyRowViewModel> Properties { get; }
}

public class PropertyRowViewModel : INotifyPropertyChanged
{
    public PropertyEntry Entry     { get; }
    public string EditValue        { get; set; }   // two-way bound to editor
    public bool HasError           { get; private set; }
    public string? ErrorMessage    { get; private set; }
    public ICommand CommitEditCommand { get; }
}
```

#### State Flow

1. `SelectedAgent` changes → `ConnectCommand` becomes executable.
2. Connect succeeds → `IsConnected = true`, `RefreshTreeCommand` fires automatically.
3. `SelectedNode` changes → `GetPropertiesRequest` sent, `PropertyGroups` populated.
4. `PropertyRowViewModel.CommitEditCommand` → `SetPropertyRequest` sent; on success updates `EditValue`, on failure sets `HasError` + `ErrorMessage`.
5. Disconnect / pipe broken → clears `RootNodes`, `PropertyGroups`, sets `StatusMessage`.

### 2.4 Connection Lifecycle

```
Disconnected ──[Connect]──► Connecting ──[success]──► Connected
     ▲                           │                        │
     │                     [timeout/error]          [pipe broken]
     │                           │                        │
     └───────────────────────────┴────────────────────────┘
              (clear tree + properties, show error in StatusMessage)
```

`MainViewModel` drives this via a private `ConnectionState` enum. Commands are gated on state: `ConnectCommand` only runnable from `Disconnected`; `RefreshTreeCommand` and `SetPropertyCommand` only from `Connected`.

### 2.5 XAML Layout

#### MainWindow.xaml — Shell

Three rows: connection bar, main split content, status bar.

```xml
<Window ...>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <controls:ConnectionBarControl Grid.Row="0"/>

    <Grid Grid.Row="1">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="300" MinWidth="150"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <controls:ElementTreeControl Grid.Column="0"/>
      <GridSplitter Grid.Column="1" Width="4"/>
      <Grid Grid.Column="2">
        <Grid.RowDefinitions>
          <RowDefinition Height="*"/>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="160"/>
        </Grid.RowDefinitions>
        <controls:PropertyGridControl Grid.Row="0"/>
        <GridSplitter Grid.Row="1" Height="4"/>
        <controls:PreviewPaneControl Grid.Row="2"/>
      </Grid>
    </Grid>

    <TextBlock Grid.Row="2"
               Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
               Padding="8,4"/>
  </Grid>
</Window>
```

#### ElementTreeControl.xaml

`TreeView` with `VirtualizingStackPanel` for performance with large trees. Refresh button at top.

```xml
<UserControl ...>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
    <Button Grid.Row="0" Content="Refresh Tree"
            Command="{x:Bind ViewModel.RefreshTreeCommand}"/>
    <TreeView Grid.Row="1"
              ItemsSource="{x:Bind ViewModel.RootNodes, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedNode, Mode=TwoWay}">
      <TreeView.ItemTemplate>
        <DataTemplate x:DataType="vm:TreeNodeViewModel">
          <TextBlock Text="{x:Bind DisplayName}"/>
        </DataTemplate>
      </TreeView.ItemTemplate>
      <TreeView.ItemContainerStyle>
        <Style TargetType="TreeViewItem">
          <Setter Property="IsExpanded" Value="{x:Bind IsExpanded, Mode=TwoWay}"/>
          <Setter Property="ItemsSource" Value="{x:Bind Children}"/>
        </Style>
      </TreeView.ItemContainerStyle>
    </TreeView>
  </Grid>
</UserControl>
```

#### PropertyGridControl.xaml

Groups via `ItemsControl` over `PropertyGroups`. Rows use a `DataTemplateSelector` (`PropertyEditorTemplateSelector`) to pick the right editor per `ValueKind`.

| ValueKind | Editor |
|-----------|--------|
| `"Boolean"` | `CheckBox` two-way bound to `EditValue` |
| `"Number"` | `NumberBox` two-way bound to `EditValue` |
| `"Enum"`, `"Color"`, `"String"`, `"Object"` | `TextBox` (text fallback) |
| Any read-only | `TextBlock` (no editor) |

Commit on lost focus or Enter key. Error state: red border + tooltip showing `ErrorMessage`.

#### PreviewPaneControl.xaml

`StackPanel` showing selected element's type name, element name, and bounds (`X`, `Y`, `Width`, `Height`) as labelled `TextBlock` rows. Static `TextBlock` placeholder: *"Visual preview coming in a future release."*

### 2.6 x:Bind Pattern

Each UserControl code-behind exposes a typed accessor:

```csharp
public MainViewModel ViewModel => (MainViewModel)DataContext;
```

XAML binds via `{x:Bind ViewModel.SomeProperty, Mode=OneWay}`. This provides compile-time safety and IntelliSense without passing the ViewModel explicitly to each control.

---

## 3. Feature B — SampleApp Enrichment

### 3.1 Goal

Provide a representative cross-section of Uno controls that exercises every `PropertyReader` path: all `ValueKind` variants, bound vs. unbound properties, named vs. anonymous elements, nested layouts, and read-only vs. writable properties.

### 3.2 MainWindow Layout

```
ScrollViewer
└── StackPanel x:Name="RootPanel"
    ├── TextBlock "Snaipe Sample App"          (Header, no binding)
    ├── Grid (two-column form)
    │   ├── TextBlock "Name:"
    │   ├── TextBox x:Name="NameBox"           (bound to ViewModel.Name, TwoWay)
    │   ├── TextBlock "Age:"
    │   └── NumberBox x:Name="AgeBox"          (bound to ViewModel.Age, TwoWay)
    ├── StackPanel (horizontal, Spacing=8)
    │   ├── Button x:Name="PrimaryBtn"         (Content bound to ViewModel.ButtonLabel)
    │   ├── Button Content="Disabled"           (IsEnabled=False — exercises read-only path)
    │   └── ToggleButton x:Name="ToggleBtn"
    ├── CheckBox x:Name="OptionsCheck"         (IsChecked bound to ViewModel.IsEnabled, TwoWay)
    ├── Slider x:Name="OpacitySlider"          (Value bound to ViewModel.SliderValue, TwoWay, 0–1)
    ├── Rectangle x:Name="ColorSwatch"        (Fill bound to ViewModel.SwatchColor — exercises Color ValueKind)
    ├── Border x:Name="StyledBorder"           (explicit Margin, Padding, CornerRadius, Background)
    │   └── TextBlock "Styled content"
    └── ListView x:Name="ItemList"             (ItemsSource bound to ViewModel.Items)
        └── DataTemplate → TextBlock bound to item.Name
```

### 3.3 SampleViewModel

```csharp
public class SampleViewModel : INotifyPropertyChanged
{
    public string Name            { get; set; } = "Alice";
    public int Age                { get; set; } = 30;
    public string ButtonLabel     { get; set; } = "Click Me";
    public bool IsEnabled         { get; set; } = true;
    public double SliderValue     { get; set; } = 0.8;
    public Brush SwatchColor      { get; }  // SolidColorBrush, updates when SliderValue changes
    public ObservableCollection<SampleItem> Items { get; }  // 8 items
}

public record SampleItem(string Name, string Value);
```

`SwatchColor` interpolates between two colors based on `SliderValue`, ensuring `PropertyReader` sees a live `SolidColorBrush` with a non-trivial value.

### 3.4 Agent Attach

`SnaipeAgent.Attach(window)` called in `MainWindow`'s `Loaded` event handler (not in constructor, to ensure the visual tree is ready).

---

## 4. Feature C — End-to-End Integration

### 4.1 Smoke Test Sequence

The minimum working loop to validate the full stack:

1. Launch `Snaipe.SampleApp` → agent writes discovery file, IPC listener starts.
2. Launch `Snaipe.Inspector` → scans discovery dir, SampleApp entry appears in dropdown.
3. Click **Connect** → `ConnectAsync` succeeds, `GetTreeRequest` fires automatically, tree populates.
4. Click a tree node → `GetPropertiesRequest` fires, property grid populates with categorised rows.
5. Edit a writable property (e.g., `TextBlock.Text`) → `SetPropertyRequest` fires, SampleApp reflects change live.
6. Click **Refresh Tree** → tree re-fetches; previously expanded nodes remain expanded (matched by element ID).
7. Close SampleApp → pipe breaks, Inspector shows *"Connection lost — Reconnect?"* in status bar; tree and properties cleared.

### 4.2 Error Surface

| Scenario | UI behaviour |
|----------|-------------|
| No agents discovered | Dropdown empty; status: *"No Snaipe agents found — is your app running with Snaipe.Agent?"* |
| Connect timeout (5 s) | Status: *"Connection timed out"*; state → Disconnected |
| Pipe broken mid-request | Status: *"Connection lost — Reconnect?"*; Reconnect button shown; tree + properties cleared |
| Element GC'd (error 1002) | Property grid shows: *"Element no longer in tree — refresh the tree"* |
| Property write rejected (1004) | Inline red border + tooltip: *"Property is read-only"* |
| Invalid property value (1005) | Inline red border + tooltip: *"Cannot parse value as [type]: [details]"* |
| Tree truncated (1006) | Synthetic *"… N children omitted"* node in tree; no error state |
| Payload too large (1009) | Status: *"Response too large — tree may be too deep"*; state → Disconnected |

---

## 5. Implementation Order

1. **Feature A** — Inspector UI (XAML migration + MVVM)
   - `MainViewModel` + ViewModel classes
   - `MainWindow.xaml` shell
   - `ConnectionBarControl`
   - `ElementTreeControl`
   - `PropertyGridControl` (with `PropertyEditorTemplateSelector`)
   - `PreviewPaneControl`
   - Delete old `MainWindow.cs`

2. **Feature B** — SampleApp
   - `SampleViewModel`
   - `MainWindow.xaml` with full control variety
   - Wire `SnaipeAgent.Attach` in `Loaded`

3. **Feature C** — Integration
   - Connect → tree → select → properties → edit loop
   - All error surfaces per Section 4.2
   - Refresh tree with expanded-state preservation

---

## 6. Out of Scope

- Color picker control (deferred; text field fallback used)
- Enum dropdown editor (deferred; text field fallback used)
- Visual preview via `RenderTargetBitmap` (placeholder only)
- Auto-refresh / polling interval
- Linux (X11) target — Windows Skia host first; Linux parity is a follow-up
