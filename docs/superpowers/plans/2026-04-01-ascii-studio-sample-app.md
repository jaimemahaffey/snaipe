# ASCII Studio Sample App — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the minimal Snaipe sample app with ASCII Studio — a full-featured image-to-ASCII art converter that exercises every inspector feature.

**Architecture:** Shell + per-panel ViewModels sharing an immutable `ConversionState` record. `ConversionPipeline` runs on a background `Task`. Four custom `Control` subclasses with `ControlTemplate`s in `Themes/Generic.xaml`; one `UserControl` for image preview. Existing `SnaipeAgent.Attach` wiring is preserved.

**Tech Stack:** C# 13, .NET 9, Uno Platform 6.5 (WinUI), SixLabors.ImageSharp 3.1.5

---

## File Map

| File | Action |
|---|---|
| `samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj` | Modify — add ImageSharp, new Page entries, remove old |
| `samples/Snaipe.SampleApp/App.cs` | Modify — open AsciiPreviewWindow alongside MainWindow |
| `samples/Snaipe.SampleApp/MainWindow.xaml` | Replace — full ASCII Studio layout |
| `samples/Snaipe.SampleApp/MainWindow.xaml.cs` | Replace |
| `samples/Snaipe.SampleApp/SampleViewModel.cs` | Delete |
| `samples/Snaipe.SampleApp/ViewModels/ViewModelBase.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/RelayCommand.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/AsyncRelayCommand.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/ConversionSettings.cs` | Create — enums + settings record |
| `samples/Snaipe.SampleApp/ViewModels/AsciiDocument.cs` | Create — AsciiLine/AsciiSpan records |
| `samples/Snaipe.SampleApp/ViewModels/ConversionState.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/ShellViewModel.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/ImagePanelViewModel.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/AsciiOutputViewModel.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/ConversionSettingsViewModel.cs` | Create |
| `samples/Snaipe.SampleApp/ViewModels/ExportViewModel.cs` | Create |
| `samples/Snaipe.SampleApp/Services/ConversionPipeline.cs` | Create |
| `samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml` | Create — styles for all 4 Control subclasses |
| `samples/Snaipe.SampleApp/Controls/SplitPanel.cs` | Create |
| `samples/Snaipe.SampleApp/Controls/ToolbarButton.cs` | Create |
| `samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml` | Create |
| `samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml.cs` | Create |
| `samples/Snaipe.SampleApp/Controls/AsciiOutputControl.cs` | Create |
| `samples/Snaipe.SampleApp/Controls/CharacterSetPicker.cs` | Create |
| `samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml` | Create |
| `samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml.cs` | Create |
| `samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml` | Create |
| `samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml.cs` | Create |

---

### Task 1: Project scaffold — csproj, directories, delete old files

**Files:**
- Modify: `samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj`
- Delete: `samples/Snaipe.SampleApp/SampleViewModel.cs`

- [ ] **Step 1: Delete the old sample ViewModel**

```bash
rm samples/Snaipe.SampleApp/SampleViewModel.cs
```

- [ ] **Step 2: Create directory structure**

```bash
mkdir -p samples/Snaipe.SampleApp/ViewModels
mkdir -p samples/Snaipe.SampleApp/Services
mkdir -p samples/Snaipe.SampleApp/Controls/Themes
mkdir -p samples/Snaipe.SampleApp/Windows
mkdir -p samples/Snaipe.SampleApp/Dialogs
```

- [ ] **Step 3: Replace csproj**

```xml
<!-- samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Snaipe.SampleApp</RootNamespace>
    <Description>ASCII Studio — image to ASCII art converter (Snaipe inspector sample app)</Description>
  </PropertyGroup>

  <PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Snaipe.Agent\Snaipe.Agent.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Uno.WinUI" Version="6.5.153" />
    <PackageReference Include="Uno.WinUI.Skia.X11" Version="6.5.153" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
    <PackageReference Include="Uno.WinUI.Runtime.Skia.Win32" Version="6.5.153" />
  </ItemGroup>

  <ItemGroup>
    <Page Include="MainWindow.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\Themes\Generic.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Controls\ImagePreviewControl.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Windows\AsciiPreviewWindow.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
    <Page Include="Dialogs\ExportDialog.xaml">
      <SubType>Designer</SubType>
      <Generator>MSBuild:Compile</Generator>
    </Page>
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Verify NuGet restore**

```bash
dotnet restore samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj
```

Expected: no errors, ImageSharp listed in output.

- [ ] **Step 5: Commit**

```bash
git add samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj
git rm samples/Snaipe.SampleApp/SampleViewModel.cs
git commit -m "chore: scaffold ASCII Studio project structure"
```

---

### Task 2: MVVM infrastructure

**Files:**
- Create: `samples/Snaipe.SampleApp/ViewModels/ViewModelBase.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/RelayCommand.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/AsyncRelayCommand.cs`

- [ ] **Step 1: Create ViewModelBase**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ViewModelBase.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Snaipe.SampleApp.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 2: Create RelayCommand**

```csharp
// samples/Snaipe.SampleApp/ViewModels/RelayCommand.cs
using System.Windows.Input;

namespace Snaipe.SampleApp.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter is T t ? t : default) ?? true;
    public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 3: Create AsyncRelayCommand**

```csharp
// samples/Snaipe.SampleApp/ViewModels/AsyncRelayCommand.cs
using System.Windows.Input;

namespace Snaipe.SampleApp.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private volatile bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try { await _execute(); }
        finally { _isExecuting = false; RaiseCanExecuteChanged(); }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: build succeeds (will warn about missing MainWindow since we haven't replaced it yet — that's fine).

- [ ] **Step 5: Commit**

```bash
git add samples/Snaipe.SampleApp/ViewModels/
git commit -m "feat(sample): add MVVM infrastructure (ViewModelBase, RelayCommand, AsyncRelayCommand)"
```

---

### Task 3: Data models

**Files:**
- Create: `samples/Snaipe.SampleApp/ViewModels/ConversionSettings.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/AsciiDocument.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/ConversionState.cs`

- [ ] **Step 1: Create ConversionSettings**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ConversionSettings.cs
namespace Snaipe.SampleApp.ViewModels;

public enum CharacterSet { Block, Classic, Braille, Minimal }
public enum ColorMode { Grayscale, Ansi16, Ansi256, TrueColor }
public enum DitheringAlgorithm { None, FloydSteinberg, BayerOrdered }

public sealed record ConversionSettings(
    CharacterSet CharacterSet,
    int OutputWidth,
    ColorMode ColorMode,
    DitheringAlgorithm Dithering,
    bool Invert)
{
    public static ConversionSettings Default => new(
        CharacterSet.Block,
        OutputWidth: 120,
        ColorMode.Grayscale,
        DitheringAlgorithm.None,
        Invert: false);

    // Character arrays indexed by brightness 0–N (darkest to lightest)
    public static readonly string[] BlockChars  = [" ", "░", "▒", "▓", "█"];
    public static readonly string[] ClassicChars = [" ", ".", ":", "-", "=", "+", "*", "#", "%", "@"];
    public static readonly string[] BrailleChars = [" ", "⠂", "⠒", "⠶", "⣤", "⣶", "⣾", "⣿"];
    public static readonly string[] MinimalChars = [" ", ".", ":", "-", "=", "+", "*", "#"];

    public string[] GetChars() => CharacterSet switch
    {
        CharacterSet.Block   => BlockChars,
        CharacterSet.Classic => ClassicChars,
        CharacterSet.Braille => BrailleChars,
        CharacterSet.Minimal => MinimalChars,
        _ => BlockChars
    };
}
```

- [ ] **Step 2: Create AsciiDocument**

```csharp
// samples/Snaipe.SampleApp/ViewModels/AsciiDocument.cs
using Windows.UI;

namespace Snaipe.SampleApp.ViewModels;

/// <summary>One colored run of characters on a single row.</summary>
public sealed record AsciiSpan(string Text, Color? Color);

/// <summary>One row of the ASCII output, made of colored spans.</summary>
public sealed record AsciiLine(IReadOnlyList<AsciiSpan> Spans);

/// <summary>The full structured output of a conversion.</summary>
public sealed record AsciiDocument(IReadOnlyList<AsciiLine> Lines)
{
    /// <summary>Plain text rendering — strips all color information.</summary>
    public string ToPlainText() =>
        string.Join("\n", Lines.Select(l => string.Concat(l.Spans.Select(s => s.Text))));
}
```

- [ ] **Step 3: Create ConversionState**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ConversionState.cs
namespace Snaipe.SampleApp.ViewModels;

public enum ConversionStatus { Idle, Converting, Done, Error }

public sealed record ConversionState(
    string? ImagePath,
    ConversionSettings Settings,
    AsciiDocument? Document,
    ConversionStatus Status,
    int ProgressPercent,
    string? ErrorMessage)
{
    public static ConversionState Empty => new(
        ImagePath: null,
        Settings: ConversionSettings.Default,
        Document: null,
        Status: ConversionStatus.Idle,
        ProgressPercent: 0,
        ErrorMessage: null);
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add samples/Snaipe.SampleApp/ViewModels/
git commit -m "feat(sample): add data models (ConversionSettings, AsciiDocument, ConversionState)"
```

---

### Task 4: SplitPanel custom control

**Files:**
- Create: `samples/Snaipe.SampleApp/Controls/SplitPanel.cs`
- Create: `samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml` (initial — just SplitPanel style)

`SplitPanel` is a `Control` subclass with a draggable divider. Template parts: `PART_LayoutGrid` (Grid), `PART_Divider` (Grid).

- [ ] **Step 1: Create SplitPanel.cs**

```csharp
// samples/Snaipe.SampleApp/Controls/SplitPanel.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Snaipe.SampleApp.Controls;

[Microsoft.UI.Xaml.TemplateVisualState(GroupName = "OrientationStates", Name = "Horizontal")]
[Microsoft.UI.Xaml.TemplateVisualState(GroupName = "OrientationStates", Name = "Vertical")]
public sealed class SplitPanel : Control
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty Pane1ContentProperty =
        DependencyProperty.Register(nameof(Pane1Content), typeof(object), typeof(SplitPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty Pane2ContentProperty =
        DependencyProperty.Register(nameof(Pane2Content), typeof(object), typeof(SplitPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty Pane1MinSizeProperty =
        DependencyProperty.Register(nameof(Pane1MinSize), typeof(double), typeof(SplitPanel), new PropertyMetadata(80.0));

    public static readonly DependencyProperty Pane2MinSizeProperty =
        DependencyProperty.Register(nameof(Pane2MinSize), typeof(double), typeof(SplitPanel), new PropertyMetadata(80.0));

    public static readonly DependencyProperty SplitterThicknessProperty =
        DependencyProperty.Register(nameof(SplitterThickness), typeof(double), typeof(SplitPanel), new PropertyMetadata(5.0));

    public object? Pane1Content { get => GetValue(Pane1ContentProperty); set => SetValue(Pane1ContentProperty, value); }
    public object? Pane2Content { get => GetValue(Pane2ContentProperty); set => SetValue(Pane2ContentProperty, value); }
    public double Pane1MinSize { get => (double)GetValue(Pane1MinSizeProperty); set => SetValue(Pane1MinSizeProperty, value); }
    public double Pane2MinSize { get => (double)GetValue(Pane2MinSizeProperty); set => SetValue(Pane2MinSizeProperty, value); }
    public double SplitterThickness { get => (double)GetValue(SplitterThicknessProperty); set => SetValue(SplitterThicknessProperty, value); }

    public event EventHandler? SplitterMoved;

    // ── Template parts ────────────────────────────────────────────────

    private Grid? _layoutGrid;
    private Grid? _divider;
    private bool _isDragging;
    private double _dragStartX;
    private double _pane1StartWidth;

    public SplitPanel()
    {
        DefaultStyleKey = typeof(SplitPanel);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_divider is not null)
        {
            _divider.PointerPressed  -= OnDividerPointerPressed;
            _divider.PointerMoved    -= OnDividerPointerMoved;
            _divider.PointerReleased -= OnDividerPointerReleased;
        }

        _layoutGrid = GetTemplateChild("PART_LayoutGrid") as Grid;
        _divider    = GetTemplateChild("PART_Divider")    as Grid;

        if (_divider is not null)
        {
            _divider.PointerPressed  += OnDividerPointerPressed;
            _divider.PointerMoved    += OnDividerPointerMoved;
            _divider.PointerReleased += OnDividerPointerReleased;
        }
    }

    private void OnDividerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_layoutGrid is null) return;
        _isDragging = true;
        _dragStartX = e.GetCurrentPoint(this).Position.X;
        _pane1StartWidth = _layoutGrid.ColumnDefinitions[0].ActualWidth;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnDividerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _layoutGrid is null) return;
        var delta = e.GetCurrentPoint(this).Position.X - _dragStartX;
        var totalWidth = ActualWidth - SplitterThickness;
        var newWidth = Math.Clamp(_pane1StartWidth + delta, Pane1MinSize, totalWidth - Pane2MinSize);
        _layoutGrid.ColumnDefinitions[0].Width = new GridLength(newWidth);
        SplitterMoved?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnDividerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
```

- [ ] **Step 2: Create Generic.xaml with SplitPanel style**

```xml
<!-- samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml -->
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Snaipe.SampleApp.Controls">

    <!-- ══ SplitPanel ══════════════════════════════════════════════════ -->
    <Style TargetType="local:SplitPanel">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="local:SplitPanel">
                    <Grid x:Name="PART_LayoutGrid">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="5"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <ContentPresenter Grid.Column="0"
                                          Content="{TemplateBinding Pane1Content}"/>

                        <Grid x:Name="PART_Divider"
                              Grid.Column="1"
                              Background="{ThemeResource SystemControlBackgroundChromeMediumBrush}">
                            <Rectangle Width="1"
                                       HorizontalAlignment="Center"
                                       Fill="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                        </Grid>

                        <ContentPresenter Grid.Column="2"
                                          Content="{TemplateBinding Pane2Content}"/>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 3: Merge Generic.xaml in App.cs**

Open `samples/Snaipe.SampleApp/App.cs` and add the merged dictionary after the XamlControlsResources line:

```csharp
// samples/Snaipe.SampleApp/App.cs
using Microsoft.UI.Xaml;
using Snaipe.Agent;

namespace Snaipe.SampleApp;

public class App : Application
{
    private SnaipeAgent? _agent;

    public App()
    {
        this.Resources.MergedDictionaries.Add(
            new Microsoft.UI.Xaml.Controls.XamlControlsResources());
        this.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("ms-appx:///Controls/Themes/Generic.xaml")
        });
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
        _agent = SnaipeAgent.Attach(window);
    }
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 5: Commit**

```bash
git add samples/Snaipe.SampleApp/Controls/ samples/Snaipe.SampleApp/App.cs
git commit -m "feat(sample): add SplitPanel custom control with drag-to-resize"
```

---

### Task 5: ToolbarButton custom control

**Files:**
- Create: `samples/Snaipe.SampleApp/Controls/ToolbarButton.cs`
- Modify: `samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml` — append ToolbarButton style

`ToolbarButton` is a `Control` with two visual state groups: `CommonStates` (Normal/PointerOver/Pressed/Disabled) and `ActiveStates` (Inactive/Active).

- [ ] **Step 1: Create ToolbarButton.cs**

```csharp
// samples/Snaipe.SampleApp/Controls/ToolbarButton.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Snaipe.SampleApp.Controls;

public sealed class ToolbarButton : Control
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ToolbarButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToolbarButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ToolbarButton),
            new PropertyMetadata(false, OnIsActiveChanged));

    public string Icon  { get => (string)GetValue(IconProperty);  set => SetValue(IconProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public ToolbarButton()
    {
        DefaultStyleKey = typeof(ToolbarButton);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", false);
        VisualStateManager.GoToState(this, IsActive ? "Active" : "Inactive", false);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsEnabled) VisualStateManager.GoToState(this, "PointerOver", true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEnabled) VisualStateManager.GoToState(this, "Pressed", true);
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        VisualStateManager.GoToState(this, IsEnabled ? "PointerOver" : "Disabled", true);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var btn = (ToolbarButton)d;
        VisualStateManager.GoToState(btn, (bool)e.NewValue ? "Active" : "Inactive", true);
    }
}
```

- [ ] **Step 2: Append ToolbarButton style to Generic.xaml**

Add this block inside the `<ResourceDictionary>` after the SplitPanel style:

```xml
    <!-- ══ ToolbarButton ══════════════════════════════════════════════ -->
    <Style TargetType="local:ToolbarButton">
        <Setter Property="Width" Value="64"/>
        <Setter Property="Padding" Value="6,8"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="local:ToolbarButton">
                    <Border x:Name="RootBorder"
                            CornerRadius="4"
                            Padding="{TemplateBinding Padding}"
                            Background="Transparent">

                        <VisualStateManager.VisualStateGroups>
                            <VisualStateGroup x:Name="CommonStates">
                                <VisualState x:Name="Normal"/>
                                <VisualState x:Name="PointerOver">
                                    <VisualState.Setters>
                                        <Setter Target="RootBorder.Background" Value="#1E3A5F"/>
                                        <Setter Target="IconText.Foreground" Value="#93C5FD"/>
                                        <Setter Target="LabelText.Foreground" Value="#93C5FD"/>
                                    </VisualState.Setters>
                                </VisualState>
                                <VisualState x:Name="Pressed">
                                    <VisualState.Setters>
                                        <Setter Target="RootBorder.Background" Value="#1E40AF"/>
                                    </VisualState.Setters>
                                </VisualState>
                                <VisualState x:Name="Disabled">
                                    <VisualState.Setters>
                                        <Setter Target="RootBorder.Opacity" Value="0.35"/>
                                    </VisualState.Setters>
                                </VisualState>
                            </VisualStateGroup>
                            <VisualStateGroup x:Name="ActiveStates">
                                <VisualState x:Name="Inactive"/>
                                <VisualState x:Name="Active">
                                    <VisualState.Setters>
                                        <Setter Target="RootBorder.Background" Value="#1E40AF"/>
                                        <Setter Target="IconText.Foreground" Value="White"/>
                                        <Setter Target="LabelText.Foreground" Value="White"/>
                                    </VisualState.Setters>
                                </VisualState>
                            </VisualStateGroup>
                        </VisualStateManager.VisualStateGroups>

                        <StackPanel HorizontalAlignment="Center" Spacing="3">
                            <TextBlock x:Name="IconText"
                                       Text="{TemplateBinding Icon}"
                                       FontSize="18"
                                       HorizontalAlignment="Center"
                                       Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}"/>
                            <TextBlock x:Name="LabelText"
                                       Text="{TemplateBinding Label}"
                                       FontSize="10"
                                       HorizontalAlignment="Center"
                                       Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                        </StackPanel>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/Controls/
git commit -m "feat(sample): add ToolbarButton control with CommonStates and ActiveStates"
```

---

### Task 6: ImagePreviewControl (UserControl)

**Files:**
- Create: `samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml`
- Create: `samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml.cs`

`ImagePreviewControl` is a `UserControl` wrapping a `ScrollViewer`. Custom DPs: `Source` (ImageSource), `ZoomLevel` (double). Visual states: `PanStates` group — Idle, Panning, NothingLoaded.

- [ ] **Step 1: Create ImagePreviewControl.xaml**

```xml
<!-- samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml -->
<UserControl
    x:Class="Snaipe.SampleApp.Controls.ImagePreviewControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name="PanStates">
                <VisualState x:Name="NothingLoaded">
                    <VisualState.Setters>
                        <Setter Target="EmptyHint.Visibility" Value="Visible"/>
                        <Setter Target="ImageScroll.Visibility" Value="Collapsed"/>
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name="Idle"/>
                <VisualState x:Name="Panning"/>
            </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>

        <!-- Checkerboard background (transparent image convention) -->
        <Rectangle x:Name="Checkerboard">
            <Rectangle.Fill>
                <ImageBrush Stretch="None" AlignmentX="Left" AlignmentY="Top"/>
            </Rectangle.Fill>
        </Rectangle>

        <ScrollViewer x:Name="ImageScroll"
                      ZoomMode="Enabled"
                      HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Auto"
                      MinZoomFactor="0.1"
                      MaxZoomFactor="8.0">
            <Image x:Name="PreviewImage"
                   Stretch="None"
                   HorizontalAlignment="Left"
                   VerticalAlignment="Top"/>
        </ScrollViewer>

        <Border x:Name="EmptyHint"
                Visibility="Collapsed"
                Background="#0A1628">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
                <TextBlock Text="🖼"
                           FontSize="40"
                           HorizontalAlignment="Center"/>
                <TextBlock Text="Drop an image here"
                           FontSize="14"
                           Foreground="#64748B"
                           HorizontalAlignment="Center"/>
                <TextBlock Text="or click Open in the toolbar"
                           FontSize="12"
                           Foreground="#475569"
                           HorizontalAlignment="Center"/>
            </StackPanel>
        </Border>

        <!-- Zoom badge -->
        <Border x:Name="ZoomBadge"
                HorizontalAlignment="Right"
                VerticalAlignment="Bottom"
                Margin="8"
                Background="#80000000"
                CornerRadius="3"
                Padding="6,2">
            <TextBlock x:Name="ZoomLabel"
                       FontSize="11"
                       Foreground="#94A3B8"
                       FontFamily="Cascadia Code, Consolas, monospace"/>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create ImagePreviewControl.xaml.cs**

```csharp
// samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Snaipe.SampleApp.Controls;

public sealed partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ImagePreviewControl),
            new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(ImagePreviewControl),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double ZoomLevel { get => (double)GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }

    public ImagePreviewControl()
    {
        InitializeComponent();
        ImageScroll.ViewChanged += OnScrollViewChanged;
        Loaded += (_, _) => UpdateVisualState();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ImagePreviewControl)d;
        ctrl.PreviewImage.Source = e.NewValue as ImageSource;
        ctrl.UpdateVisualState();
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ImagePreviewControl)d;
        ctrl.ImageScroll.ChangeView(null, null, (float)(double)e.NewValue);
        ctrl.UpdateZoomBadge((double)e.NewValue);
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var factor = ImageScroll.ZoomFactor;
        UpdateZoomBadge(factor);
        // Sync DP without triggering a circular ChangeView call
        SetValue(ZoomLevelProperty, (double)factor);
    }

    private void UpdateZoomBadge(double zoom)
        => ZoomLabel.Text = $"{zoom * 100:F0}%";

    private void UpdateVisualState()
    {
        var state = Source is null ? "NothingLoaded" : "Idle";
        VisualStateManager.GoToState(this, state, true);
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/Controls/
git commit -m "feat(sample): add ImagePreviewControl with zoom and NothingLoaded state"
```

---

### Task 7: AsciiOutputControl custom control

**Files:**
- Create: `samples/Snaipe.SampleApp/Controls/AsciiOutputControl.cs`
- Modify: `samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml` — append AsciiOutputControl style

`AsciiOutputControl` is a `Control` that renders `AsciiDocument` as a `RichTextBlock` with colored `Run` elements. Visual states: `DisplayStates` group — Empty, Idle, Converting, Error.

- [ ] **Step 1: Create AsciiOutputControl.cs**

```csharp
// samples/Snaipe.SampleApp/Controls/AsciiOutputControl.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Snaipe.SampleApp.ViewModels;
using Windows.UI;

namespace Snaipe.SampleApp.Controls;

public sealed class AsciiOutputControl : Control
{
    // ── Dependency Properties ─────────────────────────────────────────

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(AsciiDocument), typeof(AsciiOutputControl),
            new PropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(ConversionStatus), typeof(AsciiOutputControl),
            new PropertyMetadata(ConversionStatus.Idle, OnStatusChanged));

    public static readonly DependencyProperty ProgressPercentProperty =
        DependencyProperty.Register(nameof(ProgressPercent), typeof(int), typeof(AsciiOutputControl),
            new PropertyMetadata(0, OnProgressChanged));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(AsciiOutputControl),
            new PropertyMetadata(string.Empty, OnErrorChanged));

    public static readonly DependencyProperty OutputFontSizeProperty =
        DependencyProperty.Register(nameof(OutputFontSize), typeof(double), typeof(AsciiOutputControl),
            new PropertyMetadata(10.0, OnDocumentChanged));

    public AsciiDocument? Document { get => (AsciiDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public ConversionStatus Status { get => (ConversionStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public int ProgressPercent { get => (int)GetValue(ProgressPercentProperty); set => SetValue(ProgressPercentProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public double OutputFontSize { get => (double)GetValue(OutputFontSizeProperty); set => SetValue(OutputFontSizeProperty, value); }

    // ── Template parts ────────────────────────────────────────────────

    private RichTextBlock? _richText;
    private ProgressBar? _progressBar;
    private TextBlock? _errorText;

    public AsciiOutputControl()
    {
        DefaultStyleKey = typeof(AsciiOutputControl);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _richText    = GetTemplateChild("PART_RichText")   as RichTextBlock;
        _progressBar = GetTemplateChild("PART_Progress")   as ProgressBar;
        _errorText   = GetTemplateChild("PART_ErrorText")  as TextBlock;
        UpdateDisplayState();
        RenderDocument();
    }

    // ── DP change callbacks ───────────────────────────────────────────

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsciiOutputControl)d).RenderDocument();

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsciiOutputControl)d).UpdateDisplayState();

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AsciiOutputControl)d;
        if (ctrl._progressBar is not null)
            ctrl._progressBar.Value = (int)e.NewValue;
    }

    private static void OnErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AsciiOutputControl)d;
        if (ctrl._errorText is not null)
            ctrl._errorText.Text = (string)e.NewValue;
    }

    // ── Rendering ────────────────────────────────────────────────────

    private void RenderDocument()
    {
        if (_richText is null) return;
        _richText.Blocks.Clear();

        if (Document is not { Lines.Count: > 0 }) return;

        foreach (var line in Document.Lines)
        {
            var para = new Paragraph { LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            foreach (var span in line.Spans)
            {
                var run = new Run { Text = span.Text, FontSize = OutputFontSize };
                if (span.Color is { } color)
                    run.Foreground = new SolidColorBrush(color);
                para.Inlines.Add(run);
            }
            _richText.Blocks.Add(para);
        }
    }

    private void UpdateDisplayState()
    {
        var state = Status switch
        {
            ConversionStatus.Idle when Document is null => "Empty",
            ConversionStatus.Idle                       => "Idle",
            ConversionStatus.Done                       => "Idle",
            ConversionStatus.Converting                 => "Converting",
            ConversionStatus.Error                      => "Error",
            _                                           => "Empty"
        };
        VisualStateManager.GoToState(this, state, true);
    }
}
```

- [ ] **Step 2: Append AsciiOutputControl style to Generic.xaml**

Add after the ToolbarButton style block:

```xml
    <!-- ══ AsciiOutputControl ════════════════════════════════════════ -->
    <Style TargetType="local:AsciiOutputControl">
        <Setter Property="Background" Value="#020617"/>
        <Setter Property="FontFamily" Value="Cascadia Code, Consolas, monospace"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="local:AsciiOutputControl">
                    <Grid Background="{TemplateBinding Background}">
                        <VisualStateManager.VisualStateGroups>
                            <VisualStateGroup x:Name="DisplayStates">
                                <VisualState x:Name="Empty">
                                    <VisualState.Setters>
                                        <Setter Target="EmptyHint.Visibility" Value="Visible"/>
                                        <Setter Target="OutputScroll.Visibility" Value="Collapsed"/>
                                    </VisualState.Setters>
                                </VisualState>
                                <VisualState x:Name="Idle"/>
                                <VisualState x:Name="Converting">
                                    <VisualState.Setters>
                                        <Setter Target="ProgressOverlay.Visibility" Value="Visible"/>
                                    </VisualState.Setters>
                                </VisualState>
                                <VisualState x:Name="Error">
                                    <VisualState.Setters>
                                        <Setter Target="ErrorPanel.Visibility" Value="Visible"/>
                                        <Setter Target="OutputScroll.Visibility" Value="Collapsed"/>
                                    </VisualState.Setters>
                                </VisualState>
                            </VisualStateGroup>
                        </VisualStateManager.VisualStateGroups>

                        <ScrollViewer x:Name="OutputScroll"
                                      HorizontalScrollBarVisibility="Auto"
                                      VerticalScrollBarVisibility="Auto">
                            <RichTextBlock x:Name="PART_RichText"
                                           IsTextSelectionEnabled="True"
                                           FontFamily="{TemplateBinding FontFamily}"/>
                        </ScrollViewer>

                        <Border x:Name="ProgressOverlay"
                                Visibility="Collapsed"
                                Background="#C0020617">
                            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="12">
                                <ProgressBar x:Name="PART_Progress"
                                             Width="240"
                                             Minimum="0" Maximum="100"/>
                                <TextBlock Text="Converting…"
                                           HorizontalAlignment="Center"
                                           Foreground="#64748B"/>
                            </StackPanel>
                        </Border>

                        <Border x:Name="ErrorPanel"
                                Visibility="Collapsed"
                                Background="#C0200000">
                            <TextBlock x:Name="PART_ErrorText"
                                       HorizontalAlignment="Center"
                                       VerticalAlignment="Center"
                                       Foreground="#F87171"
                                       TextWrapping="Wrap"
                                       Margin="16"/>
                        </Border>

                        <Border x:Name="EmptyHint"
                                Visibility="Collapsed"
                                Background="#020617">
                            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
                                <TextBlock Text="No output yet"
                                           FontSize="14"
                                           Foreground="#1E3A5F"
                                           HorizontalAlignment="Center"/>
                                <TextBlock Text="Open an image and click Convert"
                                           FontSize="12"
                                           Foreground="#1E293B"
                                           HorizontalAlignment="Center"/>
                            </StackPanel>
                        </Border>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/Controls/
git commit -m "feat(sample): add AsciiOutputControl with DisplayStates and RichTextBlock color rendering"
```

---

### Task 8: CharacterSetPicker custom control

**Files:**
- Create: `samples/Snaipe.SampleApp/Controls/CharacterSetPicker.cs`
- Modify: `samples/Snaipe.SampleApp/Controls/Themes/Generic.xaml` — append CharacterSetPicker style

`CharacterSetPicker` renders a 2×2 grid of selectable cards, one per `CharacterSet`. Raises `SelectionChanged` event when the user clicks a card.

- [ ] **Step 1: Create CharacterSetPicker.cs**

```csharp
// samples/Snaipe.SampleApp/Controls/CharacterSetPicker.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Controls;

public sealed record CharacterSetOption(CharacterSet Value, string Label, string PreviewSample);

public sealed class CharacterSetPicker : Control
{
    public static readonly DependencyProperty SelectedCharacterSetProperty =
        DependencyProperty.Register(nameof(SelectedCharacterSet), typeof(CharacterSet), typeof(CharacterSetPicker),
            new PropertyMetadata(CharacterSet.Block, OnSelectionChanged));

    public CharacterSet SelectedCharacterSet
    {
        get => (CharacterSet)GetValue(SelectedCharacterSetProperty);
        set => SetValue(SelectedCharacterSetProperty, value);
    }

    public event EventHandler<CharacterSet>? SelectionChanged;

    private static readonly CharacterSetOption[] Options =
    [
        new(CharacterSet.Block,   "Block",   "░▒▓█ ▓▒░░\n▒▓██▓▒░░▒"),
        new(CharacterSet.Classic, "Classic", "@#$%!*+;:,\n##$$%%!!**"),
        new(CharacterSet.Braille, "Braille", "⣿⣾⣹⡏⠟⠻⢿⣿\n⣿⡿⠿⡟⠋⠉⠈⠀"),
        new(CharacterSet.Minimal, "Minimal", ". :-=+*#%@\n..::==++**"),
    ];

    // Card borders keyed by CharacterSet for highlight toggling
    private readonly Dictionary<CharacterSet, Border> _cards = new();

    public CharacterSetPicker()
    {
        DefaultStyleKey = typeof(CharacterSetPicker);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _cards.Clear();

        if (GetTemplateChild("PART_Grid") is not Grid grid) return;

        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Clear();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < Options.Length; i++)
        {
            var opt = Options[i];
            var card = BuildCard(opt);
            Grid.SetColumn(card, i % 2);
            Grid.SetRow(card, i / 2);
            grid.Children.Add(card);
            _cards[opt.Value] = card;
        }

        UpdateCardHighlights();
    }

    private Border BuildCard(CharacterSetOption opt)
    {
        var preview = new TextBlock
        {
            Text = opt.PreviewSample,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 9,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };

        var label = new TextBlock
        {
            Text = opt.Label,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray)
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6),
            Margin = new Thickness(2),
            BorderThickness = new Thickness(1),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(0xFF, 0x33, 0x41, 0x55)),
            Child = new StackPanel { Children = { label, preview } }
        };

        border.PointerPressed += (_, e) =>
        {
            SelectedCharacterSet = opt.Value;
            e.Handled = true;
        };

        return border;
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (CharacterSetPicker)d;
        picker.UpdateCardHighlights();
        picker.SelectionChanged?.Invoke(picker, (CharacterSet)e.NewValue);
    }

    private void UpdateCardHighlights()
    {
        foreach (var (cs, card) in _cards)
        {
            bool selected = cs == SelectedCharacterSet;
            card.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                selected
                    ? Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x3A, 0x8A)
                    : Microsoft.UI.Colors.Transparent);
            card.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                selected
                    ? Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)
                    : Windows.UI.Color.FromArgb(0xFF, 0x33, 0x41, 0x55));
        }
    }
}
```

- [ ] **Step 2: Append CharacterSetPicker style to Generic.xaml**

```xml
    <!-- ══ CharacterSetPicker ════════════════════════════════════════ -->
    <Style TargetType="local:CharacterSetPicker">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="local:CharacterSetPicker">
                    <Grid x:Name="PART_Grid"/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/Controls/
git commit -m "feat(sample): add CharacterSetPicker control with card-based selection"
```

---

### Task 9: ConversionPipeline service

**Files:**
- Create: `samples/Snaipe.SampleApp/Services/ConversionPipeline.cs`

The pipeline loads an image via ImageSharp, converts it row-by-row on a background `Task`, and reports progress via `IProgress<int>`. It produces an `AsciiDocument` with colored spans.

- [ ] **Step 1: Create ConversionPipeline.cs**

```csharp
// samples/Snaipe.SampleApp/Services/ConversionPipeline.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Snaipe.SampleApp.ViewModels;
using Windows.UI;

namespace Snaipe.SampleApp.Services;

public sealed class ConversionPipeline : IDisposable
{
    private CancellationTokenSource? _cts;
    private Image<Rgba32>? _loadedImage;
    private string? _loadedPath;

    // ── ANSI 16-color palette (approximated as RGB) ───────────────────
    private static readonly (Color Color, string Name)[] Ansi16 =
    [
        (Color.FromArgb(0xFF,   0,   0,   0), "Black"),
        (Color.FromArgb(0xFF, 128,   0,   0), "DarkRed"),
        (Color.FromArgb(0xFF,   0, 128,   0), "DarkGreen"),
        (Color.FromArgb(0xFF, 128, 128,   0), "DarkYellow"),
        (Color.FromArgb(0xFF,   0,   0, 128), "DarkBlue"),
        (Color.FromArgb(0xFF, 128,   0, 128), "DarkMagenta"),
        (Color.FromArgb(0xFF,   0, 128, 128), "DarkCyan"),
        (Color.FromArgb(0xFF, 192, 192, 192), "Gray"),
        (Color.FromArgb(0xFF, 128, 128, 128), "DarkGray"),
        (Color.FromArgb(0xFF, 255,   0,   0), "Red"),
        (Color.FromArgb(0xFF,   0, 255,   0), "Green"),
        (Color.FromArgb(0xFF, 255, 255,   0), "Yellow"),
        (Color.FromArgb(0xFF,   0,   0, 255), "Blue"),
        (Color.FromArgb(0xFF, 255,   0, 255), "Magenta"),
        (Color.FromArgb(0xFF,   0, 255, 255), "Cyan"),
        (Color.FromArgb(0xFF, 255, 255, 255), "White"),
    ];

    public async Task<AsciiDocument> ConvertAsync(
        string imagePath,
        ConversionSettings settings,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        // Load image (cache if same path)
        if (_loadedPath != imagePath)
        {
            _loadedImage?.Dispose();
            _loadedImage = await Image.LoadAsync<Rgba32>(imagePath, ct);
            _loadedPath = imagePath;
        }

        var src = _loadedImage!;

        // Resize: ASCII chars are roughly 2× taller than wide, so halve the height
        const double aspectCorrection = 0.5;
        int targetW = Math.Max(1, settings.OutputWidth);
        int targetH = Math.Max(1, (int)(src.Height * ((double)targetW / src.Width) * aspectCorrection));

        using var resized = src.Clone(ctx => ctx.Resize(targetW, targetH));

        var chars = settings.GetChars();
        var lines = new List<AsciiLine>(targetH);

        // Build dithering error buffer for Floyd-Steinberg
        var errorR = new double[targetH, targetW];
        var errorG = new double[targetH, targetW];
        var errorB = new double[targetH, targetW];

        // Bayer 4×4 matrix (values 0–15, threshold at 8)
        int[,] bayer4 = {
            {  0,  8,  2, 10 },
            { 12,  4, 14,  6 },
            {  3, 11,  1,  9 },
            { 15,  7, 13,  5 }
        };

        for (int row = 0; row < targetH; row++)
        {
            ct.ThrowIfCancellationRequested();

            var spans = new List<AsciiSpan>();
            string? currentText = null;
            Color? currentColor = null;
            var runBuffer = new System.Text.StringBuilder();

            for (int col = 0; col < targetW; col++)
            {
                var pixel = resized[col, row];

                double r = pixel.R / 255.0;
                double g = pixel.G / 255.0;
                double b = pixel.B / 255.0;

                // Apply accumulated dithering error
                if (settings.Dithering == DitheringAlgorithm.FloydSteinberg)
                {
                    r = Math.Clamp(r + errorR[row, col], 0, 1);
                    g = Math.Clamp(g + errorG[row, col], 0, 1);
                    b = Math.Clamp(b + errorB[row, col], 0, 1);
                }
                else if (settings.Dithering == DitheringAlgorithm.BayerOrdered)
                {
                    double threshold = bayer4[row % 4, col % 4] / 16.0 - 0.5;
                    r = Math.Clamp(r + threshold * 0.25, 0, 1);
                    g = Math.Clamp(g + threshold * 0.25, 0, 1);
                    b = Math.Clamp(b + threshold * 0.25, 0, 1);
                }

                double brightness = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                if (settings.Invert) brightness = 1 - brightness;

                int charIndex = (int)(brightness * (chars.Length - 1));
                charIndex = Math.Clamp(charIndex, 0, chars.Length - 1);
                string ch = chars[charIndex];

                // Propagate Floyd-Steinberg error
                if (settings.Dithering == DitheringAlgorithm.FloydSteinberg)
                {
                    double quantR = brightness - (double)charIndex / (chars.Length - 1);
                    PropagateFSError(errorR, row, col, targetW, targetH, r - (r - quantR * 0.5));
                    PropagateFSError(errorG, row, col, targetW, targetH, g - (g - quantR * 0.5));
                    PropagateFSError(errorB, row, col, targetW, targetH, b - (b - quantR * 0.5));
                }

                // Determine color for this character
                Color? spanColor = settings.ColorMode switch
                {
                    ColorMode.Grayscale => null,
                    ColorMode.Ansi16    => NearestAnsi16(r, g, b),
                    ColorMode.Ansi256   => Ansi256Color(r, g, b),
                    ColorMode.TrueColor => Color.FromArgb(0xFF,
                        (byte)(r * 255), (byte)(g * 255), (byte)(b * 255)),
                    _ => null
                };

                // Run-length encode: flush run if color changes
                if (spanColor != currentColor && runBuffer.Length > 0)
                {
                    spans.Add(new AsciiSpan(runBuffer.ToString(), currentColor));
                    runBuffer.Clear();
                }
                currentColor = spanColor;
                runBuffer.Append(ch);
            }

            if (runBuffer.Length > 0)
                spans.Add(new AsciiSpan(runBuffer.ToString(), currentColor));

            lines.Add(new AsciiLine(spans));
            progress?.Report((row + 1) * 100 / targetH);
        }

        return new AsciiDocument(lines);
    }

    private static void PropagateFSError(double[,] err, int row, int col, int w, int h, double e)
    {
        if (col + 1 < w)         err[row,     col + 1] += e * 7 / 16.0;
        if (row + 1 < h)
        {
            if (col > 0)         err[row + 1, col - 1] += e * 3 / 16.0;
                                 err[row + 1, col    ] += e * 5 / 16.0;
            if (col + 1 < w)     err[row + 1, col + 1] += e * 1 / 16.0;
        }
    }

    private static Color NearestAnsi16(double r, double g, double b)
    {
        double minDist = double.MaxValue;
        Color best = Ansi16[0].Color;
        foreach (var (color, _) in Ansi16)
        {
            double dr = r - color.R / 255.0;
            double dg = g - color.G / 255.0;
            double db = b - color.B / 255.0;
            double dist = dr * dr + dg * dg + db * db;
            if (dist < minDist) { minDist = dist; best = color; }
        }
        return best;
    }

    private static Color Ansi256Color(double r, double g, double b)
    {
        // Map to 6×6×6 color cube (indices 16–231)
        int ri = (int)(r * 5 + 0.5);
        int gi = (int)(g * 5 + 0.5);
        int bi = (int)(b * 5 + 0.5);
        byte rv = (byte)(ri == 0 ? 0 : 55 + ri * 40);
        byte gv = (byte)(gi == 0 ? 0 : 55 + gi * 40);
        byte bv = (byte)(bi == 0 ? 0 : 55 + bi * 40);
        return Color.FromArgb(0xFF, rv, gv, bv);
    }

    public void Cancel() => _cts?.Cancel();

    public void Dispose()
    {
        _cts?.Dispose();
        _loadedImage?.Dispose();
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add samples/Snaipe.SampleApp/Services/
git commit -m "feat(sample): add ConversionPipeline with ImageSharp, Floyd-Steinberg, Bayer dithering"
```

---

### Task 10: ViewModels

**Files:**
- Create: `samples/Snaipe.SampleApp/ViewModels/ImagePanelViewModel.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/AsciiOutputViewModel.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/ConversionSettingsViewModel.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/ExportViewModel.cs`
- Create: `samples/Snaipe.SampleApp/ViewModels/ShellViewModel.cs`

- [ ] **Step 1: Create ImagePanelViewModel.cs**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ImagePanelViewModel.cs
using Microsoft.UI.Xaml.Media.Imaging;

namespace Snaipe.SampleApp.ViewModels;

public sealed class ImagePanelViewModel : ViewModelBase
{
    private BitmapImage? _sourceImage;
    private double _zoomLevel = 1.0;
    private string _filePath = string.Empty;

    public BitmapImage? SourceImage { get => _sourceImage; private set => SetField(ref _sourceImage, value); }
    public double ZoomLevel { get => _zoomLevel; set => SetField(ref _zoomLevel, value); }
    public string FilePath { get => _filePath; private set => SetField(ref _filePath, value); }

    public RelayCommand ZoomInCommand  { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }

    public ImagePanelViewModel()
    {
        ZoomInCommand    = new RelayCommand(() => ZoomLevel = Math.Min(8.0, ZoomLevel * 1.25));
        ZoomOutCommand   = new RelayCommand(() => ZoomLevel = Math.Max(0.1, ZoomLevel / 1.25));
        ResetZoomCommand = new RelayCommand(() => ZoomLevel = 1.0);
    }

    public async Task LoadImageAsync(string path)
    {
        FilePath = path;
        var bmp = new BitmapImage();
        using var stream = System.IO.File.OpenRead(path);
        await bmp.SetSourceAsync(stream.AsRandomAccessStream());
        SourceImage = bmp;
        ZoomLevel = 1.0;
    }
}
```

- [ ] **Step 2: Create AsciiOutputViewModel.cs**

```csharp
// samples/Snaipe.SampleApp/ViewModels/AsciiOutputViewModel.cs
namespace Snaipe.SampleApp.ViewModels;

public sealed class AsciiOutputViewModel : ViewModelBase
{
    private AsciiDocument? _document;
    private ConversionStatus _displayStatus = ConversionStatus.Idle;
    private int _progressPercent;
    private string _errorMessage = string.Empty;
    private double _outputFontSize = 10.0;
    private ColorMode _colorMode = ColorMode.Grayscale;

    public AsciiDocument? Document       { get => _document;       private set => SetField(ref _document, value); }
    public ConversionStatus DisplayStatus { get => _displayStatus;  private set => SetField(ref _displayStatus, value); }
    public int ProgressPercent           { get => _progressPercent; private set => SetField(ref _progressPercent, value); }
    public string ErrorMessage           { get => _errorMessage;    private set => SetField(ref _errorMessage, value); }
    public double OutputFontSize         { get => _outputFontSize;  set => SetField(ref _outputFontSize, value); }
    public ColorMode ColorMode           { get => _colorMode;       private set => SetField(ref _colorMode, value); }

    public void UpdateFromState(ConversionState state)
    {
        Document        = state.Document;
        DisplayStatus   = state.Status;
        ProgressPercent = state.ProgressPercent;
        ErrorMessage    = state.ErrorMessage ?? string.Empty;
        ColorMode       = state.Settings.ColorMode;
    }
}
```

- [ ] **Step 3: Create ConversionSettingsViewModel.cs**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ConversionSettingsViewModel.cs
namespace Snaipe.SampleApp.ViewModels;

public sealed class ConversionSettingsViewModel : ViewModelBase
{
    private CharacterSet _characterSet = CharacterSet.Block;
    private int _outputWidth = 120;
    private ColorMode _colorMode = ColorMode.Grayscale;
    private DitheringAlgorithm _dithering = DitheringAlgorithm.None;
    private bool _invert;

    public CharacterSet CharacterSet
    {
        get => _characterSet;
        set { if (SetField(ref _characterSet, value)) NotifySettingsChanged(); }
    }
    public int OutputWidth
    {
        get => _outputWidth;
        set { if (SetField(ref _outputWidth, Math.Clamp(value, 20, 300))) NotifySettingsChanged(); }
    }
    public ColorMode ColorMode
    {
        get => _colorMode;
        set { if (SetField(ref _colorMode, value)) NotifySettingsChanged(); }
    }
    public DitheringAlgorithm Dithering
    {
        get => _dithering;
        set { if (SetField(ref _dithering, value)) NotifySettingsChanged(); }
    }
    public bool Invert
    {
        get => _invert;
        set { if (SetField(ref _invert, value)) NotifySettingsChanged(); }
    }

    public event EventHandler? SettingsChanged;

    public ConversionSettings ToSettings() =>
        new(_characterSet, _outputWidth, _colorMode, _dithering, _invert);

    private void NotifySettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: Create ExportViewModel.cs**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ExportViewModel.cs
using Windows.Storage.Pickers;

namespace Snaipe.SampleApp.ViewModels;

public enum ExportFormat { PlainText, HtmlColor, Clipboard }

public sealed class ExportViewModel : ViewModelBase
{
    private ExportFormat _selectedFormat = ExportFormat.PlainText;
    private string _destinationPath = string.Empty;
    private AsciiDocument? _document;

    public ExportFormat SelectedFormat   { get => _selectedFormat;  set => SetField(ref _selectedFormat, value); }
    public string DestinationPath        { get => _destinationPath; set => SetField(ref _destinationPath, value); }

    public AsyncRelayCommand BrowseCommand  { get; }
    public AsyncRelayCommand ExportCommand  { get; }

    public ExportViewModel()
    {
        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _document is not null);
    }

    public void SetDocument(AsciiDocument? doc)
    {
        _document = doc;
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async Task BrowseAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = "ascii-art";
        if (SelectedFormat == ExportFormat.PlainText)
        {
            picker.FileTypeChoices.Add("Text file", [".txt"]);
            picker.SuggestedFileName += ".txt";
        }
        else if (SelectedFormat == ExportFormat.HtmlColor)
        {
            picker.FileTypeChoices.Add("HTML file", [".html"]);
            picker.SuggestedFileName += ".html";
        }
        // InitializeWithWindow not needed on WinUI3/Uno Win32 for file pickers
        var file = await picker.PickSaveFileAsync();
        if (file is not null) DestinationPath = file.Path;
    }

    private async Task ExportAsync()
    {
        if (_document is null) return;

        if (SelectedFormat == ExportFormat.Clipboard)
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(_document.ToPlainText());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            return;
        }

        if (string.IsNullOrEmpty(DestinationPath)) return;

        string content = SelectedFormat switch
        {
            ExportFormat.PlainText => _document.ToPlainText(),
            ExportFormat.HtmlColor => BuildHtml(_document),
            _ => _document.ToPlainText()
        };
        await System.IO.File.WriteAllTextAsync(DestinationPath, content);
    }

    private static string BuildHtml(AsciiDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><body style=\"background:#000;margin:0\"><pre style=\"font-family:monospace;font-size:12px;line-height:1.2\">");
        foreach (var line in doc.Lines)
        {
            foreach (var span in line.Spans)
            {
                if (span.Color is { } c)
                    sb.Append($"<span style=\"color:#{c.R:X2}{c.G:X2}{c.B:X2}\">{System.Web.HttpUtility.HtmlEncode(span.Text)}</span>");
                else
                    sb.Append(System.Web.HttpUtility.HtmlEncode(span.Text));
            }
            sb.AppendLine();
        }
        sb.AppendLine("</pre></body></html>");
        return sb.ToString();
    }
}
```

> **Note:** `System.Web.HttpUtility` requires adding `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` or using a simple manual HTML-encode. Replace `System.Web.HttpUtility.HtmlEncode(span.Text)` with the inline helper below if build fails:
> ```csharp
> private static string HtmlEncode(string text) =>
>     text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
> ```
> Then replace all `System.Web.HttpUtility.HtmlEncode(...)` calls with `HtmlEncode(...)`.

- [ ] **Step 5: Create ShellViewModel.cs**

```csharp
// samples/Snaipe.SampleApp/ViewModels/ShellViewModel.cs
using Microsoft.UI.Dispatching;
using Snaipe.SampleApp.Services;

namespace Snaipe.SampleApp.ViewModels;

public sealed class ShellViewModel : ViewModelBase, IDisposable
{
    private ConversionState _state = ConversionState.Empty;
    private bool _isDocked = true;
    private string _statusMessage = "Ready — open an image to begin";
    private CancellationTokenSource? _conversionCts;

    private readonly ConversionPipeline _pipeline = new();
    private readonly DispatcherQueue _dispatcher;

    public ImagePanelViewModel          ImagePanel   { get; } = new();
    public AsciiOutputViewModel         AsciiOutput  { get; } = new();
    public ConversionSettingsViewModel  Settings     { get; } = new();
    public ExportViewModel              Export       { get; } = new();

    public ConversionState State { get => _state; private set { SetField(ref _state, value); SyncPanels(); } }
    public bool IsDocked         { get => _isDocked;       private set => SetField(ref _isDocked, value); }
    public string StatusMessage  { get => _statusMessage;  private set => SetField(ref _statusMessage, value); }

    public AsyncRelayCommand   OpenImageCommand          { get; }
    public RelayCommand        TogglePreviewWindowCommand { get; }
    public RelayCommand        ShowExportDialogCommand   { get; }
    public AsyncRelayCommand   ConvertCommand            { get; }
    public RelayCommand        CopyToClipboardCommand    { get; }

    // Set by the view to open/close the preview window
    public Action? RequestOpenPreviewWindow  { get; set; }
    public Action? RequestClosePreviewWindow { get; set; }
    public Action? RequestShowExportDialog   { get; set; }

    public ShellViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        OpenImageCommand = new AsyncRelayCommand(OpenImageAsync);
        ConvertCommand   = new AsyncRelayCommand(
            () => ConvertAsync(ImagePanel.FilePath, Settings.ToSettings()),
            () => !string.IsNullOrEmpty(ImagePanel.FilePath));

        TogglePreviewWindowCommand = new RelayCommand(() =>
        {
            if (IsDocked) RequestOpenPreviewWindow?.Invoke();
            else RequestClosePreviewWindow?.Invoke();
        });

        ShowExportDialogCommand = new RelayCommand(
            () => RequestShowExportDialog?.Invoke(),
            () => State.Document is not null);

        CopyToClipboardCommand = new RelayCommand(() =>
        {
            if (State.Document is null) return;
            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(State.Document.ToPlainText());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
        }, () => State.Document is not null);

        Settings.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(ImagePanel.FilePath))
            _ = ConvertAsync(ImagePanel.FilePath, Settings.ToSettings());
    }

    private async Task OpenImageAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        await ImagePanel.LoadImageAsync(file.Path);
        ConvertCommand.RaiseCanExecuteChanged();
        StatusMessage = $"Loaded {System.IO.Path.GetFileName(file.Path)}";
        await ConvertAsync(file.Path, Settings.ToSettings());
    }

    public async Task ConvertAsync(string path, ConversionSettings settings)
    {
        if (string.IsNullOrEmpty(path)) return;

        _conversionCts?.Cancel();
        _conversionCts = new CancellationTokenSource();
        var ct = _conversionCts.Token;

        State = State with
        {
            Status = ConversionStatus.Converting,
            ProgressPercent = 0,
            ErrorMessage = null
        };
        StatusMessage = "Converting…";

        var progress = new Progress<int>(pct =>
            _dispatcher.TryEnqueue(() =>
            {
                State = State with { ProgressPercent = pct };
            }));

        try
        {
            var doc = await Task.Run(() => _pipeline.ConvertAsync(path, settings, progress, ct), ct);
            State = new ConversionState(path, settings, doc, ConversionStatus.Done, 100, null);
            StatusMessage = $"{System.IO.Path.GetFileName(path)} — {doc.Lines.Count} rows";
            Export.SetDocument(doc);
            ShowExportDialogCommand.RaiseCanExecuteChanged();
            CopyToClipboardCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            // A new conversion was started — ignore
        }
        catch (Exception ex)
        {
            State = State with { Status = ConversionStatus.Error, ErrorMessage = ex.Message };
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public void NotifyPreviewWindowOpened()  { IsDocked = false; }
    public void NotifyPreviewWindowClosed()  { IsDocked = true; }

    private void SyncPanels()
    {
        AsciiOutput.UpdateFromState(_state);
    }

    public void Dispose()
    {
        _conversionCts?.Dispose();
        _pipeline.Dispose();
    }
}
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds. If `System.Web.HttpUtility` fails, apply the `HtmlEncode` inline helper described in the ExportViewModel note above.

- [ ] **Step 7: Commit**

```bash
git add samples/Snaipe.SampleApp/ViewModels/
git commit -m "feat(sample): add all ViewModels (Shell, ImagePanel, AsciiOutput, Settings, Export)"
```

---

### Task 11: MainWindow — full ASCII Studio layout

**Files:**
- Modify: `samples/Snaipe.SampleApp/MainWindow.xaml` — complete replacement
- Modify: `samples/Snaipe.SampleApp/MainWindow.xaml.cs` — complete replacement

- [ ] **Step 1: Replace MainWindow.xaml**

```xml
<!-- samples/Snaipe.SampleApp/MainWindow.xaml -->
<Window
    x:Class="Snaipe.SampleApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Snaipe.SampleApp.Controls">

    <Grid Background="#0D1117">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- ── Left toolbar ──────────────────────────────────────────── -->
        <Border Grid.Row="0" Grid.Column="0"
                Background="#111827"
                Padding="4,8"
                BorderThickness="0,0,1,0"
                BorderBrush="#1E293B">
            <StackPanel Spacing="4" VerticalAlignment="Top">

                <controls:ToolbarButton x:Name="OpenBtn"
                                        Icon="🖼"
                                        Label="Open"/>

                <controls:ToolbarButton x:Name="ConvertBtn"
                                        Icon="⚙"
                                        Label="Convert"
                                        IsEnabled="False"/>

                <controls:ToolbarButton x:Name="PreviewBtn"
                                        Icon="⛶"
                                        Label="Float"
                                        IsEnabled="False"/>

                <controls:ToolbarButton x:Name="CopyBtn"
                                        Icon="📋"
                                        Label="Copy"
                                        IsEnabled="False"/>

                <controls:ToolbarButton x:Name="ExportBtn"
                                        Icon="💾"
                                        Label="Export"
                                        IsEnabled="False"/>

                <!-- Settings flyout trigger -->
                <controls:ToolbarButton x:Name="SettingsBtn"
                                        Icon="🎛"
                                        Label="Settings">
                    <FlyoutBase.AttachedFlyout>
                        <Flyout Placement="Right">
                            <StackPanel Width="240" Spacing="12" Padding="8">
                                <TextBlock Text="Conversion Settings"
                                           FontWeight="SemiBold"
                                           FontSize="13"/>

                                <StackPanel Spacing="4">
                                    <TextBlock Text="Character Set" FontSize="11" Foreground="#64748B"/>
                                    <controls:CharacterSetPicker x:Name="CharPicker"/>
                                </StackPanel>

                                <StackPanel Spacing="4">
                                    <TextBlock Text="Output Width" FontSize="11" Foreground="#64748B"/>
                                    <NumberBox x:Name="WidthBox"
                                               Minimum="20" Maximum="300"
                                               SpinButtonPlacementMode="Compact"/>
                                </StackPanel>

                                <StackPanel Spacing="4">
                                    <TextBlock Text="Color Mode" FontSize="11" Foreground="#64748B"/>
                                    <ComboBox x:Name="ColorModeBox" HorizontalAlignment="Stretch"/>
                                </StackPanel>

                                <StackPanel Spacing="4">
                                    <TextBlock Text="Dithering" FontSize="11" Foreground="#64748B"/>
                                    <ComboBox x:Name="DitheringBox" HorizontalAlignment="Stretch"/>
                                </StackPanel>

                                <CheckBox x:Name="InvertCheck" Content="Invert brightness"/>
                            </StackPanel>
                        </Flyout>
                    </FlyoutBase.AttachedFlyout>
                </controls:ToolbarButton>

            </StackPanel>
        </Border>

        <!-- ── Main split area ───────────────────────────────────────── -->
        <controls:SplitPanel x:Name="MainSplit"
                             Grid.Row="0" Grid.Column="1"
                             Pane1MinSize="120"
                             Pane2MinSize="120">
            <controls:SplitPanel.Pane1Content>
                <controls:ImagePreviewControl x:Name="ImagePreview"/>
            </controls:SplitPanel.Pane1Content>
            <controls:SplitPanel.Pane2Content>
                <Grid>
                    <controls:AsciiOutputControl x:Name="AsciiOutput"/>
                    <!-- Detach button overlay -->
                    <Button x:Name="DetachBtn"
                            Content="⛶"
                            HorizontalAlignment="Right"
                            VerticalAlignment="Top"
                            Margin="8"
                            Opacity="0.6"
                            FontSize="16"
                            Background="Transparent"
                            BorderThickness="0"
                            ToolTipService.ToolTip="Float in separate window"/>
                </Grid>
            </controls:SplitPanel.Pane2Content>
        </controls:SplitPanel>

        <!-- ── Status bar ─────────────────────────────────────────────── -->
        <Border Grid.Row="1" Grid.ColumnSpan="2"
                Background="#0F172A"
                Padding="12,4"
                BorderThickness="0,1,0,0"
                BorderBrush="#1E293B">
            <TextBlock x:Name="StatusText"
                       FontSize="11"
                       Foreground="#64748B"
                       FontFamily="Cascadia Code, Consolas, monospace"/>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Replace MainWindow.xaml.cs**

```csharp
// samples/Snaipe.SampleApp/MainWindow.xaml.cs
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.SampleApp.ViewModels;
using Snaipe.SampleApp.Windows;

namespace Snaipe.SampleApp;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    private AsciiPreviewWindow? _previewWindow;

    public MainWindow()
    {
        ViewModel = new ShellViewModel(DispatcherQueue.GetForCurrentThread());

        ViewModel.RequestOpenPreviewWindow = OpenPreviewWindow;
        ViewModel.RequestClosePreviewWindow = ClosePreviewWindow;
        ViewModel.RequestShowExportDialog = async () => await ShowExportDialogAsync();

        InitializeComponent();

        // Bind controls to VMs
        ImagePreview.DataContext = ViewModel.ImagePanel;
        AsciiOutput.DataContext  = ViewModel.AsciiOutput;

        // Bind toolbar buttons to commands
        OpenBtn.PointerPressed    += (_, _) => ViewModel.OpenImageCommand.Execute(null);
        ConvertBtn.PointerPressed += (_, _) => ViewModel.ConvertCommand.Execute(null);
        CopyBtn.PointerPressed    += (_, _) => ViewModel.CopyToClipboardCommand.Execute(null);
        ExportBtn.PointerPressed  += (_, _) => ViewModel.ShowExportDialogCommand.Execute(null);
        DetachBtn.Click           += (_, _) => ViewModel.TogglePreviewWindowCommand.Execute(null);
        SettingsBtn.PointerPressed += (_, _) =>
            FlyoutBase.GetAttachedFlyout(SettingsBtn)?.ShowAt(SettingsBtn);

        // Populate settings controls
        PopulateSettingsControls();

        // Wire CharacterSetPicker
        CharPicker.SelectedCharacterSet = ViewModel.Settings.CharacterSet;
        CharPicker.SelectionChanged += (_, cs) => ViewModel.Settings.CharacterSet = cs;

        // Wire WidthBox
        WidthBox.Value = ViewModel.Settings.OutputWidth;
        WidthBox.ValueChanged += (_, e) => ViewModel.Settings.OutputWidth = (int)e.NewValue;

        // Wire status text
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.StatusMessage))
                StatusText.Text = ViewModel.StatusMessage;
            if (e.PropertyName == nameof(ShellViewModel.State))
                UpdateToolbarState();
        };

        StatusText.Text = ViewModel.StatusMessage;

        // Bind AsciiOutput control properties to AsciiOutputViewModel
        ViewModel.AsciiOutput.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AsciiOutputViewModel.Document))
                AsciiOutput.Document = ViewModel.AsciiOutput.Document;
            if (e.PropertyName is nameof(AsciiOutputViewModel.DisplayStatus))
                AsciiOutput.Status = ViewModel.AsciiOutput.DisplayStatus;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ProgressPercent))
                AsciiOutput.ProgressPercent = ViewModel.AsciiOutput.ProgressPercent;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ErrorMessage))
                AsciiOutput.ErrorMessage = ViewModel.AsciiOutput.ErrorMessage;
        };

        // Bind ImagePreview control properties to ImagePanelViewModel
        ViewModel.ImagePanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ImagePanelViewModel.SourceImage))
                ImagePreview.Source = ViewModel.ImagePanel.SourceImage;
            if (e.PropertyName is nameof(ImagePanelViewModel.ZoomLevel))
                ImagePreview.ZoomLevel = ViewModel.ImagePanel.ZoomLevel;
        };
        ImagePreview.ZoomLevelProperty.GetMetadata(typeof(ImagePreviewControl));
    }

    private void PopulateSettingsControls()
    {
        ColorModeBox.ItemsSource = Enum.GetValues<ColorMode>();
        ColorModeBox.SelectedItem = ViewModel.Settings.ColorMode;
        ColorModeBox.SelectionChanged += (_, _) =>
        {
            if (ColorModeBox.SelectedItem is ColorMode cm)
                ViewModel.Settings.ColorMode = cm;
        };

        DitheringBox.ItemsSource = Enum.GetValues<DitheringAlgorithm>();
        DitheringBox.SelectedItem = ViewModel.Settings.Dithering;
        DitheringBox.SelectionChanged += (_, _) =>
        {
            if (DitheringBox.SelectedItem is DitheringAlgorithm d)
                ViewModel.Settings.Dithering = d;
        };

        InvertCheck.IsChecked = ViewModel.Settings.Invert;
        InvertCheck.Checked   += (_, _) => ViewModel.Settings.Invert = true;
        InvertCheck.Unchecked += (_, _) => ViewModel.Settings.Invert = false;
    }

    private void UpdateToolbarState()
    {
        bool hasImage    = !string.IsNullOrEmpty(ViewModel.ImagePanel.FilePath);
        bool hasDocument = ViewModel.State.Document is not null;
        ConvertBtn.IsEnabled = hasImage;
        PreviewBtn.IsEnabled = hasImage;
        CopyBtn.IsEnabled    = hasDocument;
        ExportBtn.IsEnabled  = hasDocument;
        PreviewBtn.IsActive  = !ViewModel.IsDocked;
    }

    private void OpenPreviewWindow()
    {
        _previewWindow = new AsciiPreviewWindow(ViewModel.AsciiOutput);
        _previewWindow.Closed += (_, _) =>
        {
            ViewModel.NotifyPreviewWindowClosed();
            _previewWindow = null;
            UpdateToolbarState();
        };
        ViewModel.NotifyPreviewWindowOpened();
        _previewWindow.Activate();
        UpdateToolbarState();
    }

    private void ClosePreviewWindow()
    {
        _previewWindow?.Close();
    }

    private async Task ShowExportDialogAsync()
    {
        ViewModel.Export.SetDocument(ViewModel.State.Document);
        var dialog = new Dialogs.ExportDialog(ViewModel.Export) { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }
}
```

> **Note:** The last line in the constructor (`ImagePreview.ZoomLevelProperty.GetMetadata(...)`) is a dummy call that can be removed — it was a placeholder. Remove it if it causes a compile error.

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds. Fix any namespace or type mismatches that appear.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/MainWindow.xaml samples/Snaipe.SampleApp/MainWindow.xaml.cs
git commit -m "feat(sample): implement MainWindow with toolbar, SplitPanel, and VM wiring"
```

---

### Task 12: AsciiPreviewWindow

**Files:**
- Create: `samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml`
- Create: `samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml.cs`

- [ ] **Step 1: Create AsciiPreviewWindow.xaml**

```xml
<!-- samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml -->
<Window
    x:Class="Snaipe.SampleApp.Windows.AsciiPreviewWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Snaipe.SampleApp.Controls">

    <Grid Background="#020617">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <controls:AsciiOutputControl x:Name="PreviewOutput"
                                     Grid.Row="0"/>

        <Border Grid.Row="1"
                Background="#0F172A"
                Padding="12,4"
                BorderThickness="0,1,0,0"
                BorderBrush="#1E293B">
            <StackPanel Orientation="Horizontal" Spacing="16">
                <TextBlock x:Name="InfoText"
                           FontSize="11"
                           Foreground="#64748B"
                           FontFamily="Cascadia Code, Consolas, monospace"
                           VerticalAlignment="Center"/>
                <Slider x:Name="FontSizeSlider"
                        Minimum="6" Maximum="24" StepFrequency="1"
                        Width="120"
                        VerticalAlignment="Center"
                        ToolTipService.ToolTip="Font size"/>
                <TextBlock Text="Font size"
                           FontSize="11"
                           Foreground="#64748B"
                           VerticalAlignment="Center"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Create AsciiPreviewWindow.xaml.cs**

```csharp
// samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml.cs
using Microsoft.UI.Xaml;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Windows;

public sealed partial class AsciiPreviewWindow : Window
{
    private readonly AsciiOutputViewModel _vm;

    public AsciiPreviewWindow(AsciiOutputViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Title = "ASCII Studio — Preview";

        // Sync initial state
        SyncFromVm();

        // Subscribe to VM changes
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AsciiOutputViewModel.Document))
            {
                PreviewOutput.Document = _vm.Document;
                UpdateInfoText();
            }
            if (e.PropertyName is nameof(AsciiOutputViewModel.DisplayStatus))
                PreviewOutput.Status = _vm.DisplayStatus;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ProgressPercent))
                PreviewOutput.ProgressPercent = _vm.ProgressPercent;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ErrorMessage))
                PreviewOutput.ErrorMessage = _vm.ErrorMessage;
        };

        // Font size slider
        FontSizeSlider.Value = _vm.OutputFontSize;
        FontSizeSlider.ValueChanged += (_, e) =>
        {
            _vm.OutputFontSize = e.NewValue;
            PreviewOutput.OutputFontSize = e.NewValue;
        };
    }

    private void SyncFromVm()
    {
        PreviewOutput.Document       = _vm.Document;
        PreviewOutput.Status         = _vm.DisplayStatus;
        PreviewOutput.ProgressPercent = _vm.ProgressPercent;
        PreviewOutput.ErrorMessage   = _vm.ErrorMessage;
        PreviewOutput.OutputFontSize  = _vm.OutputFontSize;
        FontSizeSlider.Value         = _vm.OutputFontSize;
        UpdateInfoText();
    }

    private void UpdateInfoText()
    {
        if (_vm.Document is { Lines.Count: > 0 } doc)
        {
            int cols = doc.Lines.Max(l => l.Spans.Sum(s => s.Text.Length));
            InfoText.Text = $"{doc.Lines.Count} rows × {cols} cols";
        }
        else
        {
            InfoText.Text = "No output";
        }
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add samples/Snaipe.SampleApp/Windows/
git commit -m "feat(sample): add AsciiPreviewWindow (detachable float)"
```

---

### Task 13: ExportDialog

**Files:**
- Create: `samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml`
- Create: `samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml.cs`

- [ ] **Step 1: Create ExportDialog.xaml**

```xml
<!-- samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml -->
<ContentDialog
    x:Class="Snaipe.SampleApp.Dialogs.ExportDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Export ASCII Art"
    PrimaryButtonText="Export"
    CloseButtonText="Cancel"
    DefaultButton="Primary">

    <StackPanel Spacing="16" MinWidth="320">

        <StackPanel Spacing="6">
            <TextBlock Text="Format" FontSize="12" Foreground="#64748B"/>
            <RadioButtons x:Name="FormatRadios">
                <RadioButton x:Name="PlainTextRadio" Content="Plain text (.txt)" IsChecked="True"/>
                <RadioButton x:Name="HtmlRadio" Content="Colored HTML (.html)"/>
                <RadioButton x:Name="ClipboardRadio" Content="Copy to clipboard"/>
            </RadioButtons>
        </StackPanel>

        <StackPanel x:Name="FilePathPanel" Spacing="6">
            <TextBlock Text="Save location" FontSize="12" Foreground="#64748B"/>
            <Grid ColumnSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name="PathBox"
                         Grid.Column="0"
                         PlaceholderText="Choose save location…"
                         IsReadOnly="True"/>
                <Button x:Name="BrowseBtn"
                        Grid.Column="1"
                        Content="Browse…"/>
            </Grid>
        </StackPanel>

    </StackPanel>
</ContentDialog>
```

- [ ] **Step 2: Create ExportDialog.xaml.cs**

```csharp
// samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Dialogs;

public sealed partial class ExportDialog : ContentDialog
{
    private readonly ExportViewModel _vm;

    public ExportDialog(ExportViewModel vm)
    {
        _vm = vm;
        InitializeComponent();

        // Sync path box
        PathBox.Text = _vm.DestinationPath;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExportViewModel.DestinationPath))
                PathBox.Text = _vm.DestinationPath;
        };

        // Format radio buttons
        PlainTextRadio.Checked  += (_, _) => { _vm.SelectedFormat = ExportFormat.PlainText; UpdatePathVisibility(); };
        HtmlRadio.Checked       += (_, _) => { _vm.SelectedFormat = ExportFormat.HtmlColor;  UpdatePathVisibility(); };
        ClipboardRadio.Checked  += (_, _) => { _vm.SelectedFormat = ExportFormat.Clipboard;  UpdatePathVisibility(); };

        // Browse button
        BrowseBtn.Click += async (_, _) => await _vm.BrowseCommand.ExecuteAsync();

        // Primary button triggers export
        PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true; // keep dialog open until export completes
            await _vm.ExportCommand.ExecuteAsync();
            Hide();
        };
    }

    private void UpdatePathVisibility()
    {
        FilePathPanel.Visibility = _vm.SelectedFormat == ExportFormat.Clipboard
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
```

> **Note:** `ExportCommand.ExecuteAsync()` and `BrowseCommand.ExecuteAsync()` don't exist on `AsyncRelayCommand` (it only has `Execute(object?)`). Add a helper to `AsyncRelayCommand`:
>
> ```csharp
> public Task ExecuteAsync() => _execute();
> ```
>
> This allows the dialog to await the operation cleanly.

- [ ] **Step 3: Build final check**

```bash
dotnet build samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows -v quiet
```

Expected: succeeds with no errors.

- [ ] **Step 4: Run the app and verify manually**

```bash
dotnet run --project samples/Snaipe.SampleApp/Snaipe.SampleApp.csproj -f net9.0-windows
```

Check:
- App opens with side-by-side layout and toolbar
- Click "Open" → file picker opens → select any PNG/JPG → image loads in left pane, conversion runs, ASCII art appears in right pane
- Change character set via Settings flyout → re-conversion fires automatically
- Click "Float" → `AsciiPreviewWindow` opens as a separate window with the same output
- Click "Export" → `ExportDialog` opens with format picker
- Attach Snaipe Inspector → verify inspector can see both windows and deep control trees

- [ ] **Step 5: Commit**

```bash
git add samples/Snaipe.SampleApp/Dialogs/ samples/Snaipe.SampleApp/ViewModels/AsyncRelayCommand.cs
git commit -m "feat(sample): add ExportDialog (ContentDialog) and ExecuteAsync helper"
```

---

## Self-Review Notes

**Spec coverage check:**
- ✅ Five custom controls: SplitPanel, ToolbarButton, ImagePreviewControl, AsciiOutputControl, CharacterSetPicker
- ✅ Two windows: MainWindow, AsciiPreviewWindow
- ✅ One modal dialog: ExportDialog (ContentDialog)
- ✅ Conversion pipeline: ImageSharp, all dithering modes, all char sets, all color modes
- ✅ Visual states: ToolbarButton (5 states across 2 groups), AsciiOutputControl (4 states), ImagePreviewControl (3 states)
- ✅ All inspector coverage rows from spec (ControlTemplate, ItemTemplate, DataContext drill, DP chain, visual states)

**Known build-time issues to watch for:**
1. `System.Web.HttpUtility` — replace with inline `HtmlEncode` helper if missing (noted in Task 10)
2. `ExportCommand.ExecuteAsync()` — add `ExecuteAsync()` method to `AsyncRelayCommand` (noted in Task 13)
3. The dummy `ZoomLevelProperty.GetMetadata(...)` line in MainWindow.xaml.cs — remove it
4. `FlyoutBase.AttachedFlyout` on `ToolbarButton` — `ToolbarButton` is a custom `Control`, not a `FrameworkElement` subclass that natively supports `FlyoutBase.AttachedFlyout`. If it fails, move the settings flyout to a plain `Button` in the toolbar instead
