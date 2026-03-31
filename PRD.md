# DataContext Drill-Down — Ralph Loop Prompt

## Your Job

You are implementing the DataContext / ViewModel drill-down feature for the Snaipe visual tree inspector. Each time you run, find the next unchecked step in the plan, execute it, check it off, and commit. Stop after completing one logical step (one `- [ ]` item or one `### Task N` block if it contains only small steps).

## Context

- **Project:** Snaipe — a cross-platform visual tree inspector for Uno Platform desktop apps
- **Spec:** `docs/superpowers/specs/2026-03-31-datacontext-drilldown-design.md`
- **Plan:** `docs/superpowers/plans/2026-03-31-datacontext-drilldown.md`
- **Branch:** `feature/property-editor-feedback`
- **Target framework:** `net9.0-windows`

## How to Orient Yourself Each Iteration

1. Read `docs/superpowers/plans/2026-03-31-datacontext-drilldown.md`
2. Find the first unchecked step: `- [ ]`
3. Execute that step exactly as written — the plan contains complete code for every step
4. Check it off: change `- [ ]` to `- [x]`
5. Commit if the step says to commit

## Rules

- **Follow the plan exactly.** Do not improvise, refactor, or add features beyond what is specified.
- **One step at a time.** Complete one checkbox per iteration. Do not batch multiple steps.
- **TDD strictly.** Steps that say "write the failing test" come before steps that say "write the implementation." Do not skip ahead.
- **Do not modify the plan structure** — only change `[ ]` to `[x]` on steps you complete.
- **If a build or test step fails:** diagnose the error, fix it, re-run, then continue. Do not skip failing steps.

## Build and Test Commands

```bash
# Build Inspector project
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet

# Build Agent project
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet

# Run Inspector tests
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows

# Run Agent tests (created in Task 3)
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows

# Run a specific test class
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ObjectPropertyReaderTests"
```

## Completion

When all steps in the plan are checked off (`- [x]`) and all tests pass, output:

<promise>DATACONTEXT DRILLDOWN COMPLETE</promise>
