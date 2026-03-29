# Snaipe

A cross-platform visual tree inspector for [Uno Platform](https://platform.uno/) desktop applications using the Skia renderer. Think [Snoop WPF](https://github.com/snoopwpf/snoopwpf) or [WPF Inspector](https://wpfinspector.codeplex.com/), but for Uno Desktop targets on Windows and Linux.

## Goals

- **Visual tree inspection** — browse the live UI element tree of a running Uno Skia Desktop app
- **Property viewer** — inspect and live-edit dependency properties, attached properties, and bindings
- **Element highlighting** — hover an element in the tree to highlight it in the running app (and vice-versa: pick from the app to locate in the tree)
- **Cross-platform** — works on both Windows and Linux (the two Uno Skia Desktop targets)
- **Low intrusion** — inject or attach to a running process with minimal impact on the target app

## Architecture (planned)

```
┌─────────────────────┐         ┌──────────────────────┐
│   Snaipe Inspector   │  TCP/  │   Target Uno App      │
│   (standalone app)   │◄──────►│   + Snaipe.Agent      │
│   Uno Skia Desktop   │  pipe  │   (injected library)  │
└─────────────────────┘         └──────────────────────┘
```

**Snaipe.Agent** — a small library referenced by (or injected into) the target app. Walks the Uno visual tree via `Microsoft.UI.Xaml.VisualTreeHelper`, serializes the tree and property data, and listens for commands (highlight, set property, etc.).

**Snaipe.Inspector** — a standalone Uno Skia Desktop app that connects to the agent, renders the tree view, property grid, and preview pane.

**Snaipe.Protocol** — shared DTOs and communication contract between agent and inspector.

## Tech Stack

- .NET 9
- Uno Platform 6.x (Skia Desktop — Gtk / X11 / Framebuffer)
- System.IO.Pipelines / named pipes or TCP for IPC

## Project Structure

```
src/
  Snaipe.Agent/          # Library injected into / referenced by target app
  Snaipe.Inspector/      # Standalone inspector UI (Uno Skia Desktop)
  Snaipe.Protocol/       # Shared models and serialization
samples/
  Snaipe.SampleApp/      # Minimal Uno app for testing the inspector
```

## Status

Early exploration / proof of concept.

## License

MIT
