using System.IO;
using System.IO.Pipes;
using Snaipe.Protocol;

namespace Snaipe.Inspector;

/// <summary>
/// Named pipe IPC client that connects to a Snaipe agent and sends/receives messages.
/// Manages two connections: the command pipe (request/response) and the events pipe (push).
/// </summary>
public sealed class InspectorIpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private NamedPipeClientStream? _eventsPipe;
    private CancellationTokenSource? _eventsCts;

    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public bool IsConnected => _pipe is { IsConnected: true };

    /// <summary>
    /// Raised on a background thread when the agent pushes an event.
    /// The handler must marshal to the UI thread if it touches UI state.
    /// </summary>
    public event Action<InspectorMessage>? EventReceived;

    /// <summary>
    /// Connect to an agent's command pipe.
    /// </summary>
    public async Task ConnectAsync(string pipeName, CancellationToken ct = default)
    {
        _pipe?.Dispose();

        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(ConnectionTimeout);

        await _pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the agent's events push pipe and start a background read loop.
    /// Non-fatal: if the connection fails within 5s, events are silently unavailable.
    /// Call after ConnectAsync succeeds.
    /// </summary>
    public async Task ConnectEventsAsync(string eventsPipeName, CancellationToken ct = default)
    {
        _eventsCts?.Cancel();
        _eventsCts?.Dispose();
        _eventsPipe?.Dispose();

        _eventsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            _eventsPipe = new NamedPipeClientStream(".", eventsPipeName, PipeDirection.In, PipeOptions.Asynchronous);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_eventsCts.Token);
            connectCts.CancelAfter(ConnectionTimeout);

            await _eventsPipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

            // Start background read loop — fire and forget.
            _ = Task.Run(() => RunEventsLoopAsync(_eventsCts.Token), _eventsCts.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InspectorIpcClient] Events pipe unavailable: {ex.Message}");
            _eventsPipe?.Dispose();
            _eventsPipe = null;
        }
    }

    private async Task RunEventsLoopAsync(CancellationToken ct)
    {
        if (_eventsPipe is null) return;

        try
        {
            while (!ct.IsCancellationRequested && _eventsPipe.IsConnected)
            {
                var message = await MessageFraming.ReadMessageAsync(_eventsPipe, ct).ConfigureAwait(false);
                if (message is null) break; // server closed connection

                try { EventReceived?.Invoke(message); }
                catch { /* handler errors must not kill the loop */ }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (IOException) { /* agent disconnected */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InspectorIpcClient] Events loop error: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a request and wait for a typed response.
    /// </summary>
    public async Task<TResponse> SendAsync<TResponse>(InspectorMessage request, CancellationToken ct = default)
        where TResponse : InspectorMessage
    {
        if (_pipe is not { IsConnected: true })
            throw new InvalidOperationException("Not connected to an agent.");

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(RequestTimeout);

        await MessageFraming.WriteMessageAsync(_pipe, request, requestCts.Token).ConfigureAwait(false);
        var response = await MessageFraming.ReadMessageAsync(_pipe, requestCts.Token).ConfigureAwait(false);

        if (response is null)
            throw new IOException("Connection closed while waiting for response.");

        if (response is TResponse typed)
            return typed;

        if (response is ErrorResponse error)
            throw new SnaipeProtocolException(error.ErrorCode, error.Error, error.Details);

        throw new InvalidOperationException(
            $"Unexpected response type: {response.GetType().Name}, expected {typeof(TResponse).Name}");
    }

    /// <summary>
    /// Send a request and return the raw InspectorMessage response.
    /// </summary>
    public async Task<InspectorMessage> SendRawAsync(InspectorMessage request, CancellationToken ct = default)
    {
        if (_pipe is not { IsConnected: true })
            throw new InvalidOperationException("Not connected to an agent.");

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(RequestTimeout);

        await MessageFraming.WriteMessageAsync(_pipe, request, requestCts.Token).ConfigureAwait(false);
        var response = await MessageFraming.ReadMessageAsync(_pipe, requestCts.Token).ConfigureAwait(false);

        return response ?? throw new IOException("Connection closed while waiting for response.");
    }

    public void Disconnect()
    {
        _eventsCts?.Cancel();
        _eventsCts?.Dispose();
        _eventsCts = null;

        _eventsPipe?.Dispose();
        _eventsPipe = null;

        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose() => Disconnect();
}

/// <summary>
/// Exception thrown when the agent returns an ErrorResponse.
/// </summary>
public class SnaipeProtocolException : Exception
{
    public int ErrorCode { get; }
    public string? Details { get; }

    public SnaipeProtocolException(int errorCode, string error, string? details)
        : base($"[{errorCode}] {error}" + (details is not null ? $": {details}" : ""))
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
