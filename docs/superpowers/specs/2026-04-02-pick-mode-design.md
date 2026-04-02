# Pick Mode & Popup Tree Coverage — Design Spec

**Date:** 2026-04-02  
**Status:** Approved  

---

## 1. Overview

This feature adds two related inspection capabilities to Snaipe:

1. **Pick mode** — hold Ctrl+Shift while hovering the target app to select elements in the inspector tree in real time. Clicks while the modifier is held are suppressed so they do not trigger the target app's normal event handlers.
2. **Popup tree coverage** — ComboBox dropdowns, Flyouts, Tooltips, and other popups are currently invisible to the tree walker because they live outside `Window.Content`. The tree walker is extended to also walk open popup subtrees so they appear as additional roots in the inspector.

---

## 2. Goals and Non-Goals

### Goals

- Hover an element in the target app (with modifier held) → that node is selected and scrolled into view in the inspector tree
- Modifier-held click is suppressed (does not reach the target app)
- Open popups appear in the inspector tree as additional root nodes alongside the main window content
- Inspector shows a status message when pick mode is active
- Works on both Windows and Linux (no Win32 hooks)

### Non-Goals

- Configurable modifier key (Ctrl+Shift is hardcoded; configuration deferred to a future change)
- Pick mode across multiple windows (single `Window` per `SnaipeAgent.Attach` call)
- Changes to `HighlightOverlay` beyond tagging the canvas — the existing highlight mechanism works as-is for pick mode hover feedback

---

## 3. Architecture

### 3.1 New push (events) channel

The current IPC is strictly request/response (inspector → agent). Pick mode requires the agent to push `ElementUnderCursorEvent` messages to the inspector in real time. This is implemented as a **second named pipe** rather than multiplexing the existing command pipe:

| Pipe | Direction | Purpose |
|---|---|---|
| `snaipe-{pid}` | Inspector → Agent (req/resp) | Existing command channel — unchanged |
| `snaipe-{pid}-events` | Agent → Inspector (push) | New notification channel |

The events pipe is best-effort: if the inspector is not connected the agent silently discards events. The inspector treats events pipe errors as non-fatal.

### 3.2 Component map

```
Inspector                          Agent
─────────────────────────────────  ──────────────────────────────────────────
InspectorIpcClient                 AgentEventServer
  ConnectEventsAsync()    ──────►    (NamedPipeServerStream snaipe-{pid}-events)
  EventReceived event     ◄──────    Channel<InspectorMessage> queue + write loop

MainViewModel                      PickModeManager
  handles ElementUnderCursorEvent    AddHandler(PointerMoved, handledEventsToo)
  handles PickModeActiveEvent        AddHandler(PointerPressed, handledEventsToo)
  auto-scroll + select node          FindElementsInHostCoordinates → EnqueueEvent

FetchTreeAsync                     VisualTreeWalker.BuildTree (extended)
  iterates Roots list    ◄──────    walks Window.Content + open popups via XamlRoot
```

---

## 4. Protocol Changes (`Snaipe.Protocol`)

### 4.1 New message types

```csharp
// Agent → Inspector (push only, never a response to a request)

[JsonDerivedType(typeof(ElementUnderCursorEvent), "ElementUnderCursorEvent")]
public sealed record ElementUnderCursorEvent : InspectorMessage
{
    public required string ElementId { get; init; }
    public required string TypeName  { get; init; }  // for status bar — avoids extra round-trip
}

[JsonDerivedType(typeof(PickModeActiveEvent), "PickModeActiveEvent")]
public sealed record PickModeActiveEvent : InspectorMessage
{
    public bool Active { get; init; }
}
```

Both are added to the `[JsonPolymorphic]` discriminator list on `InspectorMessage`.

### 4.2 `TreeResponse`: `Root` → `Roots`

```csharp
// Before
public sealed record TreeResponse : InspectorMessage
{
    public required ElementNode Root { get; init; }
}

// After
public sealed record TreeResponse : InspectorMessage
{
    public required List<ElementNode> Roots { get; init; }
}
```

Element `[0]` is always the `Window.Content` subtree. Additional elements are open popup subtrees, each with `TypeName = "[Popup]"` and `Name` set to the popup's `Name` property (if any).

### 4.3 `AgentInfo` and discovery file

`AgentInfo` gains `EventsPipeName: string`. `AgentDiscovery` writes this field to the discovery JSON. `AgentDiscoveryScanner` reads it. The events pipe name is always `{PipeName}-events`.

---

## 5. Agent Changes (`Snaipe.Agent`)

### 5.1 `AgentEventServer` (new)

Owns the server end of the events pipe. Lifecycle managed by `SnaipeAgent`.

```
AgentEventServer
  string PipeName                          // snaipe-{pid}-events
  Channel<InspectorMessage> _queue         // bounded, drop-oldest on overflow
  RunAsync(CancellationToken)              // background: wait for connect, write loop
  EnqueueEvent(InspectorMessage)           // called from PickModeManager (any thread)
```

- Waits for one client at a time; on disconnect, loops back to waiting
- Writes events using existing `MessageFraming.WriteMessageAsync`
- If `_queue` is full (inspector not reading fast enough), drops the oldest event — pick events are not worth blocking the UI thread

### 5.2 `PickModeManager` (new)

Created by `SnaipeAgent` after attach. Registered for the agent's lifetime.

```
PickModeManager(Window, ElementTracker, HighlightOverlay, AgentEventServer)
  Attach()    // registers AddHandler calls on Window.Content
  Detach()    // removes handlers (called from SnaipeAgent.Dispose)
```

**Handler registration:**

```csharp
_root.AddHandler(UIElement.PointerMovedEvent,  _movedHandler,  handledEventsToo: true);
_root.AddHandler(UIElement.PointerPressedEvent, _pressedHandler, handledEventsToo: true);
```

`_root` = `Window.Content` cast to `UIElement`.

**`OnPickPointerMoved` logic:**

```
1. isPickActive = (e.KeyModifiers & PickModifier) == PickModifier
2. If isPickActive != _wasPickActive:
     EnqueueEvent(new PickModeActiveEvent { Active = isPickActive })
     if !isPickActive: _highlight.SetHighlight(_lastId, false); return
   _wasPickActive = isPickActive
3. If !isPickActive: return
4. point = e.GetCurrentPoint(_root).Position
5. hits = VisualTreeHelper.FindElementsInHostCoordinates(point, _root)
6. element = hits.FirstOrDefault(IsPickable)   // see filter below
7. If element is null: return
8. id = _tracker.GetOrAssignId(element)
9. If id == _lastPickedId: return   // no-op, avoid spamming events
10. _lastPickedId = id
11. _highlight.SetHighlight(id, true)
12. EnqueueEvent(new ElementUnderCursorEvent { ElementId = id, TypeName = element.GetType().Name })
```

**`IsPickable` filter:**

```csharp
static bool IsPickable(UIElement e) =>
    e.Visibility == Visibility.Visible &&
    e is not Canvas { Tag: "SnaipeOverlay" };   // excludes the highlight canvas
```

The `HighlightOverlay` canvas is tagged `"SnaipeOverlay"` in `HighlightOverlay.EnsureOverlayInjected`.

**`OnPickPointerPressed` logic:**

```
1. If (e.KeyModifiers & PickModifier) != PickModifier: return
2. e.Handled = true   // suppress click reaching the app
```

**Modifier constant:**

```csharp
private const VirtualKeyModifiers PickModifier =
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;
```

### 5.3 `VisualTreeWalker` extension

New overload:

```csharp
public static List<ElementNode> BuildTree(
    UIElement windowContent, XamlRoot xamlRoot, ElementTracker tracker)
```

1. Builds the existing `windowContent` tree as before → element `[0]`
2. Calls `VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot)` to enumerate open popups
3. For each open `Popup` whose `Child` is not null: calls `Walk(popup.Child, ...)` and wraps in a node:
   ```
   TypeName = "[Popup]"
   Name     = popup.Name (may be empty)
   Children = [Walk(popup.Child)]
   ```
4. Returns the list

### 5.4 `SnaipeAgent` wiring

- Creates `AgentEventServer` at `Start()`, starts it on a background `Task`
- Creates `PickModeManager` and calls `Attach()` on the UI thread after `Window.Content` is set
- `HandleGetTree` calls `VisualTreeWalker.BuildTree(windowContent, windowContent.XamlRoot, _tracker)` and returns `new TreeResponse { Roots = roots }`
- `Dispose` calls `_pickMode.Detach()` and disposes `_eventServer`

---

## 6. Inspector Changes (`Snaipe.Inspector`)

### 6.1 `InspectorIpcClient` additions

```csharp
public event Action<InspectorMessage>? EventReceived;

public async Task ConnectEventsAsync(string eventsPipeName, CancellationToken ct)
```

- Opens `NamedPipeClientStream` with `PipeDirection.In` to `eventsPipeName`
- Connects with a 5-second timeout (non-fatal if agent events pipe not yet ready)
- Starts a background read loop: `MessageFraming.ReadMessageAsync` → raises `EventReceived`
- On any pipe error: logs and exits loop silently

Called from `ConnectAsync` immediately after the command pipe connects, using `AgentInfo.EventsPipeName`.

### 6.2 `MainViewModel` additions

In `ConnectAsync`, after successful command pipe connection:

```csharp
_client.EventReceived += OnAgentEventReceived;
await _client.ConnectEventsAsync(_selectedAgent.EventsPipeName, _cts.Token);
```

In `ClearSession`:

```csharp
_client.EventReceived -= OnAgentEventReceived;
```

**`OnAgentEventReceived` handler** (dispatched to UI thread):

```csharp
private void OnAgentEventReceived(InspectorMessage msg)
{
    DispatcherQueue.TryEnqueue(() =>
    {
        switch (msg)
        {
            case ElementUnderCursorEvent e:
                SelectNodeById(e.ElementId);
                StatusMessage = $"Picking: {e.TypeName}";
                break;
            case PickModeActiveEvent e:
                StatusMessage = e.Active
                    ? "Pick mode active — Ctrl+Shift + hover to select"
                    : $"Connected to {_selectedAgent?.DisplayName}";
                break;
        }
    });
}
```

**`SelectNodeById`** — DFS over `RootNodes` to find the matching `TreeNodeViewModel`, sets `SelectedNode`, raises `ScrollIntoViewRequested`. Does not trigger a new `LoadPropertiesAsync` call — `SelectedNode` setter already does that.

### 6.3 `FetchTreeAsync` update

```csharp
// Before
RootNodes.Add(BuildTreeNode(response.Root, expandedIds));

// After
foreach (var root in response.Roots)
    RootNodes.Add(BuildTreeNode(root, expandedIds));
```

---

## 7. File Map

| File | Change |
|---|---|
| `src/Snaipe.Protocol/Messages.cs` | Add `ElementUnderCursorEvent`, `PickModeActiveEvent`; change `TreeResponse.Root` → `Roots` |
| `src/Snaipe.Protocol/ProtocolJsonContext.cs` | Register new message types |
| `src/Snaipe.Agent/AgentEventServer.cs` | New — events push pipe server |
| `src/Snaipe.Agent/PickModeManager.cs` | New — pointer event hooks, hit test, click suppression |
| `src/Snaipe.Agent/VisualTreeWalker.cs` | Add `BuildTree(UIElement, XamlRoot, ElementTracker)` overload |
| `src/Snaipe.Agent/HighlightOverlay.cs` | Tag overlay canvas with `"SnaipeOverlay"` |
| `src/Snaipe.Agent/SnaipeAgent.cs` | Wire `AgentEventServer` and `PickModeManager`; update `HandleGetTree` |
| `src/Snaipe.Agent/AgentDiscovery.cs` | Write `EventsPipeName` to discovery file |
| `src/Snaipe.Inspector/InspectorIpcClient.cs` | Add `ConnectEventsAsync`, `EventReceived` |
| `src/Snaipe.Inspector/AgentDiscoveryScanner.cs` | Read `EventsPipeName` into `AgentInfo` |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Subscribe to events, `SelectNodeById`, status messages, `FetchTreeAsync` update |

---

## 8. Key Decisions

- **Push via second pipe, not multiplexed** — preserves existing request/response semantics; simpler to implement and reason about
- **Modifier detected from `PointerRoutedEventArgs.KeyModifiers`** — no separate `KeyDown`/`KeyUp` handlers; eliminates focus-order bugs
- **`AddHandler` with `handledEventsToo: true`** — single registration covers main window and all popups sharing the same `XamlRoot` event tree in Uno Skia
- **Click suppression via `e.Handled = true`** — standard WinUI/Uno mechanism; no overlay injection required
- **Popup roots as additional entries in `Roots` list** — inspector already supports multiple `RootNodes`; no UI changes needed
- **Events pipe errors are non-fatal** — pick mode is a developer convenience; a failed events connection should not break the core inspection workflow
