# NuGet Packaging — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Distribute `Snaipe.Agent` as a NuGet library and `Snaipe.Inspector` as a dotnet global tool, both published to GitHub Packages on `v*` tag push.

**Architecture:** `Directory.Build.props` carries shared metadata; each packable project opts in with `<IsPackable>true</IsPackable>`. `Snaipe.Protocol` is bundled inside `Snaipe.Agent` via `<PrivateAssets>all</PrivateAssets>` on its `ProjectReference` — it is never a separate published package. A single GitHub Actions workflow on `windows-latest` packs both projects and pushes them, with version injected from the git tag.

**Tech Stack:** .NET 9, MSBuild NuGet packing, GitHub Actions, GitHub Packages (`nuget.pkg.github.com`)

---

## File Map

| File | Action |
|---|---|
| `Directory.Build.props` | Modify — add NuGet metadata and README embed |
| `nuget.config` | Create — declare nuget.org + GitHub Packages sources |
| `src/Snaipe.Agent/Snaipe.Agent.csproj` | Modify — opt in to packing, bundle Protocol |
| `src/Snaipe.Inspector/Snaipe.Inspector.csproj` | Modify — opt in as dotnet global tool |
| `.github/workflows/publish.yml` | Create — tag-triggered pack + push workflow |

---

### Task 1: Extend Directory.Build.props with NuGet metadata

**Files:**
- Modify: `Directory.Build.props`

- [ ] **Step 1: Replace Directory.Build.props content**

Replace the entire file with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>

    <!-- NuGet packaging defaults -->
    <Authors>Jaime Mahaffey</Authors>
    <RepositoryUrl>https://github.com/jaimemahaffey/snaipe</RepositoryUrl>
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

- [ ] **Step 2: Verify the solution still builds**

```bash
dotnet build Snaipe.sln -f net9.0-windows -v quiet
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Directory.Build.props
git commit -m "chore: add NuGet metadata to Directory.Build.props"
```

---

### Task 2: Configure Snaipe.Agent for packing (with Protocol bundled)

**Files:**
- Modify: `src/Snaipe.Agent/Snaipe.Agent.csproj`

- [ ] **Step 1: Update Snaipe.Agent.csproj**

Replace the entire file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net9.0;net9.0-windows</TargetFrameworks>
    <RootNamespace>Snaipe.Agent</RootNamespace>
    <Description>In-process agent that walks the Uno visual tree and serves data to the inspector</Description>
    <IsPackable>true</IsPackable>
    <PackageId>Snaipe.Agent</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Snaipe.Protocol\Snaipe.Protocol.csproj">
      <PrivateAssets>all</PrivateAssets>
    </ProjectReference>
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>Snaipe.Agent.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Uno.WinUI" Version="6.5.153">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Pack and verify Protocol is bundled**

```bash
dotnet pack src/Snaipe.Agent/Snaipe.Agent.csproj -c Release -p:Version=0.0.1-verify -o ./nupkg-verify -f net9.0-windows -v quiet
```

Then inspect the package contents:

```bash
unzip -l ./nupkg-verify/Snaipe.Agent.0.0.1-verify.nupkg
```

Expected output must include:
- `lib/net9.0-windows/Snaipe.Agent.dll`
- `lib/net9.0-windows/Snaipe.Protocol.dll`  ← Protocol bundled here

And the .nuspec inside must NOT list `Snaipe.Protocol` as a `<dependency>`:

```bash
unzip -p ./nupkg-verify/Snaipe.Agent.0.0.1-verify.nupkg "*.nuspec"
```

Expected: No `<dependency id="Snaipe.Protocol" .../>` line.

- [ ] **Step 3: Clean up test output**

```bash
rm -rf ./nupkg-verify
```

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Agent/Snaipe.Agent.csproj
git commit -m "feat(packaging): configure Snaipe.Agent as NuGet package with bundled Protocol"
```

---

### Task 3: Configure Snaipe.Inspector as a dotnet global tool

**Files:**
- Modify: `src/Snaipe.Inspector/Snaipe.Inspector.csproj`

- [ ] **Step 1: Update Snaipe.Inspector.csproj**

Replace the entire file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Snaipe.Inspector</RootNamespace>
    <Description>Standalone visual tree inspector UI for Uno Skia Desktop apps</Description>
    <IsPackable>true</IsPackable>
    <PackageId>Snaipe</PackageId>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>snaipe</ToolCommandName>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
    <OutputType>WinExe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Snaipe.Protocol\Snaipe.Protocol.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Uno.WinUI" Version="6.5.153" />
    <PackageReference Include="Uno.WinUI.Skia.X11" Version="6.5.153" />
  </ItemGroup>

  <!-- Windows-only: native Win32 Skia host (Uno 6+) -->
  <ItemGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
    <PackageReference Include="Uno.WinUI.Runtime.Skia.Win32" Version="6.5.153" />
  </ItemGroup>

  <!-- XAML pages and controls -->
  <ItemGroup>
    <Page Include="MainWindow.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\ConnectionBarControl.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\ElementTreeControl.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\PropertyGridControl.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\PreviewPaneControl.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Pack and verify tool package**

```bash
dotnet pack src/Snaipe.Inspector/Snaipe.Inspector.csproj -c Release -p:Version=0.0.1-verify -o ./nupkg-verify -v quiet
```

Then inspect:

```bash
unzip -l ./nupkg-verify/Snaipe.0.0.1-verify.nupkg
```

Expected output must include:
- `tools/net9.0/any/Snaipe.Inspector.dll` (or similar path under `tools/`)
- `tools/net9.0/any/DotnetToolSettings.xml`

- [ ] **Step 3: Clean up test output**

```bash
rm -rf ./nupkg-verify
```

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/Snaipe.Inspector.csproj
git commit -m "feat(packaging): configure Snaipe.Inspector as dotnet global tool"
```

---

### Task 4: Add nuget.config

**Files:**
- Create: `nuget.config`

- [ ] **Step 1: Create nuget.config**

Create `nuget.config` at the repo root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github" value="https://nuget.pkg.github.com/jaimemahaffey/index.json" />
  </packageSources>
</configuration>
```

> **Important:** `nuget.org` must be listed explicitly. Adding a `nuget.config` disables the implicit nuget.org source — omitting it would break package restore for all Uno dependencies.

- [ ] **Step 2: Verify restore still works**

```bash
dotnet restore Snaipe.sln -v quiet
```

Expected: All packages restored successfully, no errors.

- [ ] **Step 3: Commit**

```bash
git add nuget.config
git commit -m "chore: add nuget.config with nuget.org and GitHub Packages sources"
```

---

### Task 5: Create GitHub Actions publish workflow

**Files:**
- Create: `.github/workflows/publish.yml`

- [ ] **Step 1: Create the workflows directory and publish.yml**

Create `.github/workflows/publish.yml`:

```yaml
name: Publish NuGet Packages

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Extract version from tag
        id: version
        shell: bash
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

      - name: Pack Snaipe.Agent
        run: dotnet pack src/Snaipe.Agent/Snaipe.Agent.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkg

      - name: Pack Snaipe (Inspector tool)
        run: dotnet pack src/Snaipe.Inspector/Snaipe.Inspector.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }} -f net9.0-windows -o ./nupkg

      - name: Push to GitHub Packages
        run: dotnet nuget push ./nupkg/*.nupkg --source github --api-key ${{ secrets.GITHUB_TOKEN }}
```

- [ ] **Step 2: Verify workflow file is valid YAML**

```bash
python3 -c "import yaml, sys; yaml.safe_load(open('.github/workflows/publish.yml')); print('YAML valid')" 2>/dev/null || echo "python3 not available — inspect manually"
```

Expected: `YAML valid` (or inspect the file manually if Python is unavailable).

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/publish.yml
git commit -m "ci: add GitHub Actions workflow to publish NuGet packages on version tags"
```

---

## Publishing a Release

Once all tasks are complete, trigger a publish by pushing a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow will run on `windows-latest`, pack both packages at version `0.1.0`, and push them to `https://nuget.pkg.github.com/jaimemahaffey/index.json`.

**Consumers install with:**
```bash
dotnet add package Snaipe.Agent          # in their app project
dotnet tool install -g Snaipe            # once globally
```

---

## Self-Review Notes

- `nuget.org` is explicitly included in `nuget.config` to avoid breaking restore when the file is added.
- `<PrivateAssets>all</PrivateAssets>` on the Protocol `ProjectReference` in Agent causes MSBuild to copy Protocol.dll into the package's `lib/` folder without emitting a NuGet dependency — consumers get the Protocol types but never see a separate Protocol package.
- The Inspector's `WinExe` output type is compatible with `PackAsTool=true`; the tool host doesn't require a console window.
- Version flows entirely from the git tag — no source file needs to be edited to cut a release.
