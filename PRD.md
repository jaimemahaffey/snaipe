# ASCII Studio Sample App — Ralph Loop Prompt

## Your Job

You are implementing the ASCII Studio sample app for the Snaipe visual tree inspector. Each time you run, find the next unchecked step in the plan, execute it, check it off, and commit. Stop after completing one logical step (one `- [ ]` item or one `### Task N` block if it contains only small steps).

## Context

- **Project:** Snaipe — a cross-platform visual tree inspector for Uno Platform desktop apps
- **Spec:** `docs/superpowers/specs/2026-04-01-ascii-studio-sample-app-design.md`
- **Plan:** `docs/superpowers/plans/2026-04-01-ascii-studio-sample-app.md`
- **Branch:** `main`
- **Target framework:** `net9.0-windows`
- **Sample app location:** `samples/Snaipe.SampleApp/` (already in solution, already wired to `SnaipeAgent.Attach`)

## How to Orient Yourself Each Iteration

1. Read `docs/superpowers/plans/2026-04-01-ascii-studio-sample-app.md`
2. Find the first unchecked step: `- [ ]`
3. Execute that step exactly as written — the plan contains complete code for every step
4. Check it off: change `- [ ]` to `- [x]`
5. Commit if the step says to commit

## Rules

- **Follow the plan exactly.** Do not improvise, refactor, or add features beyond what is specified.
- **One step at a time.** Complete one checkbox per iteration. Do not batch multiple steps.
- **No TDD for this project.** There is no test project. Verification is build + manual run. Steps say "Build to verify" — run those commands and fix any errors before marking the step done.
- **Do not modify the plan structure** — only change `[ ]` to `[x]` on steps you complete.
- **If a build step fails:** read the error, apply the fix described in the plan's notes (or diagnose inline), re-run, then continue. Do not skip failing steps.
- **Known issues to watch for** (documented in plan Self-Review Notes):
  - `System.Web.HttpUtility` may not be available — use the inline `HtmlEncode` helper described in Task 10
  - `AsyncRelayCommand.ExecuteAsync()` must be added in Task 13
  - Remove the dummy `ZoomLevelProperty.GetMetadata(...)` line if it causes a compile error in Task 11

## Build and Test Commands

```bash
# Build the sample app
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet

# Restore NuGet (after csproj changes)
dotnet restore samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj

# Run the app
dotnet run --project samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows
```

## Completion

When all steps in the plan are checked off (`- [x]`) and the app builds and runs cleanly, output:

<promise>ASCII STUDIO COMPLETE</promise>
