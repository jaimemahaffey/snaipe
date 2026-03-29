# Snaipe - Project State & Handoff

*This file acts as a universal memory and task tracker for AI assistants (Antigravity, Claude Code, Cursor, Copilot, etc.) working on the Snaipe repository. When starting a new session, ask the AI to read this file first. Before closing a session, ask the AI to update this file with its progress.*

---

## 🎯 Current Goal
Build a cross-platform visual tree inspector (like WPF Snoop) for Uno Platform desktop applications using the Skia renderer.

## 📈 Current Status
* **Stage:** Early exploration / proof of concept.
* **Working:** Protocol layer (DTOs, messages), the agent's ability to walk the visual tree and produce an `ElementNode` snapshot.
* **Pending:** IPC transport, full property inspection editing, highlighting, and the Inspector UI.

## 📝 Recent Progress
* Researched how to detect advanced XAML concepts within Uno/WinUI (Triggers, Styles, Templates, DataContext). 
* Specifically documented that `VisualStateManager` handles triggers, `Style.BasedOn` handles inheritance, templates are read from the visual tree root under a `ContentPresenter`, and `ReadLocalValue` determines DataContext inheritance.
* Saved implementation details to `docs/uno_ui_detection.md`.

## 🚧 Next Steps
- [ ] Incorporate the UI detection research (VisualState, DataContext) into the `PropertyReader` and `VisualTreeWalker` of `Snaipe.Agent`.
- [ ] Implement the IPC transport layer (Named Pipes) between Agent and Inspector.
- [ ] Scaffold the standalone `Snaipe.Inspector` UI.

## ❌ Failed Attempts / Lessons Learned
*(Agent: Log any dead-ends, library failures, or architectural pivots here so future agents don't make the same mistakes)*
* **None yet!**

## 🤖 Agent Instructions
1. Always check off items in the **Next Steps** checklist when completed.
2. If we pivot from a task, move it to **Failed Attempts** with a brief summary of why.
3. Keep the **Recent Progress** section concise (max 3-5 bullet points of the most recent work).
