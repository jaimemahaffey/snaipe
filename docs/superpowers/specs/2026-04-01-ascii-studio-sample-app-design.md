# ASCII Studio Sample App — Design Spec

**Date:** 2026-04-01
**Status:** Approved

## Problem

The current Snaipe sample app is minimal — it doesn't exercise multi-window inspection, detachable panels, deep custom control hierarchies, or live DP value chains driven by real user interactions. Demonstrating and testing every inspector feature requires a richer target.

## Goal

Build **ASCII Studio**: a genuinely usable image-to-ASCII art converter that exercises every inspector feature — multiple windows, detachable panels, custom controls with rich visual states and style hierarchies, data binding throughout, and a live conversion pipeline.

## Scope

- New project `src/Snaipe.SampleApp` alongside the existing `src/` projects
- Target: `net9.0-windows` (same as rest of solution)
- Five custom controls, two windows, one modal dialog
- Full conversion pipeline: load → resize → pixel-to-char → dither → colorize → format output
- Image loading and processing via `SixLabors.ImageSharp` (no GDI dependency)
- No persistence or settings serialization in v1

---

## Architecture

### Approach

Shell + per-panel ViewModels sharing an immutable `ConversionState` record. A `ShellViewModel` owns layout state (docked vs floating panels, active window reference). Each panel has its own ViewModel. `ConversionPipeline` is a service owned by `ShellViewModel` that runs on a background `Task`.

### `ConversionState` record

Immutable snapshot passed between ViewModels:

```csharp
public sealed record ConversionState(
    string? ImagePath,
    ConversionSettings Settings,
    string? AsciiOutput,
    ConversionStatus Status,
    int ProgressPercent,
    string? ErrorMessage);

// Note: the loaded image is held internally by ConversionPipeline and disposed
// when a new conversion starts. It is not exposed on ConversionState.

public enum ConversionStatus { Idle, Converting, Done, Error }
```

### `ConversionSettings` record

```csharp
public sealed record ConversionSettings(
    CharacterSet CharacterSet,
    int OutputWidth,
    ColorMode ColorMode,
    DitheringAlgorithm Dithering,
    bool Invert);

public enum CharacterSet { Block, Classic, Braille, Minimal }
public enum ColorMode { Grayscale, Ansi16, Ansi256, TrueColorHtml }
public enum DitheringAlgorithm { None, FloydSteinberg, BayerOrdered }
```

---

## ViewModels

### `ShellViewModel`

Owns: `ConversionState` (as a property, set on each pipeline update), reference to the floating `AsciiPreviewWindow` (null when docked), `ConversionPipeline` instance, `IsDocked` bool.

Commands: `OpenImageCommand`, `TogglePreviewWindowCommand`, `ShowExportDialogCommand`.

Raises `PropertyChanged` on `ConversionState` — all panel VMs subscribe or are updated by Shell.

### `ImagePanelViewModel`

Properties: `SourceImage`, `ZoomLevel` (double), `PanOffset` (Point), `FilePath` (string).

Commands: `ZoomInCommand`, `ZoomOutCommand`, `ResetZoomCommand`.

### `AsciiOutputViewModel`

Properties: `AsciiOutput` (string), `ColorMode`, `FontSize` (double), `LineHeight` (double), `DisplayStatus` (mirrors `ConversionState.Status`), `ProgressPercent`.

### `ConversionSettingsViewModel`

Properties: `CharacterSet`, `OutputWidth` (int), `ColorMode`, `Dithering`, `Invert` (bool).

Any property change triggers a new conversion via `ShellViewModel.ConversionPipeline.StartAsync(settings)`.

### `ExportViewModel`

Properties: `SelectedFormat` (enum: PlainText, HtmlAnsi, ClipboardText), `DestinationPath` (string).

Commands: `BrowseCommand`, `ExportCommand`.

---

## Conversion Pipeline

`ConversionPipeline` runs on a background `Task`. Accepts `CancellationToken` — starting a new conversion cancels any in-flight one.

**Steps:**
1. **Load** — `Image.LoadAsync(path)` via ImageSharp
2. **Resize** — scale to `outputWidth` × `outputWidth * aspectCorrection` (ASCII chars are ~2:1 tall)
3. **Pixel-to-char map** — for each pixel, compute brightness (0–1), index into character set array
4. **Dithering** — Floyd-Steinberg: propagate quantization error to neighbors; Bayer: threshold map lookup
5. **Colorize** — for `Ansi16`/`Ansi256`: map pixel RGB to nearest palette entry, wrap chars in ANSI escape codes; for `TrueColorHtml`: wrap in `<span style="color:#rrggbb">` tags; for `Grayscale`: plain chars
6. **Format** — join rows with `\n`; for HTML wrap in `<pre>` block

Reports `IProgress<int>` (0–100) after each row. On completion sets `ConversionState.Status = Done` and `AsciiOutput`.

---

## Windows

### `MainWindow`

Layout:
```
┌─────────────────────────────────────────┐
│  [ToolbarButton × 6]  (left, vertical)  │
│  ┌──────────────┬──┬────────────────┐  │
│  │ ImagePreview  │⬤│  AsciiOutput  │  │
│  │   Control     │  │   Control     │  │
│  └──────────────┴──┴────────────────┘  │
│  [Status bar]                           │
└─────────────────────────────────────────┘
```

The center area is a `SplitPanel` (Orientation=Horizontal). The `AsciiOutputControl` pane has a detach button that calls `ShellViewModel.TogglePreviewWindowCommand`.

### `AsciiPreviewWindow`

A separate `Window` containing only `AsciiOutputControl`, bound to the same `AsciiOutputViewModel`. Opens when the user clicks the detach button; closing it re-docks. Can be moved to a second monitor.

### `ExportDialog` (`ContentDialog`)

Hosted in `MainWindow`. Contains format picker (`RadioButtons`), file path `TextBox` + browse `Button`, and Export/Cancel buttons. Bound to `ExportViewModel`.

---

## Custom Controls

### `AsciiOutputControl`

**Template:** `ScrollViewer` → `RichTextBlock` with colored `Run` elements (all color modes, including TrueColorHtml, render via inline `Run.Foreground` — no WebView2 dependency).

**Custom DPs:** `AsciiText` (string), `ColorMode` (enum), `FontSize` (double), `LineHeight` (double), `Status` (ConversionStatus).

**Visual state groups:**
- `DisplayStates`: Idle, Converting (shows `ProgressBar` overlay), Error (shows error `TextBlock`), Empty (shows drop-zone hint)

### `ToolbarButton`

**Template:** `Border` → `StackPanel` (icon `TextBlock` + label `TextBlock`).

**Custom DPs:** `Icon` (string), `Label` (string), `IsActive` (bool).

**Visual state groups:**
- `CommonStates`: Normal, PointerOver, Pressed, Disabled
- `ActiveStates`: Inactive, Active (blue background, white text)

### `ImagePreviewControl`

**Template:** `Grid` → `ScrollViewer` (with `ZoomMode=Enabled`) → `Image` with `RenderTransform` = `ScaleTransform` + `TranslateTransform`.

**Custom DPs:** `Source` (ImageSource), `ZoomLevel` (double, default 1.0), `PanOffset` (Point).

**Visual state groups:**
- `PanStates`: Idle, Panning (crosshair cursor), NothingLoaded (drop-zone overlay)

### `CharacterSetPicker`

**Template:** `ItemsControl` bound to a list of `CharacterSetOption` items, each rendered as a bordered card showing the set name and a 2-row sample preview string.

**Custom DPs:** `SelectedCharacterSet` (CharacterSet enum), `Items` (IReadOnlyList<CharacterSetOption>).

`CharacterSetOption` is defined as:
```csharp
public sealed record CharacterSetOption(CharacterSet Value, string Label, string PreviewSample);
```

Raises `SelectionChanged` routed event.

### `SplitPanel`

**Template:** `Grid` with two `ContentPresenter`s and a `GridSplitter` between them.

**Custom DPs:** `Orientation` (Orientation, default Horizontal), `SplitterThickness` (double, default 5), `Pane1MinSize` (double), `Pane2MinSize` (double), `Pane1Content` (object), `Pane2Content` (object).

Raises `SplitterMoved` routed event.

---

## Project Structure

```
src/Snaipe.SampleApp/
  App.xaml
  App.xaml.cs
  Windows/
    MainWindow.xaml
    MainWindow.xaml.cs
    AsciiPreviewWindow.xaml
    AsciiPreviewWindow.xaml.cs
  Controls/
    AsciiOutputControl.xaml
    AsciiOutputControl.xaml.cs
    ToolbarButton.xaml
    ToolbarButton.xaml.cs
    ImagePreviewControl.xaml
    ImagePreviewControl.xaml.cs
    CharacterSetPicker.xaml
    CharacterSetPicker.xaml.cs
    SplitPanel.xaml
    SplitPanel.xaml.cs
  ViewModels/
    ShellViewModel.cs
    ImagePanelViewModel.cs
    AsciiOutputViewModel.cs
    ConversionSettingsViewModel.cs
    ExportViewModel.cs
    ConversionState.cs
    ConversionSettings.cs
  Services/
    ConversionPipeline.cs
  Dialogs/
    ExportDialog.xaml
    ExportDialog.xaml.cs
  Snaipe.SampleApp.csproj
```

---

## Inspector Coverage

| Inspector Feature | How It Gets Exercised |
|---|---|
| Visual tree depth | `MainWindow` → `SplitPanel` → `ImagePreviewControl` → `ScrollViewer` → `Image` with transforms |
| Multiple windows | Inspector targets `MainWindow` or `AsciiPreviewWindow` independently |
| ControlTemplate visualization | `ToolbarButton`, `AsciiOutputControl`, `SplitPanel` all have non-trivial `ControlTemplate`s |
| ItemTemplate visualization | `CharacterSetPicker` uses `ItemTemplate` on its `ItemsControl` |
| DataContext drill-down | Each panel has its own VM; `ShellViewModel` → `ConversionSettingsViewModel` → `ConversionSettings` |
| DP value chain — Local | `ImagePreviewControl.ZoomLevel` set locally by zoom gesture |
| DP value chain — Binding | `AsciiOutputControl.ColorMode` bound to `ConversionSettingsViewModel.ColorMode` |
| DP value chain — VisualState | `ToolbarButton.Background` and `Foreground` set in 5 visual states |
| DP value chain — Style/BasedOn | `ToolbarButton` uses a style based on a shared `ButtonBase` style |
| DP value chain — Default only | Inner template elements with unset properties — no `?` button shown |
| Popups / dialogs | `ExportDialog` (`ContentDialog`), any toolbar `Flyout`s |
| Live property editing | Tweak `ZoomLevel`, `FontSize`, `SplitterThickness`, `OutputWidth` and watch live updates |

---

## Testing

No separate test project for the sample app. Verification is manual: run the app, open an image, convert it, exercise each feature listed in Inspector Coverage above.
