# Snaipe - Project State & Handoff

*This file acts as a universal memory and task tracker for AI assistants (Antigravity, Claude Code, Cursor, Copilot, etc.) working on the Snaipe repository. When starting a new session, ask the AI to read this file first. Before closing a session, ask the AI to update this file with its progress.*

---

## 🎯 Current Goal
Build a cross-platform visual tree inspector (like WPF Snoop) for Uno Platform desktop applications using the Skia renderer.

## 📈 Current Status
* **Stage:** Feature-complete MVP.
* **Working:** Full connect→tree→properties→edit→disconnect loop. Inspector UI (XAML + MVVM). SampleApp with diverse control set.
* **Pending:** Color picker editor, enum dropdown editor, visual preview (RenderTargetBitmap), Linux target.

## 📝 Recent Progress
* Migrated the simulated ListView property grid to use `CommunityToolkit.WinUI.UI.Controls.DataGrid`.
* Implemented native Category grouping using a custom `PropertyCategoryGroup` observable collection.
* Verified full integration loop: connect, tree, select, properties, edit, disconnect, reconnect.

## 🚧 Next Steps
- [ ] Color picker editor for `"Color"` ValueKind
- [ ] Enum dropdown editor (EnumValues already available in PropertyEntry)
- [ ] Visual preview via RenderTargetBitmap
- [ ] Linux (X11) target parity
- [ ] Manual integration smoke test (launch SampleApp + Inspector, walk connect→tree→select→edit→disconnect loop)

## ❌ Failed Attempts / Lessons Learned
*(Agent: Log any dead-ends, library failures, or architectural pivots here so future agents don't make the same mistakes)*

* **GridSplitter not available in WinUI/Uno without Community Toolkit** — replaced with `Border` dividers in MainWindow.xaml. The UI is non-resizable but functional.
* **Window has no DataContext property in WinUI/Uno** — `Window` is not a `FrameworkElement`. Set `DataContext` on `Content as FrameworkElement` after `InitializeComponent()` to propagate to child UserControls.
* **XAML files are NOT auto-discovered in Uno projects** — must be registered as explicit `<Page>` items in `.csproj` (with `SubType=Designer` and `Generator=MSBuild:Compile`).
* **HierarchicalDataTemplate does not exist in WinUI 3/Uno** — was a UWP/WPF concept. Use `TreeViewNode` objects populated in code-behind instead.
* **UpdateSourceTrigger.LostFocus is not implemented in Uno** — produces Uno0001 warning; binding still works for Windows target. Use `{Binding}` for non-Windows targets.
* **x:Bind TwoWay on ComboBox.SelectedItem fails** — `SelectedItem` is typed `object`, so x:Bind type-safety rejects string assignment. Use `{Binding SelectedItem, Mode=TwoWay}` instead.

## 🤖 Agent Instructions
1. Always check off items in the **Next Steps** checklist when completed.
2. If we pivot from a task, move it to **Failed Attempts** with a brief summary of why.
3. Keep the **Recent Progress** section concise (max 3-5 bullet points of the most recent work).
4. Read `docs/superpowers/specs/2026-03-29-inspector-ui-sampleapp-integration-design.md` for full architecture context.
