# Design Spec: Live Property Editor Feedback Loop

**Status:** Draft
**Date:** 2026-03-29
**Topic:** Live Property Editor Feedback Loop

## 1. Goal
Improve the user experience of the live property editor in `Snaipe.Inspector` by providing immediate and persistent feedback when an update fails in the target application (`Snaipe.Agent`).

## 2. Success Criteria
- [ ] Users receive a clear visual indication (red border) when a property update fails.
- [ ] The specific error message from the target app (e.g., "Cannot convert 'abc' to Double") is visible to the user.
- [ ] The error state persists until the user provides a valid value or the property is successfully updated.
- [ ] The inspector does not "flicker" or revert the user's input prematurely during an error state.

## 3. Architecture: Request/Response Feedback Loop

We will use the existing IPC request/response pattern to communicate success or failure.

### 3.1. Protocol Updates (`Snaipe.Protocol`)
The current `AckResponse` is an empty record. We will extend it to optionally return the "normalized" value as applied by the agent.

```csharp
public sealed record AckResponse : InspectorMessage
{
    // The final value as applied by the agent (e.g. "10.0" if user typed "10")
    public string? NormalizedValue { get; init; }
}
```

The `ErrorResponse` already contains `ErrorCode`, `Error`, and `Details`, which is sufficient for reporting exceptions.

### 3.2. Agent Implementation (`Snaipe.Agent`)
The `AgentIpcServer` currently handles `SetPropertyRequest` by calling `PropertyWriter.SetProperty`. We will ensure it:
1.  Executes `SetProperty` on the UI thread.
2.  Returns an `ErrorResponse` if `PropertyWriter` returns an error tuple.
3.  Returns an `AckResponse` with the `NormalizedValue` (the result of `parsedValue.ToString()`) on success.

### 3.3. Inspector ViewModel Logic (`Snaipe.Inspector`)
The `PropertyRowViewModel` will be updated to handle the asynchronous result of the `CommitEditCommand`.

#### `PropertyRowViewModel.cs`
- Update `CommitEditCommand` to be an `AsyncRelayCommand`.
- Catch `SnaipeProtocolException` thrown by `InspectorIpcClient`.
- Use `SetError(ex.Message)` to trigger the UI error state.
- Clear error state on successful update.

```csharp
public async Task CommitEdit()
{
    try
    {
        ClearError();
        // Send request and wait for AckResponse
        var ack = await _ipcClient.SendAsync<AckResponse>(new SetPropertyRequest {
            MessageId = Guid.NewGuid().ToString(),
            ElementId = Entry.ElementId,
            PropertyName = Entry.Name,
            NewValue = EditValue
        });

        // Update with normalized value if provided
        if (ack.NormalizedValue != null)
        {
            EditValue = ack.NormalizedValue;
        }
    }
    catch (SnaipeProtocolException ex)
    {
        // Persistent error state (Option B)
        SetError(ex.Message);
    }
    catch (Exception ex)
    {
        SetError($"Communication error: {ex.Message}");
    }
}
```

### 3.4. UI Components (`Snaipe.Inspector`)
- **`TextBox`**: Already bound to `ErrorBorderBrush` and `ErrorMessage`.
- **`LostFocus` Trigger**: The `TextBox` uses `UpdateSourceTrigger=LostFocus`. We should also consider adding an `Enter` key behavior to trigger `CommitEditCommand` immediately.

## 4. Testing Strategy
1.  **Manual Integration Test**:
    - Launch `Snaipe.SampleApp`.
    - Launch `Snaipe.Inspector` and connect.
    - Select an element with a numeric property (e.g., `FontSize`).
    - Type "abc" and press Tab (LostFocus).
    - **Verify**: The border turns red and a tooltip shows "Cannot parse 'abc' as Double".
    - Type "20" and press Tab.
    - **Verify**: The border returns to normal and the property updates in the SampleApp.
2.  **Unit Tests**:
    - Update `PropertyRowViewModelTests` to verify `SetError` is called when the IPC client throws.

## 5. Alternatives Considered
- **Immediate Revert**: Rejected because users lose their work and don't know why it failed.
- **Client-side Validation**: Rejected as primary mechanism because it cannot catch app-specific runtime errors (e.g., a `CoerceValueCallback` rejecting a value).
