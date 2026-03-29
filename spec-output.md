# Snaipe — Technical Specification

## 1. Overview / Context

Snaipe is a cross-platform visual tree inspector for [Uno Platform](https://platform.uno/) desktop applications using the Skia renderer. It fills the same role as Snoop WPF or WPF Inspector, but targets Uno Skia Desktop on Windows and Linux.

The tool allows developers to browse a running Uno app's live visual tree, inspect and edit dependency properties, and visually highlight elements — all from a standalone inspector application connected over IPC.

### Current State

The project is in early proof-of-concept. The protocol layer (DTOs, message types) is complete. The agent can walk the visual tree and produce an `ElementNode` snapshot. The inspector and sample app are stubs. IPC transport, property inspection, and the inspector UI remain unimplemented.

## 2. Goals and Non-Goals

### Goals

1. **Live visual tree browsing** — display the full UI element hierarchy of a running Uno Skia Desktop app in a tree view, updated on demand or via polling.
2. **Property inspection and editing** — read dependency properties, attached properties, and binding expressions for any element; allow live edits that take effect immediately in the target app.
3. **Element highlighting** — hover an element in the inspector tree to overlay a highlight rectangle on the target app; pick an element from the target app to locate it in the inspector tree.
4. **Cross-platform** — work on both Windows (Skia.Wpf host) and Linux (Skia.X11 / Framebuffer host) with no platform-specific code paths in the protocol or agent logic.
5. **Low intrusion** — the agent library should add negligible overhead and must not block the target app's UI thread for longer than 5 ms during tree snapshots (leaving ample headroom within the ~16 ms frame budget).

### Non-Goals

- **Remote debugging over the network** — initial release targets localhost only (named pipes). TCP support may come later but is not in scope.
- **XAML hot-reload or live XAML editing** — Snaipe inspects the instantiated visual tree, not XAML source.
- **Performance profiling** — no frame-rate counters, GPU diagnostics, or allocation tracking.
- **Mobile / WebAssembly targets** — Uno runs on many platforms, but Snaipe targets only Skia Desktop.
- **Automated injection / attach-to-running-process** — the initial release requires the target app to reference `Snaipe.Agent` at build time and call `SnaipeAgent.Attach()`.
- **Push notifications / change subscriptions** — the inspector polls or refreshes on demand. Agent-initiated push of tree diffs is deferred to v2.

## 3. System Architecture

```
┌──────────────────────────┐              ┌───────────────────────────────┐
│   Snaipe.Inspector       │              │   Target Uno App              │
│   (Uno Skia Desktop)     │              │                               │
│                          │   Named      │   ┌───────────────────────┐   │
│  ┌────────────┐          │   Pipe       │   │   Snaipe.Agent        │   │
│  │ Tree View  │          │◄────────────►│   │                       │   │
│  ├────────────┤          │              │   │  VisualTreeWalker     │   │
│  │ Property   │          │   Snaipe     │   │  PropertyReader       │   │
│  │ Grid       │          │   .Protocol  │   │  PropertyWriter       │   │
│  ├────────────┤          │   (shared)   │   │  HighlightOverlay     │   │
│  │ Preview    │          │              │   │  ElementTracker       │   │
│  │ Pane       │          │              │   │  IPC Listener         │   │
│  └────────────┘          │              │   └───────────────────────┘   │
└──────────────────────────┘              └───────────────────────────────┘
```

### Component Responsibilities

| Component | Role |
|-----------|------|
| **Snaipe.Protocol** | Shared message records, `ElementNode` / `PropertyEntry` DTOs, JSON serialization helpers, error codes. No transport logic. |
| **Snaipe.Agent** | In-process library loaded into the target app. Walks the visual tree, reads/writes properties, manages a highlight overlay, tracks element identity, and hosts the IPC server (named pipe). |
| **Snaipe.Inspector** | Standalone Uno Skia Desktop app. Connects to the agent as an IPC client, renders tree view, property grid, and preview pane. Discovers agents via well-known discovery files. |
| **Snaipe.SampleApp** | Minimal Uno Skia Desktop app that references Snaipe.Agent and calls `SnaipeAgent.Attach()` on startup. Used for development and manual testing. |

## 4. Component Design

### 4.1 Snaipe.Protocol

#### Message Types

All messages inherit from `InspectorMessage`:

```csharp
public abstract record InspectorMessage(string MessageId);

// Requests (Inspector → Agent)
public record GetTreeRequest(string MessageId) : InspectorMessage(MessageId);
public record GetPropertiesRequest(string MessageId, string ElementId) : InspectorMessage(MessageId);
public record SetPropertyRequest(string MessageId, string ElementId, string PropertyName, string NewValue) : InspectorMessage(MessageId);
public record HighlightElementRequest(string MessageId, string ElementId, bool Show) : InspectorMessage(MessageId);

// Responses (Agent → Inspector)
public record TreeResponse(string MessageId, ElementNode Root) : InspectorMessage(MessageId);
public record PropertiesResponse(string MessageId, string ElementId, List<PropertyEntry> Properties) : InspectorMessage(MessageId);
public record AckResponse(string MessageId) : InspectorMessage(MessageId);
public record ErrorResponse(string MessageId, int ErrorCode, string Error, string? Details = null) : InspectorMessage(MessageId);
```

#### Error Codes

```csharp
public static class ErrorCodes
{
    public const int UnknownMessage = 1001;
    public const int ElementNotFound = 1002;
    public const int PropertyNotFound = 1003;
    public const int PropertyReadOnly = 1004;
    public const int InvalidPropertyValue = 1005;
    public const int TreeTruncated = 1006;
    public const int SerializationError = 1007;
    public const int InternalError = 1008;
    public const int PayloadTooLarge = 1009;
}
```

#### Data Models

```csharp
public record ElementNode(
    string Id,
    string TypeName,
    string? Name,
    BoundsInfo Bounds,
    List<PropertyEntry> Properties,  // Always empty in TreeResponse; populated via GetPropertiesRequest
    List<ElementNode> Children
);

public record BoundsInfo(double X, double Y, double Width, double Height);

public record PropertyEntry(
    string Name,
    string Category,       // "Layout", "Appearance", "Common", "Other"
    string ValueType,      // CLR type name, e.g. "Double", "SolidColorBrush"
    string Value,          // String-serialized value
    string ValueKind,      // Hint for Inspector editors: "String", "Number", "Boolean", "Color", "Thickness", "Enum"
    bool IsReadOnly,
    string? BindingExpression = null
);
```

The `ValueKind` field enables the Inspector to present type-appropriate editors (e.g., a color picker for `"Color"`, a checkbox for `"Boolean"`, a numeric stepper for `"Number"`). The mapping from CLR types to `ValueKind` is:

| ValueKind | CLR Types |
|-----------|-----------|
| `"String"` | `String` |
| `"Number"` | `Double`, `Single`, `Int32`, `Int64` |
| `"Boolean"` | `Boolean` |
| `"Color"` | `Color`, `SolidColorBrush` (serialized as hex `#AARRGGBB`) |
| `"Thickness"` | `Thickness` (serialized as `"left,top,right,bottom"`) |
| `"Enum"` | Any `Enum` type (serialized as member name) |
| `"Object"` | Everything else (serialized via `ToString()`) |

#### Serialization

Messages are serialized as length-prefixed JSON over the pipe:

```
[4 bytes: little-endian int32 payload length][UTF-8 JSON payload]
```

**Maximum payload size: 50 MB.** The reader rejects any length prefix exceeding this limit with `ErrorCodes.PayloadTooLarge`.

The JSON payload uses `System.Text.Json` with a `$type` discriminator for polymorphic deserialization:

```json
{
  "$type": "GetTreeRequest",
  "MessageId": "abc-123"
}
```

Use source-generated `JsonSerializerContext` for AOT compatibility and reduced reflection overhead:

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InspectorMessage))]
[JsonSerializable(typeof(GetTreeRequest))]
// ... all message types
internal partial class ProtocolJsonContext : JsonSerializerContext { }
```

#### Protocol Versioning

The protocol carries a version string `"1.0"` exposed in discovery files (see Section 4.4). The Inspector checks this version at connection time. If the major version differs, the Inspector shows a warning and refuses to connect. Minor version differences are tolerated (backward compatible).

### 4.2 Snaipe.Agent

#### Element ID Stability

Element IDs must be **stable across multiple `GetTreeRequest` calls** within the same session so the Inspector can maintain selection state, expanded nodes, and property grid focus after a tree refresh.

Strategy: use a `ConditionalWeakTable<UIElement, string>` to associate each element with a GUID on first encounter. This ensures:
- IDs persist as long as the element is alive, even if the tree is re-walked.
- IDs are not affected by sibling insertion/removal (unlike path-based approaches).
- GC'd elements are automatically removed (no manual cleanup needed for the table itself).

A secondary `ConcurrentDictionary<string, WeakReference<UIElement>>` (the `ElementTracker`) provides O(1) reverse lookup from ID → element for property and highlight requests. This dictionary is rebuilt on each tree walk. A periodic timer (every 60 seconds) sweeps dead `WeakReference` entries to prevent unbounded growth of stale keys.

#### Lifecycle

1. **`SnaipeAgent.Attach(Window window)`** — static factory. Stores a reference to the window's root `UIElement`, creates a `CancellationTokenSource`, writes the discovery file, and starts the IPC listener on a background thread.
2. The IPC listener accepts exactly **one client connection at a time**. If a second client attempts to connect while one is active, the new connection is rejected (the pipe is created with `maxNumberOfServerInstances: 1`).
3. On each incoming message, the agent dispatches to the appropriate handler on the UI thread (for tree walks and property access) or directly (for lightweight operations).
4. **`Dispose()`** — cancels the `CancellationTokenSource`, closes the pipe, removes the highlight overlay, deletes the discovery file.

#### IPC Server

```csharp
public class AgentIpcServer : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipe;

    public async Task RunAsync(
        Func<InspectorMessage, CancellationToken, Task<InspectorMessage>> handler,
        CancellationToken ct);
}
```

- **Pipe name convention**: `snaipe-{ProcessId}` (e.g., `snaipe-12345`).
- **Read loop**: read 4-byte little-endian length prefix → validate ≤ 50 MB → read payload → deserialize → dispatch to handler → serialize response → write length prefix + payload.
- **Per-request timeout**: 30 seconds. If the handler exceeds this, the request is cancelled and an `ErrorResponse` is returned.
- **Reconnection**: after a client disconnects, the server returns to waiting for a new connection. A 1-second delay before re-listening prevents spin-looping on repeated connect/disconnect.

#### VisualTreeWalker

- `BuildTree(UIElement root, ElementTracker tracker)` — recursively walks using `VisualTreeHelper.GetChildrenCount` / `GetChild`.
- **Bounds calculation**: use `element.TransformToVisual(root).TransformPoint(new Point(0, 0))` to get position relative to the window root. Use `element.ActualWidth` / `ActualHeight` for size. Catch exceptions from `TransformToVisual` (element may not be in the visual tree) and return `BoundsInfo(0, 0, 0, 0)`.
- **Truncation limits**: max tree depth of 100 levels, max 2000 children per node. Truncated branches include a synthetic `[Truncated: N children omitted]` node.

#### PropertyReader

Strategy:
1. Use reflection to enumerate all `DependencyProperty` fields on the element's type hierarchy (by convention they are `public static readonly` fields ending in `Property`). **Cache the results per type** in a `ConcurrentDictionary<Type, List<DependencyProperty>>`.
2. For each, call `element.GetValue(dp)` to get the current value.
3. Detect bindings via `element.ReadLocalValue(dp)` — if the result is a `BindingExpression`, capture `BindingExpression.ParentBinding.Path.Path`.
4. Categorize properties: `Layout` (Width, Height, Margin, Padding, Alignment), `Appearance` (Background, Foreground, Opacity, Visibility), `Common` (Name, DataContext, Tag, Content, Text), `Other` (everything else).
5. Determine `ValueKind` from the property's `PropertyType` using the mapping table in Section 4.1.
6. Format values: `null` → `"(null)"`, `NaN` → `"NaN"`, colors → hex `#AARRGGBB`, `Thickness` → `"left,top,right,bottom"`, enums → member name, everything else → `ToString()` with `InvariantCulture`.

All property reads **must execute on the UI thread** via `DispatcherQueue.TryEnqueue`.

#### PropertyWriter

- Locate the `DependencyProperty` by name using the same reflection scan as `PropertyReader`.
- **Validate before writing**: check that the property exists, is not read-only, and that `NewValue` can be parsed into the target type.
- Parse `NewValue` from string to the property's target type: `bool.Parse` for booleans, `double.Parse` with `InvariantCulture` for numbers, `Enum.Parse` for enums, hex → `Color` → `SolidColorBrush` for colors, CSV → `Thickness` for thickness, `TypeDescriptor.GetConverter` as fallback.
- Call `element.SetValue(dp, parsedValue)` **on the UI thread** via `DispatcherQueue.TryEnqueue`.
- On failure, return `ErrorResponse` with the appropriate error code (`PropertyNotFound`, `PropertyReadOnly`, `InvalidPropertyValue`).

#### HighlightOverlay

- On `HighlightElementRequest(Show=true)`, find the element by ID via `ElementTracker`.
- **Overlay approach**: inject a `Canvas` as the last child of `Window.Content`, set `IsHitTestVisible = false` and `HorizontalAlignment = Stretch`, `VerticalAlignment = Stretch`. Place a semi-transparent `Border` (fill: `#4000A0FF`, border: 2px `#FF00A0FF`) positioned to match the target element's bounds via `TransformToVisual`.
- **Fallback for non-Panel content**: if `Window.Content` is not a `Panel` (e.g., it's a `ContentControl` or leaf element), the agent wraps the existing content in a `Grid` and appends the overlay `Canvas` to that `Grid`. This ensures the overlay injection point always exists.
- On `Show=false` or when a different element is highlighted, reposition or hide the overlay.
- The overlay `Canvas` is created once and reused. `Dispose()` removes it from the visual tree.
- Respond with `AckResponse`.

### 4.3 Snaipe.Inspector

#### Host Initialization

```csharp
// Linux
var host = new X11ApplicationHost();
// Windows
var host = new WpfHost();

host.Run(() => new App());
```

The Inspector is a standard Uno Skia Desktop app. Its XAML structure:

```
MainWindow
├── ConnectionBar          (top bar: dropdown of discovered agents, Connect/Disconnect button)
├── SplitContainer
│   ├── TreeView           (left pane: element hierarchy, with VirtualizingStackPanel)
│   └── SplitContainer
│       ├── PropertyGrid   (right-top: properties of selected element)
│       └── PreviewPane    (right-bottom: placeholder for future visual preview)
```

#### IPC Client

```csharp
public class InspectorIpcClient : IDisposable
{
    public async Task ConnectAsync(string pipeName, CancellationToken ct);
    public async Task<TResponse> SendAsync<TResponse>(InspectorMessage request, CancellationToken ct)
        where TResponse : InspectorMessage;
    public void Disconnect();
}
```

- Wraps `NamedPipeClientStream`.
- Same length-prefixed JSON framing as the agent.
- **Timeouts**: 5 seconds for connection, 10 seconds per request.
- **Retry strategy**: on `IOException` / `TimeoutException`, the client does not auto-retry. It surfaces the error to the UI, which shows "Connection lost. Reconnect?" with a manual retry button.

#### Agent Discovery

The inspector scans the discovery directory (see Section 4.4) on startup and on a Refresh button press. For each `{pid}.json` file:
1. Parse the JSON to extract process name, window title, pipe name, and protocol version.
2. Verify the process is still running (`Process.GetProcessById(pid)`). Remove stale files for dead processes.
3. Check protocol version compatibility (major version must match).
4. Display in dropdown: `"ProcessName (PID pid) — WindowTitle"`.

#### Tree View

- On Connect, sends `GetTreeRequest`, receives `TreeResponse`, populates a `TreeView` control with `VirtualizingStackPanel` for performance with large trees.
- Each `TreeViewItem` displays `TypeName` and `Name` (if set): e.g., `Button "SubmitBtn"` or `StackPanel`.
- Selecting a node sends `GetPropertiesRequest` for that element.
- A "Refresh" button re-fetches the full tree. **The Inspector preserves expanded/selected state by matching element IDs** across refreshes.
- Future: auto-refresh on a configurable polling interval (e.g., 2 seconds). Not in initial release.

#### Property Grid

- Displays properties grouped by category (`Layout`, `Appearance`, `Common`, `Other`).
- Each row: property name, type, current value, binding expression (if any).
- Read-only properties shown as plain text; writable properties shown in editable fields.
- **Type-specific editors** based on `ValueKind`: color picker for `"Color"`, checkbox for `"Boolean"`, numeric input for `"Number"`, dropdown for `"Enum"`, text field for everything else.
- On edit, sends `SetPropertyRequest` and updates the displayed value on success, shows inline error on failure.

#### Preview Pane

- Placeholder in the initial release. Displays the selected element's bounds info, type name, and basic layout metrics.
- Future: render a bitmap snapshot of the element via `RenderTargetBitmap`.

### 4.4 Agent Discovery Protocol

Agents advertise their presence by creating a JSON file in a user-specific temporary directory:
- **Path**: `Path.GetTempPath() + "snaipe/" + {pid}.json` (cross-platform via .NET API)
- On Linux this resolves to a per-user path (e.g., `/tmp/snaipe/` or `$XDG_RUNTIME_DIR/snaipe/`). The directory is created with `0700` permissions to prevent other users from reading or writing.
- On Windows this resolves to `%TEMP%\snaipe\` which is already user-scoped.

**Discovery File Schema:**
```json
{
  "pid": 1234,
  "processName": "MyApp",
  "windowTitle": "Main Window",
  "pipeName": "snaipe-1234",
  "protocolVersion": "1.0",
  "agentVersion": "0.1.0",
  "startedAt": "2026-03-28T10:30:00Z"
}
```

**Cleanup**: The agent registers handlers for `AppDomain.ProcessExit` and `AssemblyLoadContext.Unloading` to delete its discovery file on shutdown. The inspector also cleans up stale files for dead processes when scanning (see Section 4.3).

### 4.5 Snaipe.SampleApp

A minimal Uno Skia Desktop app for testing. Should include a variety of controls:
- `StackPanel` with nested `Grid`
- `TextBlock`, `TextBox`, `Button`, `CheckBox`, `Slider`
- A `ListView` with data-bound items
- Elements with explicit `x:Name`, bindings, and attached properties

The sample app calls `SnaipeAgent.Attach(window)` after the window loads.

## 5. API Design

The API is the IPC message protocol (no HTTP endpoints). All contracts are defined in Section 4.1 above.

### Request / Response Summary

| Request | Response | Notes |
|---------|----------|-------|
| `GetTreeRequest` | `TreeResponse` | Properties list always empty in tree nodes |
| `GetPropertiesRequest` | `PropertiesResponse` | Returns all properties for a single element |
| `SetPropertyRequest` | `PropertiesResponse` | Returns refreshed property list on success |
| `SetPropertyRequest` | `ErrorResponse` | On failure (not found, read-only, invalid value) |
| `HighlightElementRequest` | `AckResponse` | Show/hide highlight overlay |
| Any unknown type | `ErrorResponse(1001)` | Unknown message type |

### Example: GetTree

```
→ { "$type": "GetTreeRequest", "messageId": "550e8400-e29b-41d4-a716-446655440000" }
← { "$type": "TreeResponse", "messageId": "550e8400-e29b-41d4-a716-446655440000",
     "root": {
       "id": "a1b2c3d4",
       "typeName": "Button",
       "name": "SubmitBtn",
       "bounds": { "x": 100.0, "y": 50.0, "width": 120.0, "height": 40.0 },
       "children": [ ... ],
       "properties": []
     }
   }
```

### Example: GetProperties

```
→ { "$type": "GetPropertiesRequest", "messageId": "<guid>", "elementId": "a1b2c3d4" }
← { "$type": "PropertiesResponse", "messageId": "<guid>", "elementId": "a1b2c3d4",
     "properties": [
       { "name": "Width", "category": "Layout", "valueType": "Double",
         "value": "120", "valueKind": "Number", "isReadOnly": false, "bindingExpression": null },
       { "name": "Background", "category": "Appearance", "valueType": "SolidColorBrush",
         "value": "#FF0078D4", "valueKind": "Color", "isReadOnly": false,
         "bindingExpression": "{Binding ThemeColor}" }
     ]
   }
```

### Example: SetProperty (failure)

```
→ { "$type": "SetPropertyRequest", "messageId": "<guid>", "elementId": "a1b2c3d4",
     "propertyName": "Width", "newValue": "notanumber" }
← { "$type": "ErrorResponse", "messageId": "<guid>", "errorCode": 1005,
     "error": "Invalid property value", "details": "Cannot parse 'notanumber' as Double" }
```

## 6. Data Models

No persistent storage. All data is ephemeral and in-memory:

- **Agent side**:
  - `ConditionalWeakTable<UIElement, string>` — stable ID assignment (GUID per element, auto-cleaned by GC).
  - `ConcurrentDictionary<string, WeakReference<UIElement>>` — reverse lookup from ID → element (rebuilt each tree walk, periodic sweep of dead entries).
  - `ConcurrentDictionary<Type, List<DependencyProperty>>` — reflection cache for property enumeration per type.
- **Inspector side**: in-memory tree of `ElementNode` objects mirroring the agent's snapshot.

## 7. Infrastructure Requirements

- **.NET 9 SDK** (specified in `global.json`).
- **Uno Platform 6.5.x NuGet packages** (already referenced in `.csproj` files).
- **No external services, databases, or cloud infrastructure.**
- Build: `dotnet build Snaipe.sln`
- Run inspector: `dotnet run --project src/Snaipe.Inspector`
- Run sample app: `dotnet run --project samples/Snaipe.SampleApp`

## 8. Security Considerations

### Threat Model

Snaipe is a **developer tool** intended for local use only. The threat model is limited:

| Threat | Mitigation |
|--------|-----------|
| **Unauthorized pipe connection** | Named pipes use OS-level access control. The pipe is created with the current user's identity; other users on the same machine cannot connect. On Windows, explicitly set `PipeSecurity` to current-user-only. |
| **Malicious property writes** | The agent runs in-process with the target app and has full access to the visual tree regardless. Snaipe does not elevate privileges. A connected inspector can only do what the app process itself can do. |
| **Pipe name collision / spoofing** | Pipe names include the PID. The inspector reads the PID from the discovery file and verifies the process is running. Low-risk for a local dev tool. |
| **Denial of service (large payloads)** | Maximum payload size enforced at 50 MB. Length prefix validated before allocating read buffer. |
| **Denial of service (large tree)** | Tree walk capped at depth 100 and 2000 children per node. Truncation nodes inserted when limits are hit. |

### Input Validation

- All incoming `ElementId` values checked against `ElementTracker` before use. Invalid IDs return `ErrorResponse(1002)`.
- All `PropertyName` values validated via reflection lookup. Unknown properties return `ErrorResponse(1003)`.
- All `NewValue` strings parsed and validated before `SetValue` is called. Unparseable values return `ErrorResponse(1005)`.
- JSON deserialization failures return `ErrorResponse(1007)`.

### Explicit Non-Threats

- Network attacks (no network listener in initial release).
- Injection of arbitrary code (the agent is a library, not a runtime injector).
- Data exfiltration (all data stays on localhost).

## 9. Error Handling Strategy

| Scenario | Error Code | Handling |
|----------|-----------|---------|
| Inspector cannot find any agents | — | Show "No Snaipe agents detected. Ensure the target app references Snaipe.Agent and calls SnaipeAgent.Attach()." in the connection bar. |
| Pipe connection refused / broken | — | Catch `IOException` / `TimeoutException`. Show "Connection lost. Reconnect?" with a retry button. Clear tree and property views. |
| Agent receives unknown message type | 1001 | Return `ErrorResponse`. Log a warning. |
| Element not found by ID (GC'd) | 1002 | Return `ErrorResponse`. Inspector prompts user to refresh the tree. |
| Property not found on element | 1003 | Return `ErrorResponse`. |
| Property is read-only | 1004 | Return `ErrorResponse`. Inspector shows inline error. |
| Property value cannot be parsed | 1005 | Return `ErrorResponse` with details. Inspector shows inline error. |
| Tree walk exceeds depth/child limits | 1006 | Truncate and add synthetic node. Return `TreeResponse` (not an error — tree is usable, just incomplete). |
| JSON serialization/deserialization failure | 1007 | Return `ErrorResponse`. Log the raw payload for debugging. |
| Payload exceeds 50 MB | 1009 | Reject before reading. Close connection. |
| Protocol version mismatch | — | Inspector detects at discovery time. Shows warning, disables Connect button for incompatible agents. |

## 10. Performance Requirements

This is a local developer tool. Performance targets are oriented around responsiveness:

| Operation | Target | Notes |
|-----------|--------|-------|
| Tree snapshot (agent, UI thread portion) | < 5 ms for structural walk | Walk the tree and collect element references on UI thread. Serialize to JSON on a background thread. The 5 ms target leaves ~11 ms of headroom within a 60 fps frame for the app's own rendering. |
| Tree snapshot (agent, total including serialization) | < 200 ms for 5,000 elements | Background thread serialization can take longer without affecting the target app. |
| Property fetch | < 20 ms | Single element, reflection-based. UI thread access required. |
| Property write | < 10 ms | Single `SetValue` call dispatched to UI thread. |
| Inspector tree render | < 200 ms for 5,000 nodes | Requires `VirtualizingStackPanel` on the `TreeView`. |
| Pipe round-trip latency | < 5 ms | Named pipes on localhost. |
| Memory overhead (agent) | < 10 MB | WeakReference dictionary + serialization buffers + reflection cache. |

### UI Thread Budget

The 5 ms budget for the UI thread portion of a tree walk means: walk the tree and populate `ElementNode` objects in memory, but defer JSON serialization to a background thread. To stay within 5 ms for large trees, `TransformToVisual` calls for bounds calculation should be **deferred by default** — the tree walk collects only structural information (type, name, children) and assigns stable IDs. Bounds are computed lazily when the Inspector requests properties for a specific element, or when a highlight is requested. This "lazy bounds" strategy is the default; only if profiling shows the full bounds walk fits within 5 ms for typical trees (< 2,000 elements) should it be done eagerly.

### Scalability Limits

Snaipe is not designed for apps with >50,000 visual tree elements. At that scale, tree serialization and inspector rendering may degrade. Document this as a known limitation.

## 11. Observability

### Logging

- **Agent**: Use `System.Diagnostics.Debug.WriteLine` (visible in IDE output window / debugger).
- **Inspector**: Use `Microsoft.Extensions.Logging.ILogger` for structured logging to console.
- Log levels:
  - `Debug`: message send/receive, serialization timing, element ID assignments.
  - `Information`: connection established/lost, tree snapshot size and timing, discovery file created/removed.
  - `Warning`: element not found, property write rejected, truncation triggered, stale discovery file cleaned up.
  - `Error`: pipe failures, serialization errors, unhandled exceptions in message handlers.

### Metrics

Not applicable for a local dev tool. No telemetry or metrics collection.

## 12. Testing Strategy

### Unit Tests (`Snaipe.Protocol.Tests`)

- Serialization round-trip: serialize each message type to JSON and deserialize back; verify equality.
- Polymorphic deserialization: verify `$type` discriminator resolves to correct record type.
- `ElementNode` tree construction and traversal.
- Payload size validation: verify messages exceeding 50 MB are rejected.
- Error code values are unique and within expected range.

### Unit Tests (`Snaipe.Agent.Tests`)

- `VisualTreeWalker.BuildTree` with mock `UIElement` hierarchies (requires Uno test host or an abstraction over `VisualTreeHelper`).
- **Element ID stability**: verify that the same `UIElement` gets the same ID across 10 consecutive `BuildTree` calls.
- `PropertyReader.GetProperties` with a test `DependencyObject` that has known properties. Verify categories, value formatting, and `ValueKind` assignment.
- `PropertyWriter.SetProperty` with writable and read-only properties. Verify read-only rejection.
- `ElementTracker` dead-reference cleanup.

### Integration Tests

- Full IPC round-trip: start an agent in-process, connect a client, send `GetTreeRequest`, verify response.
- Property edit round-trip: set a property and verify the value changed on the element.
- Error scenarios: request properties for a non-existent element ID, send malformed JSON, send oversized payload.
- Connection lifecycle: connect, disconnect, reconnect.
- Second client rejected while first is connected.

### Manual Testing

- Run `Snaipe.SampleApp` alongside `Snaipe.Inspector` and manually verify:
  - Agent appears in discovery dropdown with correct process name and window title.
  - Tree appears and matches the sample app's visual hierarchy.
  - Selecting a tree node loads its properties with correct categories and value kinds.
  - Editing a property (e.g., changing a `TextBlock.Text`) updates the sample app live.
  - Highlight overlay appears on the correct element and clears when deselected.
  - Tree refresh preserves selection and expanded state.

## 13. Deployment Strategy

Snaipe is a developer tool distributed as NuGet packages and a standalone executable:

- **`Snaipe.Protocol`** — NuGet package, referenced by both Agent and Inspector.
- **`Snaipe.Agent`** — NuGet package. Developers add it to their Uno app project and call `SnaipeAgent.Attach()`.
- **`Snaipe.Inspector`** — distributed as a self-contained single-file executable via `dotnet publish -c Release --self-contained -r linux-x64` (and `win-x64`). Alternatively, as a `dotnet tool` for global install.

No CI/CD pipeline defined yet. Future: GitHub Actions for build, test, NuGet pack, and release.

## 14. Migration Plan

Not applicable — greenfield project, no existing system to migrate from.

## 15. Open Questions / Future Considerations

1. **Attach-to-running-process without code changes** — investigate .NET profiler API or `AssemblyLoadContext` injection to attach the agent to an already-running Uno process without requiring a project reference.
2. **TCP transport** — for remote inspection (e.g., inspecting an app running in a VM or container). Would require adding authentication/TLS.
3. **Push notifications / change subscriptions** — instead of polling, the agent could push tree diffs when the visual tree changes (listen to `LayoutUpdated` or similar events). This requires a bidirectional communication model (agent pushes to inspector). Deferred to v2.
4. **Visual preview** — render a bitmap snapshot of the selected element or the entire window in the inspector's preview pane using `RenderTargetBitmap`.
5. **Search / filter** — allow searching the tree by type name, element name, or property value.
6. **XAML source mapping** — if debug symbols are available, map elements back to their XAML source file and line number.
7. **Plugin system** — allow third-party extensions to add custom property editors or visualizers.
8. **Multi-window support** — Uno Platform 6.x supports multiple windows. The current `Attach(Window)` signature binds the agent to a single window. Future versions could allow attaching to multiple windows, each with its own discovery entry, or a single agent managing multiple roots.
