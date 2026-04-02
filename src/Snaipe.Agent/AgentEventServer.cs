using System.IO.Pipes;
using System.Threading.Channels;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Owns the agent→inspector push notification pipe (snaipe-{pid}-events).
/// Enqueued events are written to the connected inspector in order.
/// If no inspector is connected, events are silently discarded.
/// </summary>
public sealed class AgentEventServer : IDisposable
{
    private readonly string _pipeName;
    private readonly Channel<InspectorMessage> _queue;
    private NamedPipeServerStream? _pipe;

    public AgentEventServer(string pipeName)
    {
        _pipeName = pipeName;
        _queue = Channel.CreateBounded<InspectorMessage>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Queue an event for delivery to the connected inspector. Thread-safe. Non-blocking.
    /// If no inspector is connected or the queue is full, the event is silently discarded.
    /// </summary>
    public void EnqueueEvent(InspectorMessage message)
    {
        _queue.Writer.TryWrite(message);
    }

    /// <summary>
    /// Run the events server loop. Accepts one client at a time and streams queued events.
    /// Call from a background Task via Task.Run. Exits when ct is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.Out,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                System.Diagnostics.Debug.WriteLine($"[AgentEventServer] Waiting for client on '{_pipeName}'...");
                await _pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine("[AgentEventServer] Client connected.");

                await DrainAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // Client disconnected — loop back to waiting.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentEventServer] Error: {ex.Message}");
            }
            finally
            {
                try { _pipe?.Dispose(); } catch { }
                _pipe = null;
            }

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(500, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        var reader = _queue.Reader;

        while (_pipe is { IsConnected: true } && !ct.IsCancellationRequested)
        {
            // Wait up to 200 ms for a queued event, then check pipe still alive.
            bool hasData;
            try
            {
                using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pollCts.CancelAfter(200);
                hasData = await reader.WaitToReadAsync(pollCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Poll timeout — check pipe connection and retry.
                continue;
            }

            if (!hasData) break; // channel completed (Dispose called)

            while (reader.TryRead(out var message))
            {
                if (_pipe is not { IsConnected: true }) return;
                try
                {
                    await MessageFraming.WriteMessageAsync(_pipe, message, ct).ConfigureAwait(false);
                }
                catch (IOException) { return; }
            }
        }
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
    }
}
