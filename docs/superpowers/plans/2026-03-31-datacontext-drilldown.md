# DataContext / ViewModel Drill-Down — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow any object-valued property in the property grid to be drilled into, replacing the grid with that object's CLR properties, with a breadcrumb trail for navigation back.

**Architecture:** Path-based addressing — `GetPropertiesRequest` and `SetPropertyRequest` gain an optional `PropertyPath: string[]`. The agent traverses from the element root using CLR reflection and `DataContext` special-casing. `MainViewModel` owns a `Breadcrumb` stack and two commands (`DrillIntoCommand`, `NavigateToBreadcrumbCommand`). `PropertyGridControl` gains a breadcrumb row and per-row chevron affordance.

**Tech Stack:** .NET 9, C# 13, Uno Platform 6.5 (Skia/WinUI), xUnit 2.9, hand-rolled MVVM (no toolkit), System.Text.Json source generation

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Protocol/ElementNode.cs` | Modify — add `IsObjectValued` to `PropertyEntry` |
| `src/Snaipe.Protocol/Messages.cs` | Modify — add `PropertyPath` to `GetPropertiesRequest` and `SetPropertyRequest` |
| `src/Snaipe.Protocol/ProtocolJsonContext.cs` | Modify — add `[JsonSerializable(typeof(string[]))]` |
| `src/Snaipe.Agent/PropertyReader.cs` | Modify — make `GetValueKind` and `FormatValue` `internal static` |
| `src/Snaipe.Agent/PropertyWriter.cs` | Modify — make `ParseValue` `internal static` |
| `src/Snaipe.Agent/ObjectPropertyReader.cs` | New |
| `src/Snaipe.Agent/ObjectPropertyWriter.cs` | New |
| `src/Snaipe.Agent/PropertyPathResolver.cs` | New |
| `src/Snaipe.Agent/SnaipeAgent.cs` | Modify — route path-bearing requests to new classes |
| `tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj` | New |
| `tests/Snaipe.Agent.Tests/ObjectPropertyReaderTests.cs` | New |
| `tests/Snaipe.Agent.Tests/ObjectPropertyWriterTests.cs` | New |
| `tests/Snaipe.Agent.Tests/PropertyPathResolverTests.cs` | New |
| `src/Snaipe.Inspector/ViewModels/BreadcrumbSegment.cs` | New |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Modify — add `Breadcrumb`, `DrillIntoCommand`, `NavigateToBreadcrumbCommand`, `LoadPropertiesAsync`, update `SetPropertyAsync` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | Modify — add breadcrumb row, chevron affordance |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs` | Modify — add `Host` dependency property |
| `src/Snaipe.Inspector/MainWindow.xaml` | Modify — pass `Host="{x:Bind ViewModel}"` |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | Modify — add breadcrumb/drill tests |

---

## Task 1: Protocol — `IsObjectValued` + `PropertyPath` + serialization

**Files:**
- Modify: `src/Snaipe.Protocol/ElementNode.cs`
- Modify: `src/Snaipe.Protocol/Messages.cs`
- Modify: `src/Snaipe.Protocol/ProtocolJsonContext.cs`

- [ ] **Step 1: Add `IsObjectValued` to `PropertyEntry`**

In `src/Snaipe.Protocol/ElementNode.cs`, add one property to `PropertyEntry` after `EnumValues`:

```csharp
    /// <summary>
    /// True when ValueKind is "Object" and the value is non-null.
    /// Inspector renders a drill-down chevron for these rows.
    /// </summary>
    public bool IsObjectValued { get; init; }
```

- [ ] **Step 2: Add `PropertyPath` to `GetPropertiesRequest`**

In `src/Snaipe.Protocol/Messages.cs`, replace the existing `GetPropertiesRequest` record:

```csharp
public sealed record GetPropertiesRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>
    /// CLR property names to traverse from the element root before reading properties.
    /// Null or empty = read the element's own properties (existing behaviour).
    /// </summary>
    public string[]? PropertyPath { get; init; }
}
```

- [ ] **Step 3: Add `PropertyPath` to `SetPropertyRequest`**

Replace the existing `SetPropertyRequest` record:

```csharp
public sealed record SetPropertyRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>
    /// CLR property names to traverse from the element root to reach the parent object.
    /// Null or empty = set a property on the element itself (existing behaviour).
    /// </summary>
    public string[]? PropertyPath { get; init; }
    public required string PropertyName { get; init; }
    public required string NewValue { get; init; }
}
```

- [ ] **Step 4: Register `string[]` in the JSON source-gen context**

In `src/Snaipe.Protocol/ProtocolJsonContext.cs`, add one attribute before the class declaration:

```csharp
[JsonSerializable(typeof(string[]))]
```

The full attribute list should now include this alongside the existing entries.

- [ ] **Step 5: Build Protocol to verify no errors**

```bash
dotnet build src/Snaipe.Protocol/Snaipe.Protocol.csproj -v quiet
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Protocol/ElementNode.cs src/Snaipe.Protocol/Messages.cs src/Snaipe.Protocol/ProtocolJsonContext.cs
git commit -m "feat: add IsObjectValued to PropertyEntry, PropertyPath to request messages"
```

---

## Task 2: Agent — expose shared helpers as `internal`

`ObjectPropertyReader` and `ObjectPropertyWriter` (Tasks 3–4) need to call `PropertyReader.GetValueKind`, `PropertyReader.FormatValue`, and `PropertyWriter.ParseValue`. These are currently `private static`. This task changes their visibility to `internal static` so sibling classes in the same assembly can call them.

**Files:**
- Modify: `src/Snaipe.Agent/PropertyReader.cs`
- Modify: `src/Snaipe.Agent/PropertyWriter.cs`

- [ ] **Step 1: Make `GetValueKind` and `FormatValue` internal in `PropertyReader`**

In `src/Snaipe.Agent/PropertyReader.cs`, change:

```csharp
private static string GetValueKind(Type type)
```
to:
```csharp
internal static string GetValueKind(Type type)
```

And change:

```csharp
private static string FormatValue(object? value)
```
to:
```csharp
internal static string FormatValue(object? value)
```

- [ ] **Step 2: Make `ParseValue` internal in `PropertyWriter`**

In `src/Snaipe.Agent/PropertyWriter.cs`, change:

```csharp
private static object? ParseValue(string value, Type targetType)
```
to:
```csharp
internal static object? ParseValue(string value, Type targetType)
```

- [ ] **Step 3: Build Agent to verify**

```bash
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Agent/PropertyReader.cs src/Snaipe.Agent/PropertyWriter.cs
git commit -m "refactor: expose GetValueKind, FormatValue, ParseValue as internal for reuse"
```

---

## Task 3: Agent — `ObjectPropertyReader` with test project

**Files:**
- Create: `tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj`
- Create: `src/Snaipe.Agent/ObjectPropertyReader.cs`
- Create: `tests/Snaipe.Agent.Tests/ObjectPropertyReaderTests.cs`

- [ ] **Step 1: Create the agent test project**

Create `tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Uno.WinUI" Version="6.5.153">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Snaipe.Agent\Snaipe.Agent.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Snaipe.Agent.Tests/ObjectPropertyReaderTests.cs`:

```csharp
using Snaipe.Agent;
using Xunit;

namespace Snaipe.Agent.Tests;

public class ObjectPropertyReaderTests
{
    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public Person? Child { get; set; }
        public string? NullProp { get; set; }
        public string ReadOnlyProp => "const";
        public string ThrowingProp => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void GetProperties_ReturnsAllPublicReadableProperties()
    {
        var person = new Person { Name = "Alice", Age = 30 };
        var props = ObjectPropertyReader.GetProperties(person);
        var names = props.Select(p => p.Name).ToList();
        Assert.Contains("Name", names);
        Assert.Contains("Age", names);
        Assert.Contains("IsActive", names);
    }

    [Fact]
    public void GetProperties_AllHaveCategoryProperties()
    {
        var props = ObjectPropertyReader.GetProperties(new Person());
        Assert.All(props, p => Assert.Equal("Properties", p.Category));
    }

    [Fact]
    public void GetProperties_ObjectValuedNonNull_SetsIsObjectValued()
    {
        var person = new Person { Child = new Person { Name = "Bob" } };
        var props = ObjectPropertyReader.GetProperties(person);
        var childProp = props.First(p => p.Name == "Child");
        Assert.True(childProp.IsObjectValued);
        Assert.Equal("Object", childProp.ValueKind);
    }

    [Fact]
    public void GetProperties_ObjectValuedNull_IsObjectValuedFalse()
    {
        var person = new Person { Child = null };
        var props = ObjectPropertyReader.GetProperties(person);
        var childProp = props.First(p => p.Name == "Child");
        Assert.False(childProp.IsObjectValued);
    }

    [Fact]
    public void GetProperties_ReadOnlyProperty_IsReadOnlyTrue()
    {
        var props = ObjectPropertyReader.GetProperties(new Person());
        var ro = props.First(p => p.Name == "ReadOnlyProp");
        Assert.True(ro.IsReadOnly);
    }

    [Fact]
    public void GetProperties_SkipsThrowingProperties()
    {
        // Should not throw; ThrowingProp silently absent
        var props = ObjectPropertyReader.GetProperties(new Person());
        Assert.DoesNotContain(props, p => p.Name == "ThrowingProp");
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ObjectPropertyReaderTests" 2>&1 | tail -5
```

Expected: build error — `ObjectPropertyReader does not exist`.

- [ ] **Step 4: Create `ObjectPropertyReader`**

Create `src/Snaipe.Agent/ObjectPropertyReader.cs`:

```csharp
using System.Reflection;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Reads all public instance CLR properties from an arbitrary object using reflection.
/// Used when the inspector drills into a nested ViewModel or DataContext.
/// </summary>
public static class ObjectPropertyReader
{
    public static List<PropertyEntry> GetProperties(object obj)
    {
        var results = new List<PropertyEntry>();
        var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(obj);
                var effectiveType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var kind = PropertyReader.GetValueKind(prop.PropertyType);

                results.Add(new PropertyEntry
                {
                    Name        = prop.Name,
                    Category    = "Properties",
                    ValueType   = prop.PropertyType.Name,
                    Value       = PropertyReader.FormatValue(value),
                    ValueKind   = kind,
                    IsReadOnly  = !prop.CanWrite,
                    IsObjectValued = kind == "Object" && value is not null,
                    EnumValues  = effectiveType.IsEnum
                        ? Enum.GetNames(effectiveType).ToList()
                        : null,
                });
            }
            catch
            {
                // Skip properties whose getter throws.
            }
        }

        return results;
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ObjectPropertyReaderTests" 2>&1 | tail -5
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Agent/ObjectPropertyReader.cs tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj tests/Snaipe.Agent.Tests/ObjectPropertyReaderTests.cs
git commit -m "feat: add ObjectPropertyReader for CLR object property inspection"
```

---

## Task 4: Agent — `ObjectPropertyWriter` with tests

**Files:**
- Create: `src/Snaipe.Agent/ObjectPropertyWriter.cs`
- Create: `tests/Snaipe.Agent.Tests/ObjectPropertyWriterTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Snaipe.Agent.Tests/ObjectPropertyWriterTests.cs`:

```csharp
using Snaipe.Agent;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class ObjectPropertyWriterTests
{
    private class Settings
    {
        public string Title { get; set; } = "";
        public int Count { get; set; }
        public bool Enabled { get; set; }
        public string ReadOnly => "fixed";
    }

    [Fact]
    public void SetProperty_WritesStringValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Title", "Hello");
        Assert.True(result.Success);
        Assert.Equal("Hello", s.Title);
    }

    [Fact]
    public void SetProperty_WritesIntValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Count", "42");
        Assert.True(result.Success);
        Assert.Equal(42, s.Count);
    }

    [Fact]
    public void SetProperty_WritesBoolValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Enabled", "True");
        Assert.True(result.Success);
        Assert.True(s.Enabled);
    }

    [Fact]
    public void SetProperty_UnknownProperty_ReturnsPropertyNotFoundError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "DoesNotExist", "x");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PropertyNotFound, result.ErrorCode);
    }

    [Fact]
    public void SetProperty_ReadOnlyProperty_ReturnsPropertyReadOnlyError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "ReadOnly", "x");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PropertyReadOnly, result.ErrorCode);
    }

    [Fact]
    public void SetProperty_InvalidValue_ReturnsInvalidPropertyValueError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Count", "not-a-number");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidPropertyValue, result.ErrorCode);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ObjectPropertyWriterTests" 2>&1 | tail -5
```

Expected: build error — `ObjectPropertyWriter does not exist`.

- [ ] **Step 3: Create `ObjectPropertyWriter`**

Create `src/Snaipe.Agent/ObjectPropertyWriter.cs`:

```csharp
using System.Reflection;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Writes a single public instance CLR property on an arbitrary object using reflection.
/// Used when the inspector edits a property while drilled into a nested ViewModel.
/// </summary>
public static class ObjectPropertyWriter
{
    public static SetPropertyResult SetProperty(object obj, string propertyName, string newValue)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
            return new SetPropertyResult(false, ErrorCodes.PropertyNotFound,
                "Property not found",
                $"No property '{propertyName}' on {obj.GetType().Name}");

        if (!prop.CanWrite)
            return new SetPropertyResult(false, ErrorCodes.PropertyReadOnly,
                "Property is read-only",
                $"'{propertyName}' does not have a setter.");

        object? parsedValue;
        try
        {
            parsedValue = PropertyWriter.ParseValue(newValue, prop.PropertyType);
        }
        catch (Exception ex)
        {
            return new SetPropertyResult(false, ErrorCodes.InvalidPropertyValue,
                "Invalid property value",
                $"Cannot parse '{newValue}' as {prop.PropertyType.Name}: {ex.Message}");
        }

        prop.SetValue(obj, parsedValue);
        return new SetPropertyResult(true, NormalizedValue: parsedValue?.ToString() ?? newValue);
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ObjectPropertyWriterTests" 2>&1 | tail -5
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Agent/ObjectPropertyWriter.cs tests/Snaipe.Agent.Tests/ObjectPropertyWriterTests.cs
git commit -m "feat: add ObjectPropertyWriter for CLR object property mutation"
```

---

## Task 5: Agent — `PropertyPathResolver` with tests

**Files:**
- Create: `src/Snaipe.Agent/PropertyPathResolver.cs`
- Create: `tests/Snaipe.Agent.Tests/PropertyPathResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Snaipe.Agent.Tests/PropertyPathResolverTests.cs`:

```csharp
using Snaipe.Agent;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class PropertyPathResolverTests
{
    // Test classes for CLR traversal
    private class Address { public string City { get; set; } = ""; }
    private class Person
    {
        public string Name { get; set; } = "";
        public Address? Address { get; set; }
    }
    private class Root { public Person? Owner { get; set; } }

    [Fact]
    public void TraversePath_EmptySegments_ReturnsRoot()
    {
        var root = new Root { Owner = new Person { Name = "Alice" } };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, [], 0);
        Assert.Equal(root, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_SingleSegment_ReturnsPropertyValue()
    {
        var person = new Person { Name = "Alice" };
        var root = new Root { Owner = person };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner"], 0);
        Assert.Equal(person, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_MultiSegment_TraversesChain()
    {
        var addr = new Address { City = "Paris" };
        var person = new Person { Address = addr };
        var root = new Root { Owner = person };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner", "Address"], 0);
        Assert.Equal(addr, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_NullSegment_ReturnsElementNotFoundError()
    {
        var root = new Root { Owner = null };  // Owner is null
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner", "Address"], 0);
        Assert.Null(value);
        Assert.Equal(ErrorCodes.ElementNotFound, errorCode);
    }

    [Fact]
    public void TraversePath_MissingProperty_ReturnsElementNotFoundError()
    {
        var root = new Root();
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["DoesNotExist"], 0);
        Assert.Null(value);
        Assert.Equal(ErrorCodes.ElementNotFound, errorCode);
    }

    [Fact]
    public void TraversePath_StartIndex_SkipsEarlierSegments()
    {
        // startIndex=1 skips "Owner", starts at "Address"
        var addr = new Address { City = "Paris" };
        var person = new Person { Address = addr };
        // Pass person as root (already resolved), skip 0 segments
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(person, ["Address"], 0);
        Assert.Equal(addr, value);
        Assert.Equal(0, errorCode);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyPathResolverTests" 2>&1 | tail -5
```

Expected: build error — `PropertyPathResolver does not exist`.

- [ ] **Step 3: Create `PropertyPathResolver`**

Create `src/Snaipe.Agent/PropertyPathResolver.cs`:

```csharp
using System.Reflection;
using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Resolves a string property path from a UIElement root to a nested object.
/// The first segment "DataContext" is resolved via DependencyProperty;
/// all other segments use CLR reflection.
/// </summary>
public static class PropertyPathResolver
{
    /// <summary>
    /// Resolves a property path starting from a UI element.
    /// Returns (object, 0, null) on success or (null, errorCode, message) on failure.
    /// </summary>
    public static (object? Value, int ErrorCode, string? Error) Resolve(
        DependencyObject element, string[]? path)
    {
        if (path is null or { Length: 0 })
            return (element, 0, null);

        // "DataContext" as first segment is special — resolved via DependencyProperty.
        object? startObject;
        int startIndex;
        if (path[0] == "DataContext" && element is FrameworkElement fe)
        {
            startObject = fe.DataContext;
            if (startObject is null)
                return (null, ErrorCodes.ElementNotFound, "DataContext is null");
            startIndex = 1;
        }
        else
        {
            startObject = element;
            startIndex = 0;
        }

        return TraversePath(startObject, path, startIndex);
    }

    /// <summary>
    /// Traverses CLR properties on <paramref name="root"/> starting at <paramref name="startIndex"/>.
    /// Exposed as internal for unit testing with plain POCOs.
    /// </summary>
    internal static (object? Value, int ErrorCode, string? Error) TraversePath(
        object root, string[] segments, int startIndex)
    {
        var current = root;
        for (int i = startIndex; i < segments.Length; i++)
        {
            var prop = current.GetType().GetProperty(segments[i],
                BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
                return (null, ErrorCodes.ElementNotFound,
                    $"Property '{segments[i]}' not found on {current.GetType().Name}");

            var next = prop.GetValue(current);
            if (next is null)
                return (null, ErrorCodes.ElementNotFound,
                    $"Property '{segments[i]}' is null");

            current = next;
        }
        return (current, 0, null);
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyPathResolverTests" 2>&1 | tail -5
```

Expected: `6 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Agent/PropertyPathResolver.cs tests/Snaipe.Agent.Tests/PropertyPathResolverTests.cs
git commit -m "feat: add PropertyPathResolver for CLR property path traversal"
```

---

## Task 6: Agent — wire path support in `SnaipeAgent`

**Files:**
- Modify: `src/Snaipe.Agent/SnaipeAgent.cs`

- [ ] **Step 1: Refactor `HandleGetProperties` to support `PropertyPath`**

In `src/Snaipe.Agent/SnaipeAgent.cs`, replace the `HandleGetProperties` method entirely:

```csharp
private Task<InspectorMessage> HandleGetProperties(GetPropertiesRequest request)
{
    var tcs = new TaskCompletionSource<InspectorMessage>();

    _window.DispatcherQueue.TryEnqueue(() =>
    {
        try
        {
            if (!_tracker.TryGetElement(request.ElementId, out var element) || element is null)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.ElementNotFound,
                    Error = "Element not found",
                    Details = $"ID: {request.ElementId}",
                });
                return;
            }

            List<Protocol.PropertyEntry> properties;

            if (request.PropertyPath is { Length: > 0 })
            {
                // Drill-down path: resolve to the nested object and read its CLR properties.
                var (resolved, errorCode, errorMessage) =
                    PropertyPathResolver.Resolve(element, request.PropertyPath);

                if (resolved is null)
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = errorCode,
                        Error = errorMessage ?? "Path resolution failed",
                    });
                    return;
                }

                properties = ObjectPropertyReader.GetProperties(resolved);
            }
            else
            {
                // Root-level: existing DependencyProperty reader.
                properties = PropertyReader.GetProperties(element);

                // Prepend live bounds info (only for direct element inspection).
                if (_window.Content is UIElement root)
                {
                    var bounds = VisualTreeWalker.GetBoundsRelativeTo(element, root);
                    properties.Insert(0, new Protocol.PropertyEntry
                    {
                        Name = "Bounds (X)", Category = "Layout", ValueType = "Double",
                        Value = bounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ValueKind = "Number", IsReadOnly = true,
                    });
                    properties.Insert(1, new Protocol.PropertyEntry
                    {
                        Name = "Bounds (Y)", Category = "Layout", ValueType = "Double",
                        Value = bounds.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ValueKind = "Number", IsReadOnly = true,
                    });
                }
            }

            tcs.SetResult(new PropertiesResponse
            {
                MessageId = request.MessageId,
                ElementId = request.ElementId,
                Properties = properties,
            });
        }
        catch (Exception ex)
        {
            tcs.SetResult(new ErrorResponse
            {
                MessageId = request.MessageId,
                ErrorCode = ErrorCodes.InternalError,
                Error = "Property read failed",
                Details = ex.Message,
            });
        }
    });

    return tcs.Task;
}
```

- [ ] **Step 2: Refactor `HandleSetProperty` to support `PropertyPath`**

Replace the `HandleSetProperty` method entirely:

```csharp
private Task<InspectorMessage> HandleSetProperty(SetPropertyRequest request)
{
    var tcs = new TaskCompletionSource<InspectorMessage>();

    _window.DispatcherQueue.TryEnqueue(() =>
    {
        try
        {
            if (!_tracker.TryGetElement(request.ElementId, out var element) || element is null)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.ElementNotFound,
                    Error = "Element not found",
                    Details = $"ID: {request.ElementId}",
                });
                return;
            }

            SetPropertyResult result;

            if (request.PropertyPath is { Length: > 0 })
            {
                // Drill-down path: resolve to the nested object, then write the leaf property.
                var (resolved, errorCode, errorMessage) =
                    PropertyPathResolver.Resolve(element, request.PropertyPath);

                if (resolved is null)
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = errorCode,
                        Error = errorMessage ?? "Path resolution failed",
                    });
                    return;
                }

                result = ObjectPropertyWriter.SetProperty(resolved, request.PropertyName, request.NewValue);
            }
            else
            {
                result = PropertyWriter.SetProperty(element, request.PropertyName, request.NewValue);
            }

            if (!result.Success)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = result.ErrorCode,
                    Error = result.Error ?? "Error",
                    Details = result.Details,
                });
                return;
            }

            tcs.SetResult(new AckResponse
            {
                MessageId = request.MessageId,
                NormalizedValue = result.NormalizedValue,
            });
        }
        catch (Exception ex)
        {
            tcs.SetResult(new ErrorResponse
            {
                MessageId = request.MessageId,
                ErrorCode = ErrorCodes.InternalError,
                Error = "Property write failed",
                Details = ex.Message,
            });
        }
    });

    return tcs.Task;
}
```

- [ ] **Step 3: Build Agent to verify**

```bash
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Agent/SnaipeAgent.cs
git commit -m "feat: route PropertyPath requests to ObjectPropertyReader/Writer in SnaipeAgent"
```

---

## Task 7: Inspector — `BreadcrumbSegment` + `MainViewModel` drill-down

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/BreadcrumbSegment.cs`
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Create `BreadcrumbSegment`**

Create `src/Snaipe.Inspector/ViewModels/BreadcrumbSegment.cs`:

```csharp
namespace Snaipe.Inspector.ViewModels;

/// <summary>
/// One entry in the property grid's navigation breadcrumb trail.
/// <see cref="Path"/> is the full PropertyPath needed to reach this level from the element root.
/// </summary>
public record BreadcrumbSegment(string Label, string[] Path);
```

- [ ] **Step 2: Add `Breadcrumb`, `DrillIntoCommand`, `NavigateToBreadcrumbCommand` to `MainViewModel`**

In `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`:

**a) Add the collection property** after the existing `PropertyGrid` property in the `// ── Collections` section:

```csharp
public ObservableCollection<BreadcrumbSegment> Breadcrumb { get; } = [];
```

**b) Add the two commands** in the `// ── Commands` section (after `RefreshTreeCommand`):

```csharp
public RelayCommand<PropertyRowViewModel> DrillIntoCommand { get; }
public RelayCommand<BreadcrumbSegment>   NavigateToBreadcrumbCommand { get; }
```

**c) Wire the commands in the constructor** — add these two lines after the `RefreshAgents()` call:

```csharp
DrillIntoCommand = new RelayCommand<PropertyRowViewModel>(row =>
{
    if (row is null) return;
    var newPath = Breadcrumb.Count > 0
        ? [.. Breadcrumb.Last().Path, row.Entry.Name]
        : new[] { row.Entry.Name };
    Breadcrumb.Add(new BreadcrumbSegment(row.Entry.Name, newPath));
    if (_selectedNode is not null)
        _ = LoadPropertiesAsync(_selectedNode, newPath);
});

NavigateToBreadcrumbCommand = new RelayCommand<BreadcrumbSegment>(crumb =>
{
    if (crumb is null) return;
    var idx = Breadcrumb.IndexOf(crumb);
    if (idx < 0) return;
    while (Breadcrumb.Count > idx + 1)
        Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
    if (_selectedNode is not null)
        _ = LoadPropertiesAsync(_selectedNode, crumb.Path);
});
```

- [ ] **Step 3: Add `LoadPropertiesAsync` private method**

Add this method near `OnSelectedNodeChangedAsync` (it replaces the fetch logic that was inline):

```csharp
private async Task LoadPropertiesAsync(TreeNodeViewModel node, string[] path)
{
    _propertiesCts?.Cancel();
    _propertiesCts = new CancellationTokenSource();
    var ct = _propertiesCts.Token;

    PropertyGrid.Clear();
    IsLoadingProperties = true;
    try
    {
        var response = await _client.SendAsync<PropertiesResponse>(
            new GetPropertiesRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = node.Node.Id,
                PropertyPath = path.Length > 0 ? path : null,
            });

        if (ct.IsCancellationRequested) return;

        var capturedPath = path;
        var rows = response.Properties
            .Select(prop => new PropertyRowViewModel(prop,
                row => SetPropertyAsync(node.Node.Id, capturedPath, row.Entry.Name, row.EditValue, row)))
            .ToList();

        PropertyGrid.Load(rows);

        if (path.Length == 0)
            _ = SendHighlightAsync(node.Node.Id, show: true);
    }
    catch (OperationCanceledException)
    {
        // Superseded — do nothing.
    }
    catch (IOException ex)
    {
        HandleConnectionLost(ex.Message);
    }
    catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.ElementNotFound)
    {
        StatusMessage = path.Length > 0
            ? "Drill-down target no longer available — navigated back to root."
            : "Element no longer in tree — refresh the tree.";
        if (path.Length > 0)
        {
            Breadcrumb.Clear();
            if (_selectedNode is not null)
                Breadcrumb.Add(new BreadcrumbSegment(_selectedNode.Node.TypeName, []));
        }
    }
    finally
    {
        IsLoadingProperties = false;
    }
}
```

- [ ] **Step 4: Replace `OnSelectedNodeChangedAsync` to use `LoadPropertiesAsync`**

Replace the existing `OnSelectedNodeChangedAsync` method with:

```csharp
private async Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node)
{
    Breadcrumb.Clear();
    PropertyGrid.Clear();
    _propertiesCts?.Cancel();

    if (node is null || _state != ConnectionState.Connected) return;

    Breadcrumb.Add(new BreadcrumbSegment(node.Node.TypeName, []));
    await LoadPropertiesAsync(node, []);
}
```

- [ ] **Step 5: Update `ClearSession` to clear breadcrumb**

Replace the existing `ClearSession` method:

```csharp
private void ClearSession()
{
    _propertiesCts?.Cancel();
    RootNodes.Clear();
    PropertyGrid.Clear();
    Breadcrumb.Clear();
    _selectedNode = null;
    OnPropertyChanged(nameof(SelectedNode));
}
```

- [ ] **Step 6: Update `SetPropertyAsync` to accept `propertyPath`**

Replace the existing `SetPropertyAsync` signature and `SetPropertyRequest` construction:

```csharp
public async Task SetPropertyAsync(string elementId, string[]? propertyPath,
    string propertyName, string newValue, PropertyRowViewModel row)
{
    try
    {
        var ack = await _client.SendAsync<AckResponse>(
            new SetPropertyRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = elementId,
                PropertyPath = propertyPath?.Length > 0 ? propertyPath : null,
                PropertyName = propertyName,
                NewValue = newValue,
            });

        row.ClearError();
        if (ack.NormalizedValue != null)
            row.EditValue = ack.NormalizedValue;
    }
    catch (SnaipeProtocolException ex)
    {
        row.SetError(ex.Details ?? ex.Message);
    }
    catch (IOException ex)
    {
        HandleConnectionLost(ex.Message);
    }
    catch (Exception ex)
    {
        row.SetError(ex.Message);
    }
}
```

- [ ] **Step 7: Build Inspector to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s).` (Uno warnings about `LostFocus` and `ProgressRing` are expected and harmless.)

- [ ] **Step 8: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/BreadcrumbSegment.cs src/Snaipe.Inspector/ViewModels/MainViewModel.cs
git commit -m "feat: add breadcrumb navigation and drill-down commands to MainViewModel"
```

---

## Task 8: Inspector — `PropertyGridControl` breadcrumb row + chevron affordance

**Files:**
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs`

- [ ] **Step 1: Add `DrillVisibility` computed property to `PropertyRowViewModel`**

In `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`, add after the `ErrorBorderBrush` property:

```csharp
/// <summary>Visibility for the drill-down chevron button in the Name column.</summary>
public Microsoft.UI.Xaml.Visibility DrillVisibility =>
    Entry.IsObjectValued
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
```

- [ ] **Step 2: Add `Host` dependency property to `PropertyGridControl.xaml.cs`**

Replace the entire `PropertyGridControl.xaml.cs` with:

```csharp
// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    public PropertyGridControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
        SearchBox.TextChanged += OnSearchTextChanged;
    }

    // DataContext is PropertyGridViewModel (set from MainWindow via x:Bind ViewModel.PropertyGrid).
    public PropertyGridViewModel? ViewModel => DataContext as PropertyGridViewModel;

    /// <summary>
    /// Reference to the MainViewModel. Set from MainWindow via x:Bind.
    /// Provides Breadcrumb, DrillIntoCommand, and NavigateToBreadcrumbCommand bindings.
    /// </summary>
    public MainViewModel? Host
    {
        get => (MainViewModel?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public static readonly DependencyProperty HostProperty =
        DependencyProperty.Register(nameof(Host), typeof(MainViewModel),
            typeof(PropertyGridControl), new PropertyMetadata(null));

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.SearchText = SearchBox.Text;
    }
}
```

- [ ] **Step 3: Replace `PropertyGridControl.xaml` with breadcrumb row and chevron affordance**

Replace the entire `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` with:

```xml
<UserControl
    x:Class="Snaipe.Inspector.Controls.PropertyGridControl"
    x:Name="Root"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Snaipe.Inspector.Controls"
    xmlns:vm="using:Snaipe.Inspector.ViewModels">

    <UserControl.Resources>

        <local:PropertyEditorTemplateSelector x:Key="EditorSelector">
            <local:PropertyEditorTemplateSelector.ReadOnlyTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBlock Text="{x:Bind Entry.Value}"
                               FontSize="12"
                               TextTrimming="CharacterEllipsis"
                               VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.ReadOnlyTemplate>

            <local:PropertyEditorTemplateSelector.TextTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBox Text="{x:Bind EditValue, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                             FontSize="12"
                             BorderBrush="{x:Bind ErrorBorderBrush, Mode=OneWay}"
                             BorderThickness="1"
                             ToolTipService.ToolTip="{Binding ErrorMessage}"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.TextTemplate>

            <local:PropertyEditorTemplateSelector.BooleanTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <CheckBox IsChecked="{x:Bind IsCheckedValue, Mode=TwoWay}"
                              VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.BooleanTemplate>

            <local:PropertyEditorTemplateSelector.NumberTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <NumberBox Value="{Binding NumberValue, Mode=TwoWay}"
                               SpinButtonPlacementMode="Compact"
                               FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.NumberTemplate>

            <local:PropertyEditorTemplateSelector.EnumTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <ComboBox ItemsSource="{Binding Entry.EnumValues}"
                              SelectedItem="{Binding EditValue, Mode=TwoWay}"
                              FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.EnumTemplate>
        </local:PropertyEditorTemplateSelector>

        <Style x:Key="ColumnHeaderButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="4,4"/>
            <Setter Property="HorizontalAlignment" Value="Stretch"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>

        <Style x:Key="BreadcrumbButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="2,2"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>

        <Style x:Key="ChevronButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="2,0"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="HorizontalAlignment" Value="Right"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
        </Style>

    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Breadcrumb row — visible when drilled into a nested object -->
        <ScrollViewer Grid.Row="0"
                      HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Disabled"
                      Margin="4,2,4,0">
            <ItemsControl ItemsSource="{x:Bind Host.Breadcrumb, Mode=OneWay}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <StackPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:BreadcrumbSegment">
                        <StackPanel Orientation="Horizontal">
                            <Button Content="{x:Bind Label}"
                                    Command="{Binding ElementName=Root, Path=Host.NavigateToBreadcrumbCommand}"
                                    CommandParameter="{x:Bind}"
                                    Style="{StaticResource BreadcrumbButtonStyle}"/>
                            <TextBlock Text=" › "
                                       FontSize="11"
                                       Opacity="0.5"
                                       VerticalAlignment="Center"/>
                        </StackPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <!-- Search bar -->
        <TextBox x:Name="SearchBox"
                 Grid.Row="1"
                 PlaceholderText="Search properties..."
                 FontSize="12"
                 Margin="4,4,4,2"/>

        <!-- Column headers -->
        <Grid Grid.Row="2"
              Padding="4,2"
              ColumnSpacing="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="1*"/>
                <ColumnDefinition Width="1.5*"/>
                <ColumnDefinition Width="50"/>
            </Grid.ColumnDefinitions>

            <Button Grid.Column="0"
                    Content="{x:Bind ViewModel.NameColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Name"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <TextBlock Grid.Column="1"
                       Text="VALUE"
                       FontSize="11"
                       FontWeight="SemiBold"
                       Padding="4"
                       VerticalAlignment="Center"/>

            <Button Grid.Column="2"
                    Content="{x:Bind ViewModel.TypeColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Type"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <Button Grid.Column="3"
                    Content="{x:Bind ViewModel.CategoryColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Category"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <Button Grid.Column="4"
                    Content="{x:Bind ViewModel.ReadOnlyColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="ReadOnly"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>
        </Grid>

        <!-- Property rows -->
        <ListView Grid.Row="3"
                  ItemsSource="{x:Bind ViewModel.FilteredProperties, Mode=OneWay}"
                  SelectionMode="None">
            <ListView.ItemContainerStyle>
                <Style TargetType="ListViewItem">
                    <Setter Property="Padding" Value="0"/>
                    <Setter Property="MinHeight" Value="0"/>
                    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
                </Style>
            </ListView.ItemContainerStyle>
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <Grid Padding="4,2"
                          ColumnSpacing="4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="2*"/>
                            <ColumnDefinition Width="2*"/>
                            <ColumnDefinition Width="1*"/>
                            <ColumnDefinition Width="1.5*"/>
                            <ColumnDefinition Width="50"/>
                        </Grid.ColumnDefinitions>

                        <!-- Name column: label + drill chevron -->
                        <Grid Grid.Column="0">
                            <TextBlock Text="{x:Bind Entry.Name}"
                                       FontSize="12"
                                       VerticalAlignment="Center"
                                       TextTrimming="CharacterEllipsis"
                                       HorizontalAlignment="Left"
                                       ToolTipService.ToolTip="{x:Bind Entry.Name}"/>
                            <Button Content="›"
                                    Visibility="{x:Bind DrillVisibility}"
                                    Command="{Binding ElementName=Root, Path=Host.DrillIntoCommand}"
                                    CommandParameter="{x:Bind}"
                                    Style="{StaticResource ChevronButtonStyle}"/>
                        </Grid>

                        <ContentPresenter Grid.Column="1"
                                          Content="{x:Bind}"
                                          ContentTemplateSelector="{StaticResource EditorSelector}"/>

                        <TextBlock Grid.Column="2"
                                   Text="{x:Bind Entry.ValueType}"
                                   FontSize="10"
                                   Opacity="0.6"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"/>

                        <TextBlock Grid.Column="3"
                                   Text="{x:Bind Entry.Category}"
                                   FontSize="10"
                                   Opacity="0.6"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"/>

                        <CheckBox Grid.Column="4"
                                  IsChecked="{x:Bind Entry.IsReadOnly}"
                                  IsEnabled="False"
                                  HorizontalAlignment="Center"
                                  VerticalAlignment="Center"/>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

    </Grid>
</UserControl>
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs src/Snaipe.Inspector/Controls/PropertyGridControl.xaml src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
git commit -m "feat: add breadcrumb row and drill-down chevron to PropertyGridControl"
```

---

## Task 9: Inspector — `MainWindow.xaml` + `MainViewModelTests` additions

**Files:**
- Modify: `src/Snaipe.Inspector/MainWindow.xaml`
- Modify: `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Pass `Host` in `MainWindow.xaml`**

In `src/Snaipe.Inspector/MainWindow.xaml`, replace the `PropertyGridControl` element:

```xml
<controls:PropertyGridControl Grid.Row="0"
               DataContext="{x:Bind ViewModel.PropertyGrid}"
               Host="{x:Bind ViewModel}"/>
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Write failing tests for `MainViewModel` breadcrumb behavior**

In `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`, append these test methods inside the `MainViewModelTests` class:

```csharp
    [Fact]
    public void InitialState_BreadcrumbIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.Breadcrumb);
    }

    [Fact]
    public void DrillInto_PushesBreadcrumbSegment()
    {
        var vm = new MainViewModel();
        // Seed breadcrumb as OnSelectedNodeChangedAsync would at root.
        vm.Breadcrumb.Add(new BreadcrumbSegment("Button", []));

        var row = new PropertyRowViewModel(new PropertyEntry
        {
            Name = "DataContext", Category = "Data Context", IsObjectValued = true
        });
        vm.DrillIntoCommand.Execute(row);

        Assert.Equal(2, vm.Breadcrumb.Count);
        Assert.Equal("DataContext", vm.Breadcrumb[1].Label);
        Assert.Equal(new[] { "DataContext" }, vm.Breadcrumb[1].Path);
    }

    [Fact]
    public void DrillInto_NestedLevel_BuildsPathCorrectly()
    {
        var vm = new MainViewModel();
        vm.Breadcrumb.Add(new BreadcrumbSegment("Button", []));
        vm.Breadcrumb.Add(new BreadcrumbSegment("DataContext", ["DataContext"]));

        var row = new PropertyRowViewModel(new PropertyEntry
        {
            Name = "Address", Category = "Properties", IsObjectValued = true
        });
        vm.DrillIntoCommand.Execute(row);

        Assert.Equal(3, vm.Breadcrumb.Count);
        Assert.Equal(new[] { "DataContext", "Address" }, vm.Breadcrumb[2].Path);
    }

    [Fact]
    public void NavigateToBreadcrumb_PopsToClickedCrumb()
    {
        var vm = new MainViewModel();
        var root = new BreadcrumbSegment("Button", []);
        var dc   = new BreadcrumbSegment("DataContext", ["DataContext"]);
        var addr = new BreadcrumbSegment("Address", ["DataContext", "Address"]);
        vm.Breadcrumb.Add(root);
        vm.Breadcrumb.Add(dc);
        vm.Breadcrumb.Add(addr);

        vm.NavigateToBreadcrumbCommand.Execute(dc);

        Assert.Equal(2, vm.Breadcrumb.Count);
        Assert.Equal("DataContext", vm.Breadcrumb[1].Label);
    }

    [Fact]
    public void SelectedNode_SetToNull_ClearsBreadcrumb()
    {
        var vm = new MainViewModel();
        vm.Breadcrumb.Add(new BreadcrumbSegment("Button", []));
        vm.Breadcrumb.Add(new BreadcrumbSegment("DataContext", ["DataContext"]));

        vm.SelectedNode = null;

        Assert.Empty(vm.Breadcrumb);
    }
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows 2>&1 | tail -6
```

Expected: all tests pass (previous 36 + 5 new = 41 passed, 0 failed).

- [ ] **Step 5: Run agent tests to confirm still passing**

```bash
dotnet test tests/Snaipe.Agent.Tests/Snaipe.Agent.Tests.csproj -f net9.0-windows 2>&1 | tail -5
```

Expected: `18 passed, 0 failed`.

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/MainWindow.xaml tests/Snaipe.Inspector.Tests/MainViewModelTests.cs
git commit -m "feat: wire Host binding in MainWindow, add breadcrumb VM tests"
```

---

## Smoke Test Checklist

Launch `Snaipe.SampleApp` and `Snaipe.Inspector` together and verify:

- [ ] Select an element that has a DataContext — a `›` chevron appears on the `DataContext` row in the Name column
- [ ] Clicking `›` on `DataContext` replaces the grid with the ViewModel's CLR properties; breadcrumb shows `ElementType › DataContext`
- [ ] Clicking `›` on a nested object-valued property drills another level; breadcrumb grows
- [ ] Clicking any breadcrumb crumb navigates back to that depth; crumbs after it disappear
- [ ] Editing a property while drilled in (e.g., a string on the ViewModel) commits the value via the agent
- [ ] Selecting a different element in the tree resets the breadcrumb and shows the new element's root properties
- [ ] Elements without a DataContext show no chevron on non-object properties
- [ ] Read-only properties at any drill depth show a checked R/O checkbox and no editor
