# NuGet Packaging — Design Spec

**Date:** 2026-04-02  
**Status:** Approved

---

## Goal

Distribute Snaipe as two NuGet packages published to GitHub Packages, triggered automatically by git tags.

---

## Package Structure

Two packages are published. `Snaipe.Protocol` is not a separate package — it is bundled inside `Snaipe.Agent`.

| Package ID | Type | Description |
|---|---|---|
| `Snaipe.Agent` | Library | In-process agent; add to the target Uno app |
| `Snaipe` | dotnet global tool | Inspector UI; install once globally |

**User install story:**
```bash
# In the target app project
dotnet add package Snaipe.Agent

# Once, globally
dotnet tool install -g Snaipe
```

---

## Protocol Bundling

`Snaipe.Protocol` is an implementation detail of `Snaipe.Agent` and is not published separately.

In `Snaipe.Agent.csproj`, the existing `ProjectReference` to Protocol gains `<PrivateAssets>all</PrivateAssets>`. This causes MSBuild to:
- Include `Snaipe.Protocol.dll` in the Agent package's `lib/` folder
- Emit **no** NuGet dependency on a `Snaipe.Protocol` package

Protocol types remain visible to Agent consumers (they flow through the public API surface), but users never install or reference Protocol directly.

---

## Shared Metadata — `Directory.Build.props`

A `Directory.Build.props` at the repo root carries all common NuGet metadata:

```xml
<Project>
  <PropertyGroup>
    <Authors>Jaime Mahaffey</Authors>
    <RepositoryUrl>https://github.com/[owner]/snaipe</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <!-- Embed README.md in every packable project -->
  <ItemGroup Condition="'$(IsPackable)' == 'true'">
    <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

> **Note:** Replace `[owner]` in `RepositoryUrl` with the GitHub org or username before publishing.

`IsPackable` defaults to `false` so test projects and the sample app are never accidentally packed. Each packable project overrides it to `true`.

**Version** is not stored in `Directory.Build.props`. It is injected at pack time by the CI workflow from the git tag (see below), so there is no version to manually maintain in source.

---

## Per-Project Changes

### `Snaipe.Agent.csproj`

- Add `<IsPackable>true</IsPackable>`
- Add `<PackageId>Snaipe.Agent</PackageId>`
- Add `<PrivateAssets>all</PrivateAssets>` to the Protocol `ProjectReference`

### `Snaipe.Inspector.csproj`

- Add `<IsPackable>true</IsPackable>`
- Add `<PackageId>Snaipe</PackageId>`
- Add `<PackAsTool>true</PackAsTool>`
- Add `<ToolCommandName>snaipe</ToolCommandName>`

### `Snaipe.Protocol.csproj`

No changes needed. Protocol remains non-packable (inherits `IsPackable=false`).

---

## NuGet Source — `nuget.config`

A `nuget.config` at the repo root declares the GitHub Packages feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/[owner]/index.json" />
  </packageSources>
</configuration>
```

The `[owner]` placeholder is the GitHub org or username that owns the repo.

---

## GitHub Actions Workflow — `.github/workflows/publish.yml`

Triggers on pushed `v*` tags (e.g. `v0.1.0`).

**Runner:** `windows-latest` — required because `Snaipe.Inspector` targets `net9.0-windows`.

**Steps:**
1. Checkout
2. Setup .NET 9
3. Extract version from tag: strip the leading `v` from `GITHUB_REF_NAME`
4. `dotnet pack` `Snaipe.Agent.csproj` with `-c Release -p:Version=<tag-version>`
5. `dotnet pack` `Snaipe.Inspector.csproj` with `-c Release -p:Version=<tag-version> -f net9.0-windows`
6. `dotnet nuget push` both `.nupkg` files to the `github` source, authenticated via `GITHUB_TOKEN`

The tag drives versioning end-to-end. Both packages always ship the same version number, so the compatibility question ("which agent works with which inspector?") is answered by matching version numbers.

---

## Files Created or Modified

| File | Action |
|---|---|
| `Directory.Build.props` | Create |
| `nuget.config` | Create |
| `src/Snaipe.Agent/Snaipe.Agent.csproj` | Modify |
| `src/Snaipe.Inspector/Snaipe.Inspector.csproj` | Modify |
| `.github/workflows/publish.yml` | Create |

No source code changes required.

---

## Out of Scope

- Package signing / Authenticode
- Linux or macOS inspector builds
- `Snaipe.Protocol` as a public package
- NuGet.org publishing
