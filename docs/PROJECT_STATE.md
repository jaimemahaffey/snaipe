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
* Saved implementation details to `docs/uno_ui_detection.md`.
* Scaffolded the standalone `Snaipe.Inspector` UI (`MainWindow`) and `AgentDiscoveryScanner`.
* Scaffolded `InspectorIpcClient` for Named Pipes transport.
* Fixed C# compilation errors (missing `using System.IO;`) and `.csproj` misconfigurations (`net9.0-windows`) to successfully build both projects on Windows.

## 🚧 Next Steps
- [ ] Incorporate the UI detection research (VisualState, DataContext) into the `PropertyReader` and `VisualTreeWalker` of `Snaipe.Agent`.
- [x] Implement the IPC transport layer (Named Pipes) between Agent and Inspector.
- [x] Scaffold the standalone `Snaipe.Inspector` UI.
- [ ] Test integration between Agent and Inspector by launching the SampleApp and attaching Inspector.

## ❌ Failed Attempts / Lessons Learned
*(Agent: Log any dead-ends, library failures, or architectural pivots here so future agents don't make the same mistakes)*
* **None yet!**

## 🤖 Agent Instructions
1. Always check off items in the **Next Steps** checklist when completed.
2. If we pivot from a task, move it to **Failed Attempts** with a brief summary of why.
3. Keep the **Recent Progress** section concise (max 3-5 bullet points of the most recent work).
