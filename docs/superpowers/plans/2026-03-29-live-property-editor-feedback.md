# Live Property Editor Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable immediate and persistent error feedback in the live property editor when updates fail in the target application.

**Architecture:** Update the `Snaipe.Protocol` to allow `AckResponse` to return normalized values, update `Snaipe.Agent` to return errors or normalized values, and update `Snaipe.Inspector` to handle these responses and drive the UI error state.

**Tech Stack:** C#, .NET 9, Uno Platform, IPC (Named Pipes), JSON.

---

### Task 1: Update Protocol

**Files:**
- Modify: `src/Snaipe.Protocol/Messages.cs`

- [ ] **Step 1: Add `NormalizedValue` to `AckResponse`**

```csharp
public sealed record AckResponse : InspectorMessage
{
    /// <summary>
    /// The final value as applied by the agent (e.g. "10.0" if user typed "10")
    /// </summary>
    public string? NormalizedValue { get; init; }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Snaipe.Protocol/Messages.cs
git commit -m "protocol: add NormalizedValue to AckResponse"
```

### Task 2: Update Agent PropertyWriter

**Files:**
- Modify: `src/Snaipe.Agent/PropertyWriter.cs`

- [ ] **Step 1: Change `SetProperty` return type to include the normalized value**

Update `SetProperty` signature and implementation to return `(int ErrorCode, string Error, string? Details, string? NormalizedValue)?`.

```csharp
    /// <returns>null on success (with normalized value), or an error tuple on failure.</returns>
    public static (int ErrorCode, string Error, string? Details, string? NormalizedValue)? SetProperty(
        DependencyObject element, string propertyName, string newValue)
    {
        // ... existing logic ...
        // After setting the value:
        var normalizedValue = parsedValue?.ToString() ?? newValue;
        return (0, "", null, normalizedValue); // Using 0 for success internally or just null if preferred.
    }
```

Actually, let's keep the existing `null` for success but return the value in an out parameter or change the tuple.
Let's change the return type to a more explicit struct or tuple.

New signature:
`public static SetPropertyResult SetProperty(DependencyObject element, string propertyName, string newValue)`

```csharp
public record struct SetPropertyResult(bool Success, int ErrorCode = 0, string? Error = null, string? Details = null, string? NormalizedValue = null);
```

- [ ] **Step 2: Implement `SetPropertyResult` and update `SetProperty`**

Modify `src/Snaipe.Agent/PropertyWriter.cs`:
```csharp
public record struct SetPropertyResult(bool Success, int ErrorCode = 0, string? Error = null, string? Details = null, string? NormalizedValue = null);

public static class PropertyWriter
{
    public static SetPropertyResult SetProperty(DependencyObject element, string propertyName, string newValue)
    {
        // ... (find field, check read-only) ...
        // If error: return new SetPropertyResult(false, ErrorCodes.PropertyNotFound, "Property not found", "...");

        // ... (parse value) ...
        // If catch ex: return new SetPropertyResult(false, ErrorCodes.InvalidPropertyValue, "Invalid property value", ex.Message);

        // ... (set value) ...
        element.SetValue(dp, parsedValue);
        return new SetPropertyResult(true, NormalizedValue: parsedValue?.ToString() ?? newValue);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Snaipe.Agent/PropertyWriter.cs
git commit -m "agent: update PropertyWriter to return SetPropertyResult"
```

### Task 3: Update SnaipeAgent Handler

**Files:**
- Modify: `src/Snaipe.Agent/SnaipeAgent.cs`

- [ ] **Step 1: Update `HandleSetProperty` to use `AckResponse`**

Modify `HandleSetProperty` to call the new `PropertyWriter.SetProperty` and return `AckResponse` on success.

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

                var result = PropertyWriter.SetProperty(element, request.PropertyName, request.NewValue);
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
                    NormalizedValue = result.NormalizedValue
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

- [ ] **Step 2: Commit**

```bash
git add src/Snaipe.Agent/SnaipeAgent.cs
git commit -m "agent: change HandleSetProperty to return AckResponse"
```

### Task 4: Update Inspector MainViewModel

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Update `SetPropertyAsync` to use `AckResponse`**

```csharp
    public async Task SetPropertyAsync(string elementId, string propertyName, string newValue,
        PropertyRowViewModel row)
    {
        try
        {
            var ack = await _client.SendAsync<AckResponse>(
                new SetPropertyRequest
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ElementId = elementId,
                    PropertyName = propertyName,
                    NewValue = newValue,
                });

            row.ClearError();
            if (ack.NormalizedValue != null)
                row.EditValue = ack.NormalizedValue;
        }
        catch (SnaipeProtocolException ex)
        {
            // Persistent error state
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

- [ ] **Step 2: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs
git commit -m "inspector: update MainViewModel to use AckResponse for property updates"
```

### Task 5: Verify and Add Unit Tests

**Files:**
- Create/Modify: `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` (if it exists, else check existing tests)

- [ ] **Step 1: Verify `PropertyRowViewModel` property changes**

Ensure `SetError` correctly updates `ErrorBorderBrush` and `HasError`.

- [ ] **Step 2: Add test for `MainViewModel.SetPropertyAsync` (Optional/Manual)**

Since `MainViewModel` tests might be hard without mocking the client, perform a manual smoke test with `SampleApp`.

- [ ] **Step 3: Commit**

```bash
git commit -m "test: verify property error states"
```
